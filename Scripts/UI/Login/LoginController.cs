using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LoginController : MonoBehaviour
{
    [Header("References (assign via Inspector)")]
    [SerializeField] Transform xrRig;
    [SerializeField] Transform entranceSpawn;
    [SerializeField] Transform wardSpawn;
    [SerializeField] GameObject loginUIRoot;
    [SerializeField] MonoBehaviour signalBus;

    [Header("Student Run Integration")]
    [SerializeField] TMP_InputField studentIdInput;
    [SerializeField] ScenarioController scenarioController;
    [SerializeField] PerformanceScoreManager performanceScoreManager;
    [SerializeField] ExperimentSessionClient sessionClient;
    [SerializeField] StudentResultUploader resultUploader;
    [SerializeField] bool requireStudentId = true;

    [Header("Ward Entry Flow")]
    [SerializeField] bool requireGreetingBeforeWardEntry = true;
    [SerializeField] string greetingStepId = "intro_nurse";

    bool loginInProgress;
    bool loginCompleted;
    bool wardEntered;
    bool listeningForScenarioCompletion;

    void Awake()
    {
        EnsureIntegrationRuntime();
    }

    void OnEnable()
    {
        SubscribeToScenario();
    }

    void OnDisable()
    {
        UnsubscribeFromScenario();
    }

    void Start()
    {
        SubscribeToScenario();
        RuntimeLog.Info("[LoginController] Start()");
        if (resultUploader != null)
            resultUploader.ResultUploadSucceeded += HandleResultUploadSucceeded;
        StartCoroutine(AlignOnStart());
    }

    void OnDestroy()
    {
        UnsubscribeFromScenario();
        if (resultUploader != null)
            resultUploader.ResultUploadSucceeded -= HandleResultUploadSucceeded;
    }

    public void OnLoginClicked()
    {
        RuntimeLog.Info("LOGIN CLICKED");
        if (loginInProgress) return;

        EnsureIntegrationRuntime();
        string studentId = studentIdInput != null ? studentIdInput.text.Trim() : string.Empty;
        if (requireStudentId && string.IsNullOrWhiteSpace(studentId))
        {
            RuntimeLog.Warning("[LoginController] Student ID is required.");
            if (studentIdInput != null)
            {
                studentIdInput.Select();
                studentIdInput.ActivateInputField();
            }
            return;
        }

        if (sessionClient == null)
        {
            RuntimeLog.Warning("[LoginController] ExperimentSessionClient is missing.");
            return;
        }
        if (!sessionClient.ApplicationSessionReady)
        {
            RuntimeLog.Warning("[LoginController] App startup is not ready. Pending upload recovery and Session creation must finish first.");
            return;
        }

        StudentRunContext existingRun = StudentRunContext.Current;
        if (existingRun.HasPendingResult)
        {
            RuntimeLog.Warning("[LoginController] A frozen result is still pending. Retry that upload before starting another student run.");
            resultUploader?.RetryPendingResult();
            return;
        }
        if (existingRun.HasStudentRun && !existingRun.ResultSubmitted &&
            !string.Equals(existingRun.StudentId, studentId, System.StringComparison.Ordinal))
        {
            RuntimeLog.Warning("[LoginController] A Student Run already exists. Retry it with the same StudentId before starting another student.");
            return;
        }
        if (existingRun.StudentRunAccepted && !existingRun.ScenarioCompleted)
        {
            RuntimeLog.Warning("[LoginController] The current student's Scenario is already running.");
            return;
        }

        loginInProgress = true;
        BeginStudentRun(studentId);
    }

    // Kept for old scene bindings. Session creation is now startup-only.
    public void BeginNewSession()
    {
        RuntimeLog.Warning("[LoginController] Manual Session creation is disabled; restart the App to begin a new experiment round.");
    }

    public void RefreshCurrentSession()
    {
        EnsureIntegrationRuntime();
        sessionClient?.RefreshCurrentSession();
    }

    public void RetryResultUpload()
    {
        EnsureIntegrationRuntime();
        resultUploader?.RetryPendingResult();
    }

    // Call this only with the confirmed final 0..20 score, never raw -1..1 tone data.
    public void SetFinalToneScore(int score)
    {
        EnsureIntegrationRuntime();
        resultUploader?.SetFinalToneScore(score);
    }

    void BeginStudentRun(string studentId)
    {
        StudentRunContext context = StudentRunContext.Current;
        bool reusePendingStart = context.HasStudentRun &&
            !context.ResultSubmitted &&
            string.Equals(context.StudentId, studentId, System.StringComparison.Ordinal);

        if (!reusePendingStart)
        {
            context.BeginStudentRun(studentId);
            ResetForStudent();
        }

        sessionClient.StartStudentRun(success =>
        {
            loginInProgress = false;
            if (success)
                CompleteLoginAndStartScenario();
            else
                RuntimeLog.Warning("[LoginController] Backend did not start the student run. Scenario remains stopped.");
        });
    }

    void ResetForStudent()
    {
        performanceScoreManager?.ResetScores();
        FindObjectOfType<ScenarioScoreManager>()?.ResetScore();
        FindObjectOfType<ScenarioKeywordAdvancer>()?.ResetForStudent();
        FindObjectOfType<RunAI>()?.ResetForStudent();
        FindObjectOfType<EmotionStateManager>()?.ResetForStudent();
        FindObjectOfType<AudioUploader>()?.ResetForStudent();
    }

    void CompleteLoginAndStartScenario()
    {
        loginCompleted = true;
        wardEntered = !requireGreetingBeforeWardEntry;

        Transform loginDestination = requireGreetingBeforeWardEntry ? entranceSpawn : wardSpawn;
        if (loginDestination != null)
            RuntimeLog.Info($"[LoginController] Target spawn={loginDestination.name} pos={FormatVec(loginDestination.position)} yaw={loginDestination.rotation.eulerAngles.y:0.###}");
        else if (requireGreetingBeforeWardEntry)
            RuntimeLog.Warning("[LoginController] entranceSpawn is not assigned. The player cannot wait outside the ward before greeting.");

        MoveRigTo(loginDestination);
        if (loginUIRoot != null)
            loginUIRoot.SetActive(false);

        EmitSignal("LoginCompleted", null);
        FindObjectOfType<AudioUploader>()?.StartLoop();
        SubscribeToScenario();
        if (scenarioController != null)
            scenarioController.StartScenario();
        else if (requireGreetingBeforeWardEntry)
            RuntimeLog.Warning("[LoginController] ScenarioController not found. Greeting completion cannot open the ward flow.");
    }

    void HandleResultUploadSucceeded()
    {
        loginCompleted = false;
        wardEntered = false;
        if (loginUIRoot != null)
            loginUIRoot.SetActive(true);
        if (studentIdInput != null)
            studentIdInput.text = string.Empty;
        MoveRigTo(entranceSpawn);
        RuntimeLog.Info("[LoginController] Result saved. Ready for the next student in the same Session.");
    }

    void SubscribeToScenario()
    {
        if (listeningForScenarioCompletion)
            return;
        if (scenarioController == null)
            scenarioController = FindObjectOfType<ScenarioController>();
        if (scenarioController == null || scenarioController.stepCompleted == null)
            return;

        scenarioController.stepCompleted.AddListener(OnScenarioStepCompleted);
        listeningForScenarioCompletion = true;
    }

    void UnsubscribeFromScenario()
    {
        if (!listeningForScenarioCompletion)
            return;
        if (scenarioController != null && scenarioController.stepCompleted != null)
            scenarioController.stepCompleted.RemoveListener(OnScenarioStepCompleted);
        listeningForScenarioCompletion = false;
    }

    void OnScenarioStepCompleted(string stepId)
    {
        if (!requireGreetingBeforeWardEntry || !loginCompleted || wardEntered)
            return;
        if (!string.Equals(stepId, greetingStepId, System.StringComparison.OrdinalIgnoreCase))
            return;

        wardEntered = true;
        RuntimeLog.Info($"[LoginController] Greeting step completed ({stepId}). Entering ward.");
        MoveRigTo(wardSpawn);
        EmitSignal("WardEntered", null);
    }

    void EnsureIntegrationRuntime()
    {
        if (studentIdInput == null && loginUIRoot != null)
            studentIdInput = loginUIRoot.GetComponentInChildren<TMP_InputField>(true);
        if (scenarioController == null)
            scenarioController = FindObjectOfType<ScenarioController>();

        if (sessionClient == null)
            sessionClient = FindObjectOfType<ExperimentSessionClient>();
        if (sessionClient == null)
            sessionClient = gameObject.AddComponent<ExperimentSessionClient>();

        if (performanceScoreManager == null)
            performanceScoreManager = FindObjectOfType<PerformanceScoreManager>();
        if (performanceScoreManager == null)
        {
            GameObject owner = scenarioController != null ? scenarioController.gameObject : gameObject;
            performanceScoreManager = owner.AddComponent<PerformanceScoreManager>();
        }
        if (scenarioController != null)
            performanceScoreManager.controller = scenarioController;
        if (performanceScoreManager.emotionState == null)
            performanceScoreManager.emotionState = FindObjectOfType<EmotionStateManager>();

        if (resultUploader == null)
            resultUploader = FindObjectOfType<StudentResultUploader>();
        if (resultUploader == null)
        {
            GameObject owner = scenarioController != null ? scenarioController.gameObject : gameObject;
            resultUploader = owner.AddComponent<StudentResultUploader>();
        }
        resultUploader.scenarioController = scenarioController;
        resultUploader.scoreManager = performanceScoreManager;
        resultUploader.sessionClient = sessionClient;
    }

    void MoveRigTo(Transform spawn)
    {
        if (xrRig == null || spawn == null)
        {
            if (xrRig == null) RuntimeLog.Warning("[LoginController] MoveRigTo aborted: xrRig is null.");
            if (spawn == null) RuntimeLog.Warning("[LoginController] MoveRigTo aborted: spawn is null.");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            RuntimeLog.Warning("[LoginController] MoveRigTo aborted: Camera.main is null.");
            return;
        }

        LogPose("[LoginController] MoveRigTo BEFORE", cam.transform, xrRig, spawn);
        Vector3 spawnPosition = spawn.position;
        float spawnYaw = spawn.rotation.eulerAngles.y + 180f;
        if (spawn.IsChildOf(cam.transform) || spawn.IsChildOf(xrRig))
            RuntimeLog.Warning("[LoginController] Spawn is under the HMD/XR rig. Use a fixed scene marker for stable teleport targets.");

        float yawDelta = spawnYaw - cam.transform.rotation.eulerAngles.y;
        xrRig.RotateAround(cam.transform.position, Vector3.up, yawDelta);
        Vector3 camOffset = cam.transform.position - xrRig.position;
        xrRig.position = new Vector3(spawnPosition.x - camOffset.x, spawnPosition.y, spawnPosition.z - camOffset.z);
        LogPose("[LoginController] MoveRigTo AFTER", cam.transform, xrRig, spawn);
    }

    void MoveRigToPose(Vector3 camWorldPos, float yawDeg)
    {
        if (xrRig == null)
        {
            RuntimeLog.Warning("[LoginController] MoveRigToPose aborted: xrRig is null.");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            RuntimeLog.Warning("[LoginController] MoveRigToPose aborted: Camera.main is null.");
            return;
        }

        xrRig.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        LogPose("[LoginController] MoveRigToPose BEFORE", cam.transform, xrRig, null);
        Vector3 camOffset = cam.transform.position - xrRig.position;
        xrRig.position = camWorldPos - camOffset;
        LogPose("[LoginController] MoveRigToPose AFTER", cam.transform, xrRig, null);
    }

    void EmitSignal(string signalName, object payload)
    {
        if (signalBus is ISignalBus emitter)
            emitter.Emit(signalName);
        else
            RuntimeLog.Warning("[LoginController] EmitSignal skipped: signalBus not set or does not implement ISignalBus.");
    }

    public interface ISignalBus
    {
        void Emit(string signalName);
    }

    IEnumerator AlignOnStart()
    {
        Vector3 targetCamPos = new Vector3(5.173f, 1.077f, -1.985f);
        const float targetYaw = 180.954f;
        const int initialFrameDelay = 3;
        const int maxAttempts = 6;

        for (int i = 0; i < initialFrameDelay; i++)
            yield return null;

        for (int i = 0; i < maxAttempts; i++)
        {
            yield return null;
            if (Camera.main == null) continue;
            if (xrRig == null) yield break;
            MoveRigToPose(targetCamPos, targetYaw);
            yield return null;
            MoveRigToPose(targetCamPos, targetYaw);
        }
    }

    void LogPose(string label, Transform cam, Transform rig, Transform spawn)
    {
        float camYaw = cam != null ? cam.rotation.eulerAngles.y : 0f;
        float rigYaw = rig != null ? rig.rotation.eulerAngles.y : 0f;
        float spawnYaw = spawn != null ? spawn.rotation.eulerAngles.y : 0f;
        RuntimeLog.Info($"{label} | camPos={FormatVec(cam != null ? cam.position : Vector3.zero)} camYaw={camYaw:0.###} " +
            $"| rigPos={FormatVec(rig != null ? rig.position : Vector3.zero)} rigYaw={rigYaw:0.###} " +
            $"| spawnPos={(spawn != null ? FormatVec(spawn.position) : "(none)")} spawnYaw={(spawn != null ? spawnYaw.ToString("0.###") : "(none)")}");
    }

    static string FormatVec(Vector3 value)
    {
        return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
    }
}
