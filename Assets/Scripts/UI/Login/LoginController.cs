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
    [SerializeField] GameObject introPanelRoot;
    [SerializeField] MonoBehaviour signalBus;

    [Header("Student Run Integration")]
    [SerializeField] TMP_InputField studentIdInput;
    [SerializeField] ScenarioController scenarioController;
    [SerializeField] PerformanceScoreManager performanceScoreManager;
    [SerializeField] ExperimentSessionClient sessionClient;
    [SerializeField] StudentResultUploader resultUploader;
    [SerializeField] bool requireStudentId = true;
    [Tooltip("Start pose: metres to step back from the login panel (+ = farther, - = closer).")]
    [SerializeField] float loginStartExtraDistance = 0.3f;

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
            {
                if (loginUIRoot != null) loginUIRoot.SetActive(true);
                if (introPanelRoot != null) introPanelRoot.SetActive(false);
                RuntimeLog.Warning("[LoginController] Backend did not start the student run. Scenario remains stopped.");
            }
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

        if (loginUIRoot != null)
            loginUIRoot.SetActive(false);
        if (introPanelRoot != null)
            introPanelRoot.SetActive(true);

        EmitSignal("LoginCompleted", null);
        SubscribeToScenario();
        if (scenarioController != null)
            scenarioController.EnsureScenarioStarted();
        else if (requireGreetingBeforeWardEntry)
            RuntimeLog.Warning("[LoginController] ScenarioController not found. Greeting completion cannot open the ward flow.");

        FindObjectOfType<AudioUploader>()?.StartLoop();
    }

    void HandleResultUploadSucceeded()
    {
        loginCompleted = false;
        wardEntered = false;
        if (loginUIRoot != null)
            loginUIRoot.SetActive(true);
        if (studentIdInput != null)
            studentIdInput.text = string.Empty;
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
        {
            GameObject owner = scenarioController != null ? scenarioController.gameObject : gameObject;
            sessionClient = owner.AddComponent<ExperimentSessionClient>();
        }

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

    // At start the player stands in front of the login panel, looking straight at it.
    IEnumerator AlignOnStart()
    {
        const int initialFrameDelay = 3;
        const int maxAttempts = 6;

        for (int i = 0; i < initialFrameDelay; i++)
            yield return null;

        bool hasScenePose = false;
        Vector3 sceneCamPos = Vector3.zero;
        float sceneRigY = 0f;

        for (int i = 0; i < maxAttempts; i++)
        {
            yield return null;
            Camera cam = Camera.main;
            if (cam == null) continue;
            if (xrRig == null) yield break;

            if (!hasScenePose)
            {
                hasScenePose = true;
                sceneCamPos = cam.transform.position;
                sceneRigY = xrRig.position.y;
            }

            if (TryGetLoginViewPose(sceneCamPos, out Vector3 camPos, out float yaw, out float panelHeight))
            {
                FaceLoginPanel(cam, camPos, yaw, sceneRigY, panelHeight);
                yield return null;
                FaceLoginPanel(cam, camPos, yaw, sceneRigY, panelHeight);
                if (i == 0)
                    RuntimeLog.Info($"[LoginController] Start pose: facing login panel, camPos={FormatVec(cam.transform.position)} yaw={yaw:0.#}");
            }
            else
            {
                // No login panel found: old fixed start pose.
                Vector3 targetCamPos = new Vector3(5.173f, 1.077f, -1.985f);
                const float targetYaw = 180.954f;
                MoveRigToPose(targetCamPos, targetYaw);
                yield return null;
                MoveRigToPose(targetCamPos, targetYaw);
            }
        }
    }

    // Where the camera should be (XZ) and which way it should look so the login panel is
    // straight ahead. Keeps the scene's own start spot when that is already in front of the
    // panel; otherwise stands 2.2 m in front of it.
    bool TryGetLoginViewPose(Vector3 sceneCamPos, out Vector3 camPos, out float yaw, out float panelHeight)
    {
        camPos = sceneCamPos;
        yaw = 0f;
        panelHeight = 1.3f;
        if (loginUIRoot == null)
            return false;

        Transform panel = loginUIRoot.transform;
        Canvas canvas = loginUIRoot.GetComponentInParent<Canvas>(true);
        if (canvas != null)
            panel = canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform;

        Vector3 panelPos = panel.position;
        panelHeight = panelPos.y;
        Vector3 normal = Vector3.ProjectOnPlane(panel.forward, Vector3.up);   // readable when looking along it
        if (normal.sqrMagnitude < 1e-6f)
            return false;
        normal.Normalize();

        Vector3 toPanel = Vector3.ProjectOnPlane(panelPos - sceneCamPos, Vector3.up);
        float distance = toPanel.magnitude;
        bool sceneSpotIsGood = distance > 1.0f && distance < 4.5f && Vector3.Angle(toPanel, normal) < 40f;
        if (!sceneSpotIsGood)
        {
            camPos = panelPos - normal * 2.2f;
            toPanel = normal;
        }

        // Step back (or forward) along the viewing direction.
        camPos -= toPanel.normalized * loginStartExtraDistance;

        yaw = Mathf.Atan2(toPanel.x, toPanel.z) * Mathf.Rad2Deg;
        return true;
    }

    void FaceLoginPanel(Camera cam, Vector3 camPos, float yawDeg, float sceneRigY, float panelHeight)
    {
        float yawDelta = Mathf.DeltaAngle(cam.transform.rotation.eulerAngles.y, yawDeg);
        xrRig.RotateAround(cam.transform.position, Vector3.up, yawDelta);

        Vector3 camOffset = cam.transform.position - xrRig.position;
        // With a headset the floor stays the floor. In the editor without a headset the camera
        // sits at the rig's feet, so lift it to the height of the panel.
        float rigY = camOffset.y > 0.5f ? sceneRigY : Mathf.Max(1.0f, panelHeight) - camOffset.y;
        xrRig.position = new Vector3(camPos.x - camOffset.x, rigY, camPos.z - camOffset.z);
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


