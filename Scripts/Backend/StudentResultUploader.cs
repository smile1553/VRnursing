using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class StudentResultUploader : MonoBehaviour
{
    public enum ToneScoreSource
    {
        ExplicitFinalToneScore,
        PerformanceToneScore
    }

    [Header("References")]
    public ScenarioController scenarioController;
    public PerformanceScoreManager scoreManager;
    public ExperimentSessionClient sessionClient;

    [Header("Tone score")]
    [Tooltip("Product definition: PerformanceScoreManager.ToneScore is the formal 0..20 score. ExplicitFinalToneScore remains available only for tests or a future override.")]
    public ToneScoreSource toneScoreSource = ToneScoreSource.PerformanceToneScore;

    [Header("Retry")]
    public bool autoRetryTransientFailures = true;
    [Tooltip("0 means retry transient failures until the pending result succeeds.")]
    public int maxAutomaticRetries = 0;
    public float firstRetryDelaySeconds = 2f;

    public string LastStatusMessage { get; private set; }
    public long LastResponseCode { get; private set; }
    public event Action ResultUploadSucceeded;

    Coroutine retryRoutine;
    int automaticRetryCount;

    void Awake()
    {
        if (!scenarioController) scenarioController = FindObjectOfType<ScenarioController>();
        if (!scoreManager) scoreManager = FindObjectOfType<PerformanceScoreManager>();
        if (!sessionClient) sessionClient = FindObjectOfType<ExperimentSessionClient>();
    }

    void OnEnable()
    {
        if (scenarioController != null)
            scenarioController.onScenarioCompleted.AddListener(HandleScenarioCompleted);
    }

    void OnDisable()
    {
        if (scenarioController != null)
            scenarioController.onScenarioCompleted.RemoveListener(HandleScenarioCompleted);
        if (retryRoutine != null)
        {
            StopCoroutine(retryRoutine);
            retryRoutine = null;
        }
    }

    public bool SetFinalToneScore(int score)
    {
        bool accepted = StudentRunContext.Current.TrySetToneScore(score);
        if (!accepted)
            SetStatus("ToneScore must be an integer from 0 to 20.", true);
        else
            TryFreezeAndUpload();
        return accepted;
    }

    public bool RestorePendingResultFromDisk()
    {
        FrozenStudentResult restored;
        string error;
        if (!PendingResultStore.TryLoad(out restored, out error))
        {
            if (!string.IsNullOrEmpty(error))
                SetStatus(error, true);
            return false;
        }

        StudentRunContext.Current.RestorePendingResult(restored);
        SetStatus("Recovered pending result " + restored.resultId + " from " + PendingResultStore.FilePath, false);
        return true;
    }

    public void RetryPendingResult()
    {
        StudentRunContext context = StudentRunContext.Current;
        if (context.ResultSubmitted || context.IsSubmitting)
            return;
        if (retryRoutine != null)
        {
            StopCoroutine(retryRoutine);
            retryRoutine = null;
        }
        if (!context.HasPendingResult)
        {
            TryFreezeAndUpload();
            return;
        }
        StartCoroutine(UploadFrozenResult());
    }

    void HandleScenarioCompleted()
    {
        StudentRunContext.Current.ScenarioCompleted = true;
        TryFreezeAndUpload();
    }

    void TryFreezeAndUpload()
    {
        StudentRunContext context = StudentRunContext.Current;
        if (retryRoutine != null)
            return;
        if (!context.HasStudentRun)
        {
            SetStatus("Scenario completed without an active student run; result was not uploaded.", true);
            return;
        }
        if (context.ResultSubmitted || context.IsSubmitting)
            return;
        if (!context.ScenarioCompleted)
        {
            SetStatus("ToneScore is ready; waiting for Scenario completion before freezing the result.", false);
            return;
        }

        if (!context.HasPendingResult)
        {
            if (scoreManager == null)
            {
                SetStatus("PerformanceScoreManager is missing; result was not uploaded.", true);
                return;
            }

            int correctCount = scoreManager.QuizCorrectCount;
            if (correctCount < 0 || correctCount > 8)
            {
                SetStatus("CorrectCount is outside the Backend range 0..8.", true);
                return;
            }

            int toneScore;
            if (!TryResolveToneScore(context, out toneScore))
            {
                SetStatus("Scenario is complete; waiting for a confirmed final ToneScore from 0 to 20.", true);
                return;
            }

            FrozenStudentResult frozen = context.FreezeResult(correctCount, toneScore);
            string persistenceError;
            if (!PendingResultStore.TrySaveNew(frozen, out persistenceError))
            {
                context.PendingResult = null;
                SetStatus(persistenceError + " Result was not uploaded because durable storage is required.", true);
                return;
            }
            SetStatus("Frozen result saved to " + PendingResultStore.FilePath, false);
            automaticRetryCount = 0;
        }

        StartCoroutine(UploadFrozenResult());
    }

    bool TryResolveToneScore(StudentRunContext context, out int toneScore)
    {
        if (toneScoreSource == ToneScoreSource.PerformanceToneScore)
        {
            toneScore = scoreManager != null ? scoreManager.ToneScore : -1;
            if (toneScore >= 0 && toneScore <= 20)
                return true;
            SetStatus("Performance ToneScore is outside 0..20.", true);
            return false;
        }

        if (context.HasToneScore)
        {
            toneScore = context.ToneScore;
            return true;
        }

        toneScore = 0;
        SetStatus("No explicit final ToneScore is available. Switch back to PerformanceToneScore for the formal scoring flow.", true);
        return false;
    }

    IEnumerator UploadFrozenResult()
    {
        StudentRunContext context = StudentRunContext.Current;
        if (context.ResultSubmitted || context.IsSubmitting || !context.HasPendingResult)
            yield break;

        string baseUrl = sessionClient != null ? sessionClient.ResolveBaseUrl() : string.Empty;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            SetStatus("Backend URL is unavailable; frozen result retained for retry.", true);
            ScheduleRetryIfAllowed();
            yield break;
        }
        if (RunAI_Network.IsLoopbackUrl(baseUrl) && Application.platform == RuntimePlatform.Android)
        {
            SetStatus("Quest cannot upload results to localhost or 127.0.0.1.", true);
            yield break;
        }

        FrozenStudentResult frozen = context.PendingResult;
        StudentResultRequest payload = new StudentResultRequest
        {
            studentId = frozen.studentId,
            loginTime = frozen.loginTime,
            correctCount = frozen.correctCount,
            toneScore = frozen.toneScore
        };
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        string url = string.Format("{0}/api/sessions/{1}/results/{2}",
            baseUrl.TrimEnd('/'),
            Uri.EscapeDataString(frozen.sessionId),
            Uri.EscapeDataString(frozen.resultId));

        context.IsSubmitting = true;
        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = sessionClient != null ? Mathf.Max(1, sessionClient.timeoutSeconds) : 10;
            yield return request.SendWebRequest();
            LastResponseCode = request.responseCode;

            if (request.responseCode >= 200 && request.responseCode < 300)
            {
                string responseJson = request.downloadHandler.text;
                string[] requiredFields =
                {
                    "ok", "duplicate", "sessionId", "resultId", "correctCount",
                    "questionScore", "toneScore", "totalScore"
                };
                if (!BackendJsonObjectValidator.HasRequiredTopLevelProperties(responseJson, requiredFields))
                {
                    context.IsSubmitting = false;
                    SetStatus("Backend result response is missing one or more required fields; frozen result retained.", true);
                    yield break;
                }

                StudentResultResponse response = ParseResponse(responseJson);
                int expectedQuestionScore = frozen.correctCount * 10;
                if (response == null || !response.ok ||
                    response.sessionId != frozen.sessionId ||
                    response.resultId != frozen.resultId ||
                    response.correctCount != frozen.correctCount ||
                    response.toneScore != frozen.toneScore ||
                    response.questionScore != expectedQuestionScore ||
                    response.totalScore != response.questionScore + response.toneScore)
                {
                    context.IsSubmitting = false;
                    SetStatus("Backend result response does not match the complete frozen result; result retained.", true);
                    yield break;
                }

                context.ResultSubmitted = true;
                context.IsSubmitting = false;
                string deleteError;
                if (PendingResultStore.TryDelete(out deleteError))
                {
                    context.PendingResult = null;
                }
                else
                {
                    context.ResultSubmitted = false;
                    SetStatus(deleteError, true);
                    ScheduleRetryIfAllowed();
                    yield break;
                }
                string duplicate = response.duplicate ? " (duplicate accepted)" : string.Empty;
                SetStatus("Student result uploaded" + duplicate + ".", false);
                ResultUploadSucceeded?.Invoke();
                yield break;
            }

            context.IsSubmitting = false;
            string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            SetStatus(string.Format("Result upload failed. HTTP {0}: {1} {2}", request.responseCode, request.error, responseBody).Trim(), true);

            bool transient = request.responseCode == 0 || request.responseCode == 503;
            if (transient)
                ScheduleRetryIfAllowed();
        }
    }

    void ScheduleRetryIfAllowed()
    {
        if (!autoRetryTransientFailures)
            return;
        if (maxAutomaticRetries > 0 && automaticRetryCount >= maxAutomaticRetries)
            return;
        ScheduleRetry();
    }

    void ScheduleRetry()
    {
        if (retryRoutine != null)
            return;
        automaticRetryCount++;
        float delay = Mathf.Min(30f, Mathf.Max(0.5f, firstRetryDelaySeconds) * Mathf.Pow(2f, automaticRetryCount - 1));
        retryRoutine = StartCoroutine(RetryAfterDelay(delay));
    }

    IEnumerator RetryAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        retryRoutine = null;
        yield return UploadFrozenResult();
    }

    void SetStatus(string message, bool warning)
    {
        LastStatusMessage = message;
        if (warning) Debug.LogWarning("[StudentResultUploader] " + message, this);
        else Debug.Log("[StudentResultUploader] " + message, this);
    }

    static StudentResultResponse ParseResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonUtility.FromJson<StudentResultResponse>(json); }
        catch { return null; }
    }

    [Serializable]
    public class StudentResultRequest
    {
        public string studentId;
        public string loginTime;
        public int correctCount;
        public int toneScore;
    }

    [Serializable]
    public class StudentResultResponse
    {
        public bool ok;
        public bool duplicate;
        public string sessionId;
        public string resultId;
        public int correctCount;
        public int questionScore;
        public int toneScore;
        public int totalScore;
    }
}
