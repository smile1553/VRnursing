using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ExperimentSessionClient : MonoBehaviour
{
    [Header("Backend")]
    [Tooltip("Optional fixed LAN URL, for example http://192.168.1.100:8000. Leave empty to share RunAI_Network discovery.")]
    public string backendBaseUrl = string.Empty;
    public RunAI_Network runAiNetwork;
    public int timeoutSeconds = 10;

    public string LastStatusMessage { get; private set; }
    public long LastResponseCode { get; private set; }
    public bool IsBusy { get; private set; }
    public bool ApplicationSessionReady { get; private set; }
    public bool StartupBlocked { get; private set; }

    public event Action<string> StatusChanged;
    public event Action<string> SessionChanged;

    void Awake()
    {
        if (!runAiNetwork)
            runAiNetwork = FindObjectOfType<RunAI_Network>();
    }

    IEnumerator Start()
    {
        // RunAI_Network resolves its URL asynchronously in Start().
        float deadline = Time.realtimeSinceStartup + 5f;
        while (string.IsNullOrWhiteSpace(ResolveBaseUrl()) && Time.realtimeSinceStartup < deadline)
            yield return null;

        string resolvedUrl = ResolveBaseUrl();
        if (!ValidateBaseUrl(resolvedUrl, null))
        {
            StartupBlocked = true;
            yield break;
        }
        SetStatus("Backend URL: " + resolvedUrl);

        StudentResultUploader uploader = FindObjectOfType<StudentResultUploader>();
        bool restored = uploader != null && uploader.RestorePendingResultFromDisk();
        if (PendingResultStore.Exists && !restored)
        {
            StartupBlocked = true;
            SetStatus("Pending result exists but could not be restored. New Session creation is blocked.");
            yield break;
        }

        if (restored)
        {
            uploader.RetryPendingResult();
            while (StudentRunContext.Current.HasPendingResult)
                yield return null;
        }

        SessionResponse previousCurrentSession = null;
        bool currentSessionRead = false;
        yield return ReadCurrentSessionRoutine((success, response) =>
        {
            currentSessionRead = success;
            previousCurrentSession = response;
        });
        if (!currentSessionRead)
        {
            StartupBlocked = true;
            SetStatus("Could not save the previous current Session before POST. Startup is blocked to prevent writing into an old Excel file.");
            yield break;
        }

        string previousCurrentSessionId = previousCurrentSession != null
            ? previousCurrentSession.sessionId
            : string.Empty;
        bool created = false;
        yield return BeginNewSessionRoutine(success => created = success, true, previousCurrentSessionId);
        ApplicationSessionReady = created;
        StartupBlocked = !created;
    }

    public string ResolveBaseUrl()
    {
        string value = !string.IsNullOrWhiteSpace(backendBaseUrl)
            ? backendBaseUrl
            : runAiNetwork != null ? runAiNetwork.serverBaseUrl : string.Empty;

        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/');
    }

    public void RefreshCurrentSession()
    {
        RefreshCurrentSession(null);
    }

    public void RefreshCurrentSession(Action<bool> completed)
    {
        if (IsBusy)
        {
            SetStatus("Backend request is already in progress.");
            completed?.Invoke(false);
            return;
        }
        StartCoroutine(RefreshCurrentSessionRoutine(completed));
    }

    public void BeginNewSession()
    {
        BeginNewSession(null);
    }

    public void BeginNewSession(Action<bool> completed)
    {
        SetStatus("Manual Session creation is disabled. One Session is created automatically per App run.");
        completed?.Invoke(false);
    }

    public void StartStudentRun(Action<bool> completed)
    {
        if (IsBusy)
        {
            SetStatus("Backend request is already in progress.");
            completed?.Invoke(false);
            return;
        }
        StartCoroutine(StartStudentRunRoutine(completed));
    }

    IEnumerator RefreshCurrentSessionRoutine(Action<bool> completed)
    {
        string baseUrl = ResolveBaseUrl();
        if (!ValidateBaseUrl(baseUrl, completed))
            yield break;

        IsBusy = true;
        using (UnityWebRequest request = UnityWebRequest.Get(baseUrl + "/api/sessions/current"))
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();
            LastResponseCode = request.responseCode;

            if (IsHttpSuccess(request))
            {
                SessionResponse response = ParseSessionResponse(request.downloadHandler.text);
                if (response != null && response.ok && !string.IsNullOrWhiteSpace(response.sessionId))
                {
                    StudentRunContext.Current.SetSession(response.sessionId, response.createdAt);
                    SetStatus("Current session: " + response.sessionId);
                    SessionChanged?.Invoke(response.sessionId);
                    IsBusy = false;
                    completed?.Invoke(true);
                    yield break;
                }
                SetStatus("Backend returned an invalid current-session response.");
            }
            else if (request.responseCode == 404)
            {
                StudentRunContext.Current.ClearSession();
                SetStatus("No active session. An administrator must start a new experiment round.");
            }
            else
            {
                SetStatus(BuildError("GET current session", request));
            }
        }

        IsBusy = false;
        completed?.Invoke(false);
    }

    IEnumerator BeginNewSessionRoutine(
        Action<bool> completed,
        bool recoverCurrentOnConnectionFailure = false,
        string previousCurrentSessionId = "")
    {
        string baseUrl = ResolveBaseUrl();
        if (!ValidateBaseUrl(baseUrl, completed))
            yield break;

        IsBusy = true;
        using (UnityWebRequest request = new UnityWebRequest(baseUrl + "/api/sessions", UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(new byte[0]);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();
            LastResponseCode = request.responseCode;

            if (IsHttpSuccess(request))
            {
                SessionResponse response = ParseSessionResponse(request.downloadHandler.text);
                if (response != null && response.ok && !string.IsNullOrWhiteSpace(response.sessionId))
                {
                    StudentRunContext.Current.SetSession(response.sessionId, response.createdAt);
                    SetStatus("New session created: " + response.sessionId);
                    SessionChanged?.Invoke(response.sessionId);
                    IsBusy = false;
                    completed?.Invoke(true);
                    yield break;
                }
                SetStatus("Backend returned an invalid create-session response.");
            }
            else
            {
                SetStatus(BuildError("POST session", request));
                if (recoverCurrentOnConnectionFailure && request.responseCode == 0)
                {
                    IsBusy = false;
                    bool lookupSucceeded = false;
                    SessionResponse currentSession = null;
                    SetStatus("Session POST had no HTTP response; checking current Session before any further POST.");
                    yield return ReadCurrentSessionRoutine((success, response) =>
                    {
                        lookupSucceeded = success;
                        currentSession = response;
                    });

                    bool recovered = lookupSucceeded &&
                        currentSession != null &&
                        !string.IsNullOrWhiteSpace(currentSession.sessionId) &&
                        !string.Equals(currentSession.sessionId, previousCurrentSessionId, StringComparison.Ordinal);
                    if (recovered)
                    {
                        StudentRunContext.Current.SetSession(currentSession.sessionId, currentSession.createdAt);
                        SetStatus("Recovered newly created session after POST timeout: " + currentSession.sessionId);
                        SessionChanged?.Invoke(currentSession.sessionId);
                    }
                    else if (lookupSucceeded && currentSession != null &&
                        string.Equals(currentSession.sessionId, previousCurrentSessionId, StringComparison.Ordinal))
                    {
                        SetStatus("Session POST timed out and current Session is still the previous Session. Startup remains blocked.");
                    }
                    else
                    {
                        SetStatus("Session POST timed out and a different new current Session could not be confirmed. Startup remains blocked.");
                    }
                    completed?.Invoke(recovered);
                    yield break;
                }
            }
        }

        IsBusy = false;
        completed?.Invoke(false);
    }

    IEnumerator StartStudentRunRoutine(Action<bool> completed)
    {
        string baseUrl = ResolveBaseUrl();
        if (!ValidateBaseUrl(baseUrl, completed))
            yield break;

        StudentRunContext context = StudentRunContext.Current;
        if (!ApplicationSessionReady)
        {
            SetStatus("Application Session is not ready; student login is blocked.");
            completed?.Invoke(false);
            yield break;
        }
        if (!context.HasStudentRun)
        {
            SetStatus("Student run context is incomplete.");
            completed?.Invoke(false);
            yield break;
        }

        StudentRunStartRequest payload = new StudentRunStartRequest
        {
            sessionId = context.SessionId,
            resultId = context.ResultId,
            studentId = context.StudentId,
            loginTime = context.LoginTime
        };

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        IsBusy = true;
        using (UnityWebRequest request = new UnityWebRequest(baseUrl + "/api/student-runs/start", UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();
            LastResponseCode = request.responseCode;

            if (IsHttpSuccess(request))
            {
                string responseJson = request.downloadHandler.text;
                string[] requiredFields = { "ok", "sessionId", "resultId", "studentId", "loginTime" };
                StudentRunStartResponse response = BackendJsonObjectValidator.HasRequiredTopLevelProperties(responseJson, requiredFields)
                    ? ParseStudentRunStartResponse(responseJson)
                    : null;
                if (response != null && response.ok &&
                    response.sessionId == context.SessionId &&
                    response.resultId == context.ResultId &&
                    response.studentId == context.StudentId &&
                    RepresentsSameInstant(response.loginTime, context.LoginTime))
                {
                    context.StudentRunAccepted = true;
                    SetStatus("Student run started: " + context.StudentId);
                    IsBusy = false;
                    completed?.Invoke(true);
                    yield break;
                }

                SetStatus("Student-run response is missing required fields or does not match the original run. The same resultId and loginTime are retained for retry.");
                IsBusy = false;
                completed?.Invoke(false);
                yield break;
            }

            SetStatus(BuildError("POST student run", request));
        }

        IsBusy = false;
        completed?.Invoke(false);
    }

    bool ValidateBaseUrl(string baseUrl, Action<bool> completed)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            SetStatus("Backend URL is not ready. Set a PC LAN URL or check server discovery.");
            completed?.Invoke(false);
            return false;
        }

        if (RunAI_Network.IsLoopbackUrl(baseUrl) && Application.platform == RuntimePlatform.Android)
        {
            SetStatus("Quest cannot use localhost or 127.0.0.1 as the Backend URL.");
            completed?.Invoke(false);
            return false;
        }
        return true;
    }

    void SetStatus(string message)
    {
        LastStatusMessage = message;
        Debug.Log("[ExperimentSession] " + message, this);
        StatusChanged?.Invoke(message);
    }

    static bool IsHttpSuccess(UnityWebRequest request)
    {
        return request.responseCode >= 200 && request.responseCode < 300;
    }

    static string BuildError(string operation, UnityWebRequest request)
    {
        string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        return string.Format("{0} failed. HTTP {1}: {2} {3}", operation, request.responseCode, request.error, body).Trim();
    }

    static SessionResponse ParseSessionResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try { return JsonUtility.FromJson<SessionResponse>(json); }
        catch (Exception exception)
        {
            Debug.LogWarning("[ExperimentSession] Invalid JSON: " + exception.Message);
            return null;
        }
    }

    static StudentRunStartResponse ParseStudentRunStartResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonUtility.FromJson<StudentRunStartResponse>(json); }
        catch { return null; }
    }

    IEnumerator ReadCurrentSessionRoutine(Action<bool, SessionResponse> completed)
    {
        string baseUrl = ResolveBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            completed?.Invoke(false, null);
            yield break;
        }

        IsBusy = true;
        using (UnityWebRequest request = UnityWebRequest.Get(baseUrl + "/api/sessions/current"))
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();
            LastResponseCode = request.responseCode;

            if (request.responseCode == 404)
            {
                IsBusy = false;
                completed?.Invoke(true, null);
                yield break;
            }

            if (IsHttpSuccess(request))
            {
                string responseJson = request.downloadHandler.text;
                string[] requiredFields = { "ok", "sessionId", "createdAt" };
                SessionResponse response = BackendJsonObjectValidator.HasRequiredTopLevelProperties(responseJson, requiredFields)
                    ? ParseSessionResponse(responseJson)
                    : null;
                if (response != null && response.ok && !string.IsNullOrWhiteSpace(response.sessionId))
                {
                    IsBusy = false;
                    completed?.Invoke(true, response);
                    yield break;
                }
            }

            SetStatus(BuildError("GET current session snapshot", request));
        }

        IsBusy = false;
        completed?.Invoke(false, null);
    }

    static bool RepresentsSameInstant(string left, string right)
    {
        DateTimeOffset a;
        DateTimeOffset b;
        return DateTimeOffset.TryParse(left, out a) &&
            DateTimeOffset.TryParse(right, out b) &&
            a.ToUniversalTime() == b.ToUniversalTime();
    }

    [Serializable]
    public class SessionResponse
    {
        public bool ok;
        public string sessionId;
        public string createdAt;
    }

    [Serializable]
    public class StudentRunStartRequest
    {
        public string sessionId;
        public string resultId;
        public string studentId;
        public string loginTime;
    }

    [Serializable]
    public class StudentRunStartResponse
    {
        public bool ok;
        public string sessionId;
        public string resultId;
        public string studentId;
        public string loginTime;
    }
}
