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

    [Header("Walk in: hospital entrance -> reception -> ward door")]
    [Tooltip("Empty object in the scene where the student starts (for example at the hospital entrance). Its blue arrow (Z) is the direction the student looks. The login panel appears in front of it. Not in the scene = the old start.")]
    [SerializeField] string startPointName = "Start_Point";
    [Tooltip("Empty object in front of the reception desk, blue arrow (Z) toward the desk. After logging in the student walks here; the introduction and the medical record are shown here. Not in the scene = shown right after login.")]
    [SerializeField] string receptionPointName = "Reception_Point";
    [Tooltip("Empty object in the middle of a computer screen, blue arrow (Z) pointing INTO the screen (away from the student). With it, the login and introduction panels are shown on that screen instead of floating in front of the eyes.")]
    [SerializeField] string loginScreenPointName = "Login_Screen_Point";
    [Tooltip("On: the screen glows and the student clicks it; the login panel then opens in front of the eyes (its normal look). Off: the login panel is shown on the screen itself and the student starts close to it.")]
    [SerializeField] bool clickScreenToOpenLogin = true;
    [Tooltip("The student starts right in front of the computer, so its screen looks big (the computer itself is not changed). Off = start exactly on Start_Point.")]
    [SerializeField] bool startCloseToScreen = true;
    [Tooltip("Eyes this far from the screen at the start (m). Smaller = the screen looks bigger.")]
    [SerializeField] float screenStandDistance = 0.95f;
    [SerializeField] string screenLoginLabel = "點選螢幕登入";
    [Tooltip("After the screen is clicked, the login and introduction panels are this far in front of the eyes (m), like the medical record. Closer when the computer is nearer than that.")]
    [SerializeField] float eyePanelDistance = 0.95f;
    [Tooltip("Width of those panels (m).")]
    [SerializeField] float eyePanelWidth = 0.9f;
    [Tooltip("Work out the screen size from the computer model the screen point is a child of. Off = use Login Screen Size.")]
    [SerializeField] bool autoScreenSize = true;
    [Tooltip("Width and height of the computer's screen (m). A dark page of this size covers the wallpaper and the panels are fitted inside it.")]
    [SerializeField] Vector2 screenPictureSize = new Vector2(0.585f, 0.329f);
    [Header("Built-in positions (used when the scene has no point objects)")]
    [Tooltip("Middle of the nurses' station computer screen, in world coordinates. Used when there is no object named Login_Screen_Point.")]
    [SerializeField] Vector3 builtInScreenCentre = new Vector3(7.65f, 1.086f, 0.745f);
    [Tooltip("Direction INTO the screen, as a turn around the vertical axis (degrees; 90 = +X).")]
    [SerializeField] float builtInScreenYaw = 90f;
    [Tooltip("Height of the floor at the nurses' station (world Y).")]
    [SerializeField] float builtInFloorY = -0.074f;
    [Tooltip("Where the small glowing record lies on the desk, in world coordinates. Used when there is no object named Record_Point.")]
    [SerializeField] Vector3 builtInRecordSpot = new Vector3(7.49f, 0.76f, 0.15f);
    [Tooltip("Name of the computer model at the nurses' station. The one nearest to Built In Screen Position is measured at start, so the page sits exactly on its screen. Empty = use the fixed position above.")]
    [SerializeField] string builtInComputerName = "iMac_Pro";
    [Tooltip("Middle of the picture measured from the computer model's pivot: Y = height above the base, Z = towards the viewer (negative = behind the pivot). In metres.")]
    [SerializeField] Vector3 computerScreenLocal = new Vector3(0f, 0.336f, -0.011f);
    [Tooltip("Width and height of the picture on the computer model (m).")]
    [SerializeField] Vector2 computerScreenSize = new Vector2(0.585f, 0.329f);
    [Tooltip("Off = ignore the built-in positions: without point objects the old behaviour is used.")]
    [SerializeField] bool useBuiltInPositions = true;
    [SerializeField] Color loginScreenPageColor = new Color(0.07f, 0.09f, 0.13f, 1f);
    [Tooltip("At the start the student's eyes are this far in front of the screen (m). Closer = the screen looks bigger.")]
    [SerializeField] float loginScreenViewDistance = 0.5f;
    [Tooltip("Distance from the student to the login / introduction panel (m).")]
    [SerializeField] float walkPanelDistance = 1.6f;
    [Tooltip("Width of the login / introduction panel on this route (m).")]
    [SerializeField] float walkPanelWidth = 1.3f;
    [Tooltip("The student has arrived when this close to the reception point (m, on the floor).")]
    [SerializeField] float receptionArriveRadius = 0.9f;
    [SerializeField] string receptionHintText = "請沿著地上的光點走到接待處，點選桌上發光的病歷";
    [Tooltip("Height of the hint text compared with the eyes (m).")]
    [SerializeField] float walkHintHeight = 0.09f;
    [Tooltip("Hint when the student already stands at the desk (start at the nurses' station).")]
    [SerializeField] string deskHintText = "請點選桌上發光的病歷";
    [Tooltip("Empty object ON the reception desk where the small glowing record lies. Not in the scene = guessed from the reception point and Desk Height.")]
    [SerializeField] string recordPointName = "Record_Point";
    [Tooltip("Height of the desk top above the floor (m); only used when there is no Record_Point.")]
    [SerializeField] float deskHeight = 1.12f;
    [Tooltip("Width and height of the small record on the desk (m).")]
    [SerializeField] Vector2 deskRecordSize = new Vector2(0.3f, 0.2f);
    [Tooltip("How high the small record floats above the desk top (m).")]
    [SerializeField] float deskRecordHeight = 0.19f;
    [Tooltip("After the record is closed, light spots on the floor lead to the ward door.")]
    [SerializeField] bool showDoorTrail = true;
    [SerializeField] string doorWayText = "請轉身離開護理站，跟著箭頭走到診間門口";
    [Tooltip("The light spots stop when the student is this close to the door guide point (m).")]
    [SerializeField] float doorArriveRadius = 0.8f;
    [Tooltip("Corners of the route to the ward door: empty objects named Door_Path_1, Door_Path_2, ... (optional; without them the spots go in a straight line).")]
    [SerializeField] string doorPathPrefix = "Door_Path_";
    [Tooltip("Corners of the route from the start to the reception: Reception_Path_1, Reception_Path_2, ... (optional).")]
    [SerializeField] string receptionPathPrefix = "Reception_Path_";
    [Tooltip("How far in front of the eyes the opened record is held while standing at the desk (m). Close, so the computer does not cut through it.")]
    [SerializeField] float deskRecordReadDistance = 0.5f;
    [Tooltip("How much the small record is tilted up toward the student (degrees; 0 = flat on the desk).")]
    [SerializeField] float deskRecordStandAngle = 62f;
    [Tooltip("Size of the arrow in front of the student that points where to walk (m).")]
    [SerializeField] float wayArrowSize = 0.2f;
    [Tooltip("How far below the eyes that arrow floats (m).")]
    [SerializeField] float wayArrowDrop = 0.42f;
    [Tooltip("Colour of the light spots on the floor.")]
    [SerializeField] Color trailColor = new Color(0.45f, 0.92f, 1f, 0.9f);
    [Tooltip("Distance between two light spots (m).")]
    [SerializeField] float trailDotSpacing = 0.55f;
    [Tooltip("Size of one light spot (m).")]
    [SerializeField] float trailDotSize = 0.2f;
    [Tooltip("Size of the ring on the floor at the reception (m).")]
    [SerializeField] float trailRingSize = 0.8f;

    bool startedAtStartPoint;
    float startFloorY;

    [Header("Login in front of the ward door")]
    [Tooltip("On: the player starts on the spot where the greeting is done (the door guide point), facing the door, and the login panel floats between the player and the door. After logging in nobody has to turn or walk: the door is straight ahead. Off: the login panel stays where it is in the scene.")]
    [SerializeField] bool startAtWardDoor = false;
    [Tooltip("Distance from the player to the login panel (m). Made shorter automatically when the door is closer than that.")]
    [SerializeField] float loginPanelDistance = 1.25f;
    [Tooltip("Height of the middle of the login panel compared with the eyes (m).")]
    [SerializeField] float loginPanelRaise = 0.06f;
    [Tooltip("The corridor is narrow: the login panel is made smaller so it is at most this wide (m) and never sticks into the walls. It is also never wider than its distance from the eyes.")]
    [SerializeField] float loginPanelMaxWidth = 0.95f;
    [Tooltip("Gap kept between the login panel and the door (m).")]
    [SerializeField] float loginPanelDoorGap = 0.12f;
    [Tooltip("The player starts this far behind the door guide point (m), to have a little more room. Keep it below the guide's arrive radius.")]
    [SerializeField] float standBackFromGuide = 0.25f;

    readonly System.Collections.Generic.Dictionary<Transform, Vector3> panelSceneScales =
        new System.Collections.Generic.Dictionary<Transform, Vector3>();

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

        // With a start point and a reception point in the scene, the student first walks to
        // the reception; the introduction (and then the medical record) is shown there.
        // No reception point = the record is picked up right where the student starts
        // (for example when the start is at the nurses' station).
        // Logging in at the nurses' station computer: the record is picked up right there.
        Transform reception = !startedAtStartPoint ? null
            : usingScreenLogin ? FindPoint(startPointName)
            : (FindPoint(receptionPointName) ?? FindPoint(startPointName));
        if (reception != null)
        {
            // This component sits on the login panel, which was just switched off, and an
            // inactive object cannot run a coroutine: run it on a small helper object instead.
            GameObject runnerObject = new GameObject("Login_WalkRunner");
            runnerObject.AddComponent<CoroutineRunner>().StartCoroutine(WalkToReception(reception, runnerObject));
        }
        else if (introPanelRoot != null)
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

            Transform startPoint = FindPoint(startPointName);
            if (startPoint != null)
            {
                startedAtStartPoint = true;
                startFloorY = startPoint.position.y;
                Vector3 walkFacing = FlatForward(startPoint);
                Vector3 standAt = startPoint.position;
                float floorY = startPoint.position.y;
                float editorEyeHeight = floorY + 1.5f;

                // A screen point in the scene: the student starts right in front of that
                // computer screen (so it looks big) and the login panel is on the screen.
                Transform screenPoint = ScreenPoint();
                if (screenPoint != null && screenPoint == builtInScreenPoint)
                {
                    floorY = builtInFloorY;
                    startFloorY = builtInFloorY;
                }
                bool clickScreen = screenPoint != null && clickScreenToOpenLogin;
                if (screenPoint != null && (!clickScreen || startCloseToScreen))
                {
                    Vector3 into = Vector3.ProjectOnPlane(screenPoint.forward, Vector3.up);
                    if (into.sqrMagnitude > 1e-4f)
                    {
                        walkFacing = into.normalized;
                        standAt = screenPoint.position - walkFacing * Mathf.Max(0.25f, clickScreen ? screenStandDistance : loginScreenViewDistance);
                        editorEyeHeight = screenPoint.position.y + 0.00f;
                    }
                }

                float walkYaw = Mathf.Atan2(walkFacing.x, walkFacing.z) * Mathf.Rad2Deg;
                FaceLoginPanel(cam, standAt, walkYaw, floorY, editorEyeHeight);
                yield return null;
                FaceLoginPanel(cam, standAt, walkYaw, floorY, editorEyeHeight);
                if (clickScreen)
                {
                    // The login panel stays hidden until the glowing screen is clicked.
                    PlaceCanvasInFront(cam, walkFacing);
                    SetLoginPanelShown(false);
                    if (!screenGateStarted)
                    {
                        screenGateStarted = true;
                        StartCoroutine(ScreenLoginGate(screenPoint));
                    }
                }
                else if (screenPoint != null)
                    PlaceCanvasOnScreen(screenPoint);
                else
                    PlaceCanvasInFront(cam, walkFacing);
                if (i == 0)
                    Debug.Log($"[LoginController] Start pose: at '{startPoint.name}' {FormatVec(startPoint.position)} yaw={walkYaw:0.#}; " +
                        (clickScreen
                            ? $"the screen '{loginScreenPointName}' {FormatVec(screenPoint.position)} glows; clicking it opens the login panel"
                            : screenPoint != null
                            ? $"in front of the screen '{loginScreenPointName}' {FormatVec(screenPoint.position)}, eyes {loginScreenViewDistance:0.##} m from it"
                            : $"no object named '{loginScreenPointName}' found: the login panel floats in front of the eyes"), this);
            }
            else if (startAtWardDoor && TryGetDoorPose(out Vector3 doorStand, out Vector3 doorFacing, out float doorDistance))
            {
                doorStand -= doorFacing * Mathf.Max(0f, standBackFromGuide);
                doorDistance += Mathf.Max(0f, standBackFromGuide);
                float doorYaw = Mathf.Atan2(doorFacing.x, doorFacing.z) * Mathf.Rad2Deg;
                Transform loginPanel = LoginPanelRoot(loginUIRoot);
                float startHeight = loginPanel != null ? loginPanel.position.y : 1.3f;
                FaceLoginPanel(cam, doorStand, doorYaw, sceneRigY, startHeight);
                yield return null;
                FaceLoginPanel(cam, doorStand, doorYaw, sceneRigY, startHeight);

                // The panel(s) between the player and the door, at eye height.
                float panelDistance = Mathf.Clamp(Mathf.Min(loginPanelDistance, doorDistance - loginPanelDoorGap), 0.55f, 3f);
                float panelWidth = Mathf.Min(loginPanelMaxWidth, panelDistance);
                Vector3 eye = cam.transform.position;
                Vector3 panelPosition = new Vector3(eye.x, eye.y + loginPanelRaise, eye.z) + doorFacing * panelDistance;
                Quaternion panelRotation = Quaternion.LookRotation(doorFacing, Vector3.up);
                // The canvas itself keeps its size (the medical record lives in it and sizes itself);
                // only the login and intro panels are made smaller.
                PlacePanel(loginPanel, panelPosition, panelRotation);
                Transform introPanel = LoginPanelRoot(introPanelRoot);
                if (introPanel != loginPanel)
                    PlacePanel(introPanel, panelPosition, panelRotation);
                FitPanel(loginUIRoot != null ? loginUIRoot.transform : null, panelWidth);
                FitPanel(introPanelRoot != null ? introPanelRoot.transform : null, panelWidth);
                if (i == 0)
                {
                    RectTransform panelRect = loginUIRoot != null ? loginUIRoot.transform as RectTransform : null;
                    string size = panelRect != null
                        ? $"{panelRect.rect.width * Mathf.Abs(panelRect.lossyScale.x):0.##} x {panelRect.rect.height * Mathf.Abs(panelRect.lossyScale.y):0.##} m"
                        : "(not a RectTransform)";
                    Debug.Log($"[LoginController] Start pose: at the ward door. stand={FormatVec(doorStand)} yaw={doorYaw:0.#} " +
                        $"doorDistance={doorDistance:0.##} panelDistance={panelDistance:0.##} maxWidth={panelWidth:0.##} " +
                        $"panel='{(loginPanel != null ? loginPanel.name : "none")}' size now={size} " +
                        $"introPanel='{(introPanel != null ? introPanel.name : "none")}'", this);
                }
            }
            else if (TryGetLoginViewPose(sceneCamPos, out Vector3 camPos, out float yaw, out float panelHeight))
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

    static Transform FindPoint(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;
        GameObject found = GameObject.Find(objectName.Trim());
        return found != null ? found.transform : null;
    }

    static Vector3 FlatForward(Transform t)
    {
        Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        return forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;
    }

    bool screenGateStarted;
    CanvasGroup loginPanelGroup;

    // Hidden but still active (this component lives on the login panel and must keep running).
    void SetLoginPanelShown(bool shown)
    {
        if (loginUIRoot == null)
            return;
        if (loginPanelGroup == null)
        {
            loginPanelGroup = loginUIRoot.GetComponent<CanvasGroup>();
            if (loginPanelGroup == null)
                loginPanelGroup = loginUIRoot.AddComponent<CanvasGroup>();
        }
        loginPanelGroup.alpha = shown ? 1f : 0f;
        loginPanelGroup.interactable = shown;
        loginPanelGroup.blocksRaycasts = shown;
    }

    // Mouse (editor) or controller ray + trigger: is the pointer on the target, and was it clicked?
    UnityEngine.XR.Interaction.Toolkit.XRRayInteractor[] pointerRays;
    float pointerNextScan;
    bool pointerTriggerWasDown;
    readonly System.Collections.Generic.List<UnityEngine.XR.InputDevice> pointerDevices =
        new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();

    bool PollPointer(Camera cam, System.Func<Ray, bool> hits, out bool hovered)
    {
        hovered = false;
        bool clicked = false;
#if ENABLE_INPUT_SYSTEM
        if (cam != null && UnityEngine.InputSystem.Mouse.current != null && !UnityEngine.XR.XRSettings.isDeviceActive)
        {
            hovered |= hits(cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue()));
            clicked |= UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (cam != null && !UnityEngine.XR.XRSettings.isDeviceActive)
        {
            hovered |= hits(cam.ScreenPointToRay(Input.mousePosition));
            clicked |= Input.GetMouseButtonDown(0);
        }
#endif
        if (pointerRays == null || Time.unscaledTime >= pointerNextScan)
        {
            pointerRays = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>();
            pointerNextScan = Time.unscaledTime + 2f;
        }
        foreach (UnityEngine.XR.Interaction.Toolkit.XRRayInteractor interactor in pointerRays)
        {
            if (interactor != null && interactor.isActiveAndEnabled)
                hovered |= hits(new Ray(interactor.transform.position, interactor.transform.forward));
        }

        bool triggerDown = false;
        pointerDevices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.HeldInHand, pointerDevices);
        foreach (UnityEngine.XR.InputDevice device in pointerDevices)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool pressed) && pressed)
                triggerDown = true;
        }
        if (triggerDown && !pointerTriggerWasDown)
            clicked = true;
        pointerTriggerWasDown = triggerDown;
        return clicked && hovered;
    }

    bool usingScreenLogin;
    Transform builtInScreenPoint;

    // The screen point placed in the scene, or the built-in one.
    Transform ScreenPoint()
    {
        Transform point = FindPoint(loginScreenPointName);
        if (point == null && useBuiltInPositions)
        {
            if (builtInScreenPoint == null)
            {
                builtInScreenPoint = new GameObject(loginScreenPointName + " (built-in)").transform;
                Vector3 centre = builtInScreenCentre;
                Vector3 into = Quaternion.Euler(0f, builtInScreenYaw, 0f) * Vector3.forward;
                if (!TryMeasureComputer(ref centre, ref into))
                    Debug.LogWarning($"[LoginController] v16 no computer named '{builtInComputerName}' near {FormatVec(builtInScreenCentre)}: using the fixed screen position", this);
                builtInScreenPoint.SetPositionAndRotation(centre, Quaternion.LookRotation(into, Vector3.up));
            }
            point = builtInScreenPoint;
        }
        usingScreenLogin = point != null;
        return point;
    }

    bool hasMeasuredScreen;
    Vector2 measuredScreenSize;
    Vector3 measuredRecordPosition;

    // Finds the real computer model and measures where its screen is, which way it faces and
    // how big it is, so nothing depends on hand-typed coordinates.
    bool TryMeasureComputer(ref Vector3 centre, ref Vector3 into)
    {
        if (string.IsNullOrEmpty(builtInComputerName))
            return false;
        Transform best = null;
        float bestDistance = 2f;
        foreach (Transform t in FindObjectsOfType<Transform>())
        {
            if (t.name != builtInComputerName)
                continue;
            float d = Vector3.Distance(t.position, builtInScreenCentre);
            if (d < bestDistance) { bestDistance = d; best = t; }
        }
        if (best == null)
            return false;

        // The model's own shape is known (static batching hides its mesh at run time):
        // the glass is 15 mm behind the pivot plane and faces the model's forward; the
        // picture is 0.585 x 0.329 m with its middle 0.336 m above the base.
        Vector3 facingOut = Vector3.ProjectOnPlane(best.forward, Vector3.up).normalized;
        if (facingOut.sqrMagnitude < 0.5f)
            return false;
        float scale = Mathf.Abs(best.lossyScale.x);
        Vector3 measuredInto = -facingOut;
        Vector3 screenCentre = best.position + Vector3.up * (computerScreenLocal.y * best.lossyScale.y) + facingOut * (computerScreenLocal.z * scale);

        centre = screenCentre;
        into = measuredInto;
        hasMeasuredScreen = true;
        measuredScreenSize = computerScreenSize * scale;
        Vector3 right = Quaternion.LookRotation(measuredInto, Vector3.up) * Vector3.right;
        measuredRecordPosition = screenCentre - measuredInto * 0.16f + right * 0.595f;
        measuredRecordPosition.y = best.position.y + 0.01f;
        Debug.Log($"[LoginController] v16 found '{best.name}' at {FormatVec(best.position)}: screen centre {FormatVec(screenCentre)}, " +
            $"into yaw {Mathf.Atan2(measuredInto.x, measuredInto.z) * Mathf.Rad2Deg:0.#}, picture {measuredScreenSize.x:0.###} x {measuredScreenSize.y:0.###} m", this);
        return true;
    }

    // Size of the screen, worked out from the computer model the screen point belongs to.
    Vector2 ScreenSize(Transform screenPoint)
    {
        if (hasMeasuredScreen && screenPoint == builtInScreenPoint)
            return measuredScreenSize;
        if (!autoScreenSize || screenPoint.parent == null)
            return screenPictureSize;
        bool has = false;
        Bounds bounds = new Bounds(screenPoint.position, Vector3.zero);
        foreach (Renderer r in screenPoint.parent.GetComponentsInChildren<Renderer>())
        {
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        float wide = Mathf.Max(bounds.size.x, bounds.size.z);
        if (!has || wide < 0.2f || wide > 1.5f)
            return screenPictureSize;
        return new Vector2(wide * 0.9f, wide * 0.9f * 9f / 16f);   // 16:9 picture inside the bezel
    }

    static TMP_FontAsset FindPromptFont()
    {
        foreach (Canvas other in FindObjectsOfType<Canvas>(true))
        {
            if (other == null || !other.name.Contains("Instruction_Canvas"))
                continue;
            TMP_Text sample = other.GetComponentInChildren<TMP_Text>(true);
            if (sample != null && sample.font != null)
                return sample.font;
        }
        return null;
    }

    static TextMeshProUGUI NewLabel(Transform parent, TMP_FontAsset font, string text, float size, Color color, Vector2 position, Vector2 box)
    {
        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)label.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        return tmp;
    }

    static UnityEngine.UI.Image NewPageImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        UnityEngine.UI.Image image = go.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    static Sprite SpriteFrom(Color32[] pixels, int width, int height, Vector4 border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    // Top colour to bottom colour.
    static Sprite MakeGradientSprite(Color top, Color bottom)
    {
        const int width = 4, height = 128;
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            Color32 c = Color.Lerp(bottom, top, y / (float)(height - 1));
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = c;
        }
        return SpriteFrom(pixels, width, height, Vector4.zero);
    }

    // inner = 0: a soft glow that fades out from the middle. inner > 0: a crisp ring whose hole
    // is that share of the radius.
    static Sprite MakeDiscSprite(float inner)
    {
        const int size = 128;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float alpha = inner <= 0f
                    ? Mathf.Pow(Mathf.Clamp01(1f - r), 2f)
                    : Mathf.Clamp01((1f - r) * size * 0.5f) * Mathf.Clamp01((r - inner) * size * 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        return SpriteFrom(pixels, size, size, Vector4.zero);
    }

    // Round portrait: pale disc with a simple head and shoulders.
    static Sprite MakePortraitSprite()
    {
        const int size = 128;
        Color32[] pixels = new Color32[size * size];
        Color disc = new Color(0.86f, 0.91f, 0.96f, 1f), figure = new Color(0.36f, 0.48f, 0.62f, 1f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                float edge = Mathf.Clamp01((0.5f - p.magnitude) * size);
                bool head = (p - new Vector2(0f, 0.10f)).magnitude < 0.17f;
                Vector2 s = p - new Vector2(0f, -0.42f);
                bool shoulders = (s.x * s.x) / (0.34f * 0.34f) + (s.y * s.y) / (0.30f * 0.30f) < 1f;
                Color c = head || shoulders ? figure : disc;
                c.a = edge;
                pixels[y * size + x] = c;
            }
        }
        return SpriteFrom(pixels, size, size, Vector4.zero);
    }

    // Pill shape that keeps its round ends at any width (sliced sprite).
    static Sprite MakePillSprite()
    {
        const int size = 64;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float alpha = Mathf.Clamp01(size * 0.5f - r);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        float b = size * 0.5f - 1f;
        return SpriteFrom(pixels, size, size, new Vector4(b, b, b, b));
    }

    // The computer itself shows a login page ("click the screen to log in"). Clicking it
    // opens the login panel in front of the eyes, like the medical record.
    IEnumerator ScreenLoginGate(Transform screenPoint)
    {
        Vector3 into = screenPoint.forward.sqrMagnitude > 1e-4f ? screenPoint.forward.normalized : Vector3.forward;
        Quaternion facing = Quaternion.LookRotation(into, screenPoint.up);
        Vector3 centre = screenPoint.position - into * 0.006f;
        Vector3 right = facing * Vector3.right, up = facing * Vector3.up;
        Vector2 size = ScreenSize(screenPoint);
        Debug.Log($"[LoginController] v16 screen page {size.x:0.##} x {size.y:0.##} m at {FormatVec(centre)}", this);

        GameObject page = new GameObject("Login_ScreenPage", typeof(RectTransform));
        Canvas pageCanvas = page.AddComponent<Canvas>();
        pageCanvas.renderMode = RenderMode.WorldSpace;
        pageCanvas.sortingOrder = 5;
        page.AddComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 6f;
        RectTransform pageRoot = (RectTransform)page.transform;
        const float pageWidth = 640f, pageHeight = 360f;
        pageRoot.sizeDelta = new Vector2(pageWidth, pageHeight);
        pageRoot.SetPositionAndRotation(centre, facing);
        pageRoot.localScale = new Vector3(size.x / pageWidth, size.y / pageHeight, size.x / pageWidth);

        TMP_FontAsset font = FindPromptFont();
        Color soft = new Color(0.82f, 0.88f, 0.95f, 0.85f);

        // Lock-screen look: soft gradient, a round portrait, the name, and one pill to press.
        UnityEngine.UI.Image backImage = NewPageImage(pageRoot, "Background", MakeGradientSprite(
            new Color(0.10f, 0.20f, 0.33f, 1f), new Color(0.04f, 0.07f, 0.13f, 1f)), Vector2.zero, pageRoot.sizeDelta);
        UnityEngine.UI.Image glow = NewPageImage(pageRoot, "Glow", MakeDiscSprite(0f), new Vector2(0f, 40f), new Vector2(520f, 520f));
        glow.color = new Color(trailColor.r, trailColor.g, trailColor.b, 0.10f);

        TextMeshProUGUI brand = NewLabel(pageRoot, font, "FJUN　護理資訊系統", 19f, soft, new Vector2(-140f, 154f), new Vector2(320f, 28f));
        brand.alignment = TextAlignmentOptions.Left;
        TextMeshProUGUI clock = NewLabel(pageRoot, font, "", 19f, soft, new Vector2(200f, 154f), new Vector2(200f, 28f));
        clock.alignment = TextAlignmentOptions.Right;

        UnityEngine.UI.Image ring = NewPageImage(pageRoot, "Ring", MakeDiscSprite(0.86f), new Vector2(0f, 62f), new Vector2(150f, 150f));
        NewPageImage(pageRoot, "Portrait", MakePortraitSprite(), new Vector2(0f, 62f), new Vector2(124f, 124f));
        NewLabel(pageRoot, font, "護理師", 34f, Color.white, new Vector2(0f, -36f), new Vector2(400f, 44f));

        UnityEngine.UI.Image buttonImage = NewPageImage(pageRoot, "Pill", MakePillSprite(), new Vector2(0f, -112f), new Vector2(380f, 70f));
        buttonImage.type = UnityEngine.UI.Image.Type.Sliced;
        RectTransform buttonRect = buttonImage.rectTransform;
        NewLabel(buttonRect, font, screenLoginLabel, 32f, Color.white, Vector2.zero, new Vector2(370f, 60f));

        float grow = 1f;
        while (true)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector2 half = size * 0.5f + new Vector2(0.03f, 0.03f);
                bool clicked = PollPointer(cam, ray =>
                {
                    float denominator = Vector3.Dot(ray.direction, into);
                    if (Mathf.Abs(denominator) < 1e-4f)
                        return false;
                    float t = Vector3.Dot(centre - ray.origin, into) / denominator;
                    if (t <= 0f || t > 6f)
                        return false;
                    Vector3 offset = ray.origin + ray.direction * t - centre;
                    return Mathf.Abs(Vector3.Dot(offset, right)) <= half.x && Mathf.Abs(Vector3.Dot(offset, up)) <= half.y;
                }, out bool hovered);

                // The pill and the ring around the portrait breathe; pointing at the screen lights them up.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.6f);
                clock.text = System.DateTime.Now.ToString("HH:mm");
                backImage.color = hovered ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f);
                Color pill = Color.Lerp(new Color(1f, 1f, 1f, 0.16f), trailColor, hovered ? 0.85f : 0.25f * pulse);
                pill.a = hovered ? 0.95f : 0.22f + 0.2f * pulse;
                buttonImage.color = pill;
                Color ringColor = trailColor;
                ringColor.a = hovered ? 1f : 0.35f + 0.55f * pulse;
                ring.color = ringColor;
                ring.rectTransform.localScale = Vector3.one * (1f + 0.04f * pulse);
                grow = Mathf.Lerp(grow, hovered ? 1.07f : 1f, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                buttonRect.localScale = Vector3.one * grow;

                if (clicked)
                {
                    Destroy(page);
                    OpenPanelsInFrontOfEyes(cam, centre);
                    SetLoginPanelShown(true);
                    yield break;
                }
            }
            yield return null;
        }
    }

    // The login and introduction panels behave like the medical record from here on: solid
    // background (readable), in front of the eyes, and they come back in front when the
    // student turns away.
    void OpenPanelsInFrontOfEyes(Camera cam, Vector3 screenCentre)
    {
        UnityEngine.UI.Image loginBack = loginUIRoot != null ? loginUIRoot.GetComponent<UnityEngine.UI.Image>() : null;
        if (loginBack != null)
        {
            Color c = loginBack.color;
            c.a = 0.97f;
            loginBack.color = c;
        }
        UnityEngine.UI.Image introBack = introPanelRoot != null ? introPanelRoot.GetComponent<UnityEngine.UI.Image>() : null;
        if (introBack != null)
        {
            Color c = introBack.color;
            c.a = 1f;
            introBack.color = c;
        }

        // Between the eyes and the screen, never behind the computer.
        Vector3 toScreen = Vector3.ProjectOnPlane(screenCentre - cam.transform.position, Vector3.up);
        float room = toScreen.magnitude;
        float distance = Mathf.Clamp(room - 0.12f, 0.5f, eyePanelDistance);
        Vector3 direction = room > 0.05f ? toScreen / room : FlatForward(cam.transform);
        float width = Mathf.Min(eyePanelWidth, distance * 1.15f);
        // On the line from the eyes to the screen, so it covers the computer instead of floating above it.
        float drop = room > 0.05f ? (screenCentre.y - cam.transform.position.y) * (distance / room) : loginPanelRaise;
        PlaceCanvasInFront(cam, direction, distance, width, drop);

        GameObject followerObject = new GameObject("Login_PanelFollower");
        PanelFollower follower = followerObject.AddComponent<PanelFollower>();
        follower.owner = this;
        follower.distance = distance;
        follower.width = width;
        follower.heightOffset = drop;
        Debug.Log($"[LoginController] Screen clicked: login panel opened {distance:0.##} m in front of the eyes, {width:0.##} m wide (screen is {room:0.##} m away).", this);
    }

    sealed class PanelFollower : MonoBehaviour
    {
        public LoginController owner;
        public float distance, width, heightOffset;
        bool following;

        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (owner == null || cam == null)
                return;
            bool loginShown = owner.loginUIRoot != null && owner.loginUIRoot.activeInHierarchy;
            bool introShown = owner.introPanelRoot != null && owner.introPanelRoot.activeInHierarchy;
            if (!loginShown && !introShown)
                return;
            Transform canvas = LoginPanelRoot(loginShown ? owner.loginUIRoot : owner.introPanelRoot);
            if (canvas == null)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                return;
            forward.Normalize();
            Vector3 eye = cam.transform.position;
            Vector3 wanted = new Vector3(eye.x, eye.y + heightOffset, eye.z) + forward * distance;
            Vector3 toCanvas = Vector3.ProjectOnPlane(canvas.position - eye, Vector3.up);
            if (Vector3.Angle(forward, toCanvas) > 35f || Mathf.Abs(toCanvas.magnitude - distance) > 0.3f ||
                Mathf.Abs(canvas.position.y - wanted.y) > 0.25f)
                following = true;
            if (!following)
                return;

            float k = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
            canvas.position = Vector3.Lerp(canvas.position, wanted, k);
            canvas.rotation = Quaternion.Slerp(canvas.rotation, Quaternion.LookRotation(forward, Vector3.up), k);
            if (Vector3.Distance(canvas.position, wanted) < 0.02f)
                following = false;
        }
    }

    // The login / introduction panels shown on a computer screen in the scene: the canvas sits
    // on the screen point (blue arrow pointing INTO the screen). A dark page covers the whole
    // screen (so the wallpaper does not show through) and the panels are shrunk to fit on it.
    void PlaceCanvasOnScreen(Transform screenPoint)
    {
        Vector3 into = screenPoint.forward.sqrMagnitude > 1e-4f ? screenPoint.forward.normalized : Vector3.forward;
        Vector3 position = screenPoint.position - into * 0.004f;   // just in front of the glass
        Quaternion rotation = Quaternion.LookRotation(into, screenPoint.up);
        Transform loginPanel = LoginPanelRoot(loginUIRoot);
        PlacePanel(loginPanel, position, rotation);
        Transform introPanel = LoginPanelRoot(introPanelRoot);
        if (introPanel != loginPanel)
            PlacePanel(introPanel, position, rotation);
        float fitWidth = screenPictureSize.x * 0.98f, fitHeight = screenPictureSize.y * 0.94f;
        FitPanel(loginUIRoot != null ? loginUIRoot.transform : null, fitWidth, fitHeight);
        FitPanel(introPanelRoot != null ? introPanelRoot.transform : null, fitWidth, fitHeight);

        if (screenBackdrop == null && loginPanel != null)
        {
            // The page shown by the computer: behind the panels, exactly as big as the screen.
            GameObject page = new GameObject("Login_ScreenPage", typeof(RectTransform));
            page.transform.SetParent(loginPanel, false);
            page.transform.SetAsFirstSibling();
            UnityEngine.UI.Image pageImage = page.AddComponent<UnityEngine.UI.Image>();
            pageImage.color = loginScreenPageColor;
            pageImage.raycastTarget = false;
            screenBackdrop = page.AddComponent<ScreenPage>();
            screenBackdrop.image = pageImage;
            screenBackdrop.panels = new[] { loginUIRoot, introPanelRoot };

            // The panels' own see-through backgrounds would look like a second, smaller window.
            UnityEngine.UI.Image loginBack = loginUIRoot != null ? loginUIRoot.GetComponent<UnityEngine.UI.Image>() : null;
            if (loginBack != null)
            {
                Color c = loginBack.color;
                c.a = 0f;
                loginBack.color = c;
            }
            UnityEngine.UI.Image introBack = introPanelRoot != null ? introPanelRoot.GetComponent<UnityEngine.UI.Image>() : null;
            if (introBack != null)
            {
                Color c = introBack.color;
                c.a = 1f;
                introBack.color = c;
            }
        }
        if (screenBackdrop != null)
        {
            RectTransform pageRect = (RectTransform)screenBackdrop.transform;
            Vector3 canvasScale = loginPanel.lossyScale;
            pageRect.anchoredPosition = Vector2.zero;
            pageRect.sizeDelta = new Vector2(
                screenPictureSize.x / Mathf.Max(1e-5f, Mathf.Abs(canvasScale.x)),
                screenPictureSize.y / Mathf.Max(1e-5f, Mathf.Abs(canvasScale.y)));
        }
    }
    ScreenPage screenBackdrop;

    // Shows the dark page only while the login or the introduction panel is on the screen.
    sealed class ScreenPage : MonoBehaviour
    {
        public UnityEngine.UI.Image image;
        public GameObject[] panels;

        void LateUpdate()
        {
            bool any = false;
            if (panels != null)
            {
                foreach (GameObject panel in panels)
                {
                    if (panel != null && panel.activeInHierarchy)
                        any = true;
                }
            }
            if (image != null && image.enabled != any)
                image.enabled = any;
        }
    }

    // The canvas with the login / introduction panels: in front of the eyes, looking along "facing".
    void PlaceCanvasInFront(Camera cam, Vector3 facing, float distance = -1f, float width = -1f, float heightOffset = float.NaN)
    {
        if (float.IsNaN(heightOffset)) heightOffset = loginPanelRaise;
        if (distance <= 0f) distance = Mathf.Max(0.5f, walkPanelDistance);
        if (width <= 0f) width = walkPanelWidth;
        Vector3 eye = cam.transform.position;
        Vector3 position = new Vector3(eye.x, eye.y + heightOffset, eye.z) + facing * distance;
        Quaternion rotation = Quaternion.LookRotation(facing, Vector3.up);
        Transform loginPanel = LoginPanelRoot(loginUIRoot);
        PlacePanel(loginPanel, position, rotation);
        Transform introPanel = LoginPanelRoot(introPanelRoot);
        if (introPanel != loginPanel)
            PlacePanel(introPanel, position, rotation);
        FitPanel(loginUIRoot != null ? loginUIRoot.transform : null, width);
        FitPanel(introPanelRoot != null ? introPanelRoot.transform : null, width);
    }

    // After login: a glowing point at the reception and a hint in front of the eyes. When the
    // student gets there, the introduction panel opens in front of the reception desk.
    sealed class CoroutineRunner : MonoBehaviour { }

    IEnumerator WalkToReception(Transform reception, GameObject runnerObject)
    {
        // Step 1, still at the start: the introduction panel (opened by the login button) is
        // read where the student stands. Its button would open the medical record right
        // there; that is held back until the student picks the record up at the reception.
        MedicalRecordPanelFollower recordFollower = FindObjectOfType<MedicalRecordPanelFollower>(true);
        GameObject record = recordFollower != null ? recordFollower.gameObject : null;
        if (record != null)
        {
            if (introPanelRoot != null && !introPanelRoot.activeSelf)
                introPanelRoot.SetActive(true);
            while (!record.activeSelf)
                yield return null;
            record.SetActive(false);
            Debug.Log("[LoginController] Introduction closed: the medical record waits at the reception.", this);
        }
        else if (introPanelRoot != null)
        {
            // No medical record panel found: old order (introduction after the walk).
            yield return null;
            introPanelRoot.SetActive(false);
        }

        // Step 2: light spots on the floor to the reception, and a small glowing record on the desk.
        Vector3 target = reception.position;
        // No separate reception point: the record is picked up where the student stands now.
        if ((usingScreenLogin || FindPoint(receptionPointName) == null) && Camera.main != null)
            target = Camera.main.transform.position;
        target.y = startFloorY;   // the reception point may have been placed above the floor
        Camera startCam = Camera.main;
        var route = new System.Collections.Generic.List<Vector3>();
        route.Add(startCam != null ? startCam.transform.position : target);
        AddPathPoints(route, receptionPathPrefix);
        route.Add(target);
        FloorTrail trail = new FloorTrail(this, route, startFloorY);
        GameObject marker = trail.Root;
        // Already standing at the desk: no walk, just "pick up the record".
        bool alreadyThere = Vector3.ProjectOnPlane(target - route[0], Vector3.up).magnitude < 1.5f;
        GameObject hint = CreateWalkHint(alreadyThere ? deskHintText : receptionHintText, out RectTransform hintRoot);

        Transform card = null, cardGlow = null;
        Vector3 cardRest = Vector3.zero;
        if (record != null)
        {
            Transform recordPoint = FindPoint(recordPointName);
            if (recordPoint == null && useBuiltInPositions && usingScreenLogin)
            {
                recordPoint = new GameObject(recordPointName + " (built-in)").transform;
                recordPoint.position = hasMeasuredScreen ? measuredRecordPosition : builtInRecordSpot;
            }
            string where;
            if (recordPoint != null)
            {
                cardRest = recordPoint.position;
                where = "at '" + recordPointName + "'";
            }
            else if (TryFindDeskTop(target, out cardRest, out string deskName))
            {
                where = "on '" + deskName + "' (found automatically; add an object named '" + recordPointName + "' on the desk for an exact spot)";
            }
            else
            {
                cardRest = target + FlatForward(reception) * 0.55f + Vector3.up * deskHeight;
                where = "guessed (no desk found; add an object named '" + recordPointName + "' on the desk)";
            }
            card = CreateDeskRecord(marker.transform, record, cardRest, FlatForward(reception), out cardGlow);
            Debug.Log($"[LoginController] Walk to the reception: {FormatVec(target)}; record at {FormatVec(cardRest)} {where}", this);
        }
        else
        {
            Debug.Log($"[LoginController] Walk to the reception: {FormatVec(target)}", this);
        }

        UnityEngine.XR.Interaction.Toolkit.XRRayInteractor[] rays = null;
        float nextRayScan = 0f;
        bool triggerWasDown = false;
        var xrDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
        float cardScale = 1f;

        bool done = false;
        while (!done)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                yield return null;
                continue;
            }
            Transform head = cam.transform;

            FollowHint(hintRoot, head);
            trail.Tick(head.position);
            trail.PointTheWay(head);

            bool arrived = Vector3.ProjectOnPlane(target - head.position, Vector3.up).magnitude <= Mathf.Max(0.2f, receptionArriveRadius);

            if (card == null)
            {
                done = arrived;
            }
            else
            {
                // The record on the desk floats a little and glows; pointing at it makes it
                // bigger, and the trigger (or a mouse click) picks it up.
                bool hovered = false;
                bool clicked = false;
                float reach = Mathf.Max(deskRecordSize.x, deskRecordSize.y) * 0.75f;
                Vector3 cardCentre = card.position;

                bool Hits(Ray ray)
                {
                    Vector3 toCard = cardCentre - ray.origin;
                    float t = Vector3.Dot(toCard, ray.direction);
                    return t > 0f && t < 6f && (ray.origin + ray.direction * t - cardCentre).magnitude <= reach;
                }

#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null && !UnityEngine.XR.XRSettings.isDeviceActive)
                {
                    hovered |= Hits(cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue()));
                    clicked |= UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (!UnityEngine.XR.XRSettings.isDeviceActive)
                {
                    hovered |= Hits(cam.ScreenPointToRay(Input.mousePosition));
                    clicked |= Input.GetMouseButtonDown(0);
                }
#endif
                if (rays == null || Time.unscaledTime >= nextRayScan)
                {
                    rays = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>();
                    nextRayScan = Time.unscaledTime + 2f;
                }
                foreach (UnityEngine.XR.Interaction.Toolkit.XRRayInteractor interactor in rays)
                {
                    if (interactor != null && interactor.isActiveAndEnabled)
                        hovered |= Hits(new Ray(interactor.transform.position, interactor.transform.forward));
                }

                bool triggerDown = false;
                xrDevices.Clear();
                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                    UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.HeldInHand, xrDevices);
                foreach (UnityEngine.XR.InputDevice device in xrDevices)
                {
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool pressed) && pressed)
                        triggerDown = true;
                }
                if (triggerDown && !triggerWasDown)
                    clicked = true;
                triggerWasDown = triggerDown;

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
                cardScale = Mathf.Lerp(cardScale, hovered ? 1.25f : 1f, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                card.position = cardRest + Vector3.up * (deskRecordHeight + 0.012f * Mathf.Sin(Time.unscaledTime * 2f));
                // Always readable: its top edge points away from the student, its face is tilted toward them.
                Vector3 awayFromStudent = Vector3.ProjectOnPlane(cardRest - head.position, Vector3.up);
                if (awayFromStudent.sqrMagnitude > 0.01f)
                {
                    Quaternion facing = Quaternion.LookRotation(awayFromStudent.normalized, Vector3.up) *
                        Quaternion.Euler(90f - Mathf.Clamp(deskRecordStandAngle, 0f, 80f), 0f, 0f);
                    card.rotation = Quaternion.Slerp(card.rotation, facing, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
                }
                card.localScale = new Vector3(deskRecordSize.x * cardScale, deskRecordSize.y * cardScale, 1f);
                if (cardGlow != null)
                {
                    float glow = Mathf.Max(deskRecordSize.x, deskRecordSize.y) * (2.2f + 0.5f * pulse);
                    cardGlow.localScale = new Vector3(glow, glow, 1f);
                    Renderer glowRenderer = cardGlow.GetComponent<Renderer>();
                    Color g = trailColor;
                    g.a = trailColor.a * (hovered ? 1f : 0.45f + 0.4f * pulse);
                    glowRenderer.material.color = g;
                }

                // Must be at the reception (or close enough to it) to pick the record up.
                bool closeEnough = arrived || Vector3.ProjectOnPlane(cardRest - head.position, Vector3.up).magnitude <= 2.2f;
                if (clicked && hovered && closeEnough)
                    done = true;
            }

            if (!done)
                yield return null;
        }

        if (marker != null) Destroy(marker);
        if (hint != null) Destroy(hint);

        if (record != null)
        {
            // The record places itself in front of the eyes and keeps following the head. At the
            // desk it is held closer, so the computer and the counter do not cut through it.
            if (recordFollower != null)
                recordFollower.SetCloseUp(deskRecordReadDistance);
            record.SetActive(true);
            Debug.Log("[LoginController] Record picked up at the reception: medical record shown.", this);

            // Step 4: once the record is closed, light spots lead to the ward door.
            while (record.activeSelf)
                yield return null;
            if (recordFollower != null)
                recordFollower.SetCloseUp(0f);
            PediatricVitalSignsPart0Flow part0 = FindObjectOfType<PediatricVitalSignsPart0Flow>(true);
            Transform doorGuide = part0 != null ? part0.DoorGuidePoint : null;
            if (doorGuide == null)
                doorGuide = entranceSpawn;
            Camera doorCam = Camera.main;
            if (showDoorTrail && doorGuide != null && doorCam != null)
            {
                var doorRoute = new System.Collections.Generic.List<Vector3>();
                doorRoute.Add(doorCam.transform.position);
                AddPathPoints(doorRoute, doorPathPrefix);
                doorRoute.Add(doorGuide.position);
                FloorTrail doorTrail = new FloorTrail(this, doorRoute, startFloorY);
                GameObject doorHint = CreateWalkHint(doorWayText, out RectTransform doorHintRoot);
                Debug.Log($"[LoginController] Walk to the ward door: {FormatVec(doorGuide.position)} ({doorRoute.Count - 2} path point(s) named '{doorPathPrefix}1', '{doorPathPrefix}2', ...)", this);

                while (true)
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        FollowHint(doorHintRoot, cam.transform);
                        doorTrail.Tick(cam.transform.position);
                        doorTrail.PointTheWay(cam.transform);
                        // Standing on the point outside the door: arrow, spots and hint all go away.
                        // The door flow itself decides when the student has arrived (it then starts
                        // listening for the greeting). Ending earlier would leave the student
                        // standing just short of the point with no guide and nobody listening.
                        bool arrivedAtDoor = part0 != null && part0.DoorGuidePoint != null
                            ? part0.PlayerArrivedAtDoor
                            : Vector3.ProjectOnPlane(doorGuide.position - cam.transform.position, Vector3.up).magnitude <= doorArriveRadius;
                        if (arrivedAtDoor)
                            break;
                    }
                    yield return null;
                }
                doorTrail.Destroy();
                if (doorHint != null) Destroy(doorHint);
            }
        }
        else
        {
            Camera arrivedCam = Camera.main;
            if (arrivedCam != null)
                PlaceCanvasInFront(arrivedCam, FlatForward(arrivedCam.transform));
            if (introPanelRoot != null)
                introPanelRoot.SetActive(true);
            Debug.Log("[LoginController] Arrived at the reception: introduction shown.", this);
        }
        if (runnerObject != null)
            Destroy(runnerObject);
    }

    // The top of the reception desk next to the reception point: the nearest big piece of
    // furniture whose top is at desk height. Returns a spot on it, a little in from its edge.
    bool TryFindDeskTop(Vector3 floorPoint, out Vector3 spot, out string deskName)
    {
        spot = Vector3.zero;
        deskName = null;
        float best = float.MaxValue;
        foreach (MeshRenderer r in FindObjectsOfType<MeshRenderer>())
        {
            if (r == null || !r.enabled)
                continue;
            Bounds b = r.bounds;
            float top = b.max.y - floorPoint.y;
            if (top < 0.75f || top > 1.35f)
                continue;                                   // not at desk height
            if (Mathf.Max(b.size.x, b.size.z) < 0.9f || Mathf.Min(b.size.x, b.size.z) < 0.3f || b.size.y < 0.4f)
                continue;                                   // too small to be the counter
            if (Mathf.Max(b.size.x, b.size.z) > 8f)
                continue;                                   // a wall or the whole floor
            Vector3 probe = new Vector3(floorPoint.x, b.center.y, floorPoint.z);
            Vector3 closest = b.ClosestPoint(probe);
            float distance = Vector3.ProjectOnPlane(closest - probe, Vector3.up).magnitude;
            if (distance > 2.5f || distance >= best)
                continue;

            best = distance;
            Vector3 inward = Vector3.ProjectOnPlane(b.center - closest, Vector3.up);
            float step = Mathf.Min(0.28f, inward.magnitude);
            Vector3 onTop = closest + (inward.sqrMagnitude > 1e-4f ? inward.normalized * step : Vector3.zero);
            spot = new Vector3(onTop.x, b.max.y, onTop.z);
            deskName = r.name;
        }
        return deskName != null;
    }

    // A small copy of the medical record lying on the desk, tilted toward the student, with
    // a soft glow under it.
    Transform CreateDeskRecord(Transform parent, GameObject record, Vector3 position, Vector3 away, out Transform glow)
    {
        Shader shader = Shader.Find("Sprites/Default");
        Quaternion lying = Quaternion.LookRotation(away, Vector3.up) * Quaternion.Euler(90f - Mathf.Clamp(deskRecordStandAngle, 0f, 80f), 0f, 0f);

        Renderer glowRenderer = NewFloorQuad(parent, position - Vector3.up * 0.015f, 0.4f, shader, MakeGlowTexture(false));
        glow = glowRenderer.transform;

        // Picture of the record: the biggest image of the real record panel.
        Texture picture = null;
        float biggest = 0f;
        foreach (UnityEngine.UI.Image image in record.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (image == null || image.sprite == null || image.sprite.texture == null)
                continue;
            Rect r = image.rectTransform.rect;
            float area = Mathf.Abs(r.width * r.height);
            if (area > biggest)
            {
                biggest = area;
                picture = image.sprite.texture;
            }
        }

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "DeskRecord";
        Collider quadCollider = quad.GetComponent<Collider>();
        if (quadCollider != null) Destroy(quadCollider);
        quad.transform.SetParent(parent, false);
        quad.transform.position = position + Vector3.up * deskRecordHeight;
        quad.transform.rotation = lying;
        quad.transform.localScale = new Vector3(deskRecordSize.x, deskRecordSize.y, 1f);
        Renderer quadRenderer = quad.GetComponent<Renderer>();
        if (shader != null)
            quadRenderer.material = new Material(shader);
        if (picture != null)
            quadRenderer.material.mainTexture = picture;
        quadRenderer.material.color = Color.white;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        return quad.transform;
    }

    static float HintDistance => HudDepth.Enabled ? HudDepth.Distance : 0.7f;

    void FollowHint(RectTransform hintRoot, Transform head)
    {
        if (hintRoot == null || head == null)
            return;
        Vector3 ahead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (ahead.sqrMagnitude < 1e-4f) ahead = head.up;
        ahead.Normalize();
        // Close and tied to where the student looks (also when looking down at the desk), so
        // it is never hidden behind the counter or out of view.
        Vector3 wanted = head.position + head.forward * HintDistance + head.up * (HintDistance * 0.15f);
        float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
        hintRoot.position = Vector3.Lerp(hintRoot.position, wanted, k);
        hintRoot.rotation = Quaternion.Slerp(hintRoot.rotation, Quaternion.LookRotation(head.forward, Vector3.up), k);
    }

    // Optional corners of a route: empty objects named <prefix>1, <prefix>2, ... (up to 9).
    static void AddPathPoints(System.Collections.Generic.List<Vector3> route, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return;
        for (int n = 1; n <= 9; n++)
        {
            Transform point = FindPoint(prefix.Trim() + n);
            if (point != null)
                route.Add(point.position);
        }
    }

    // Light spots on the floor along a route (start, optional corners, target) and a ring on
    // the target. Flat on the floor, lit from within (not affected by the room lighting).
    // The spots light up one after the other toward the target; the ones walked past go out.
    sealed class FloorTrail
    {
        public readonly GameObject Root;
        readonly LoginController owner;
        readonly Vector3[] points;
        readonly float[] startAt;          // distance along the route where each stretch starts
        readonly float total;
        readonly System.Collections.Generic.List<Renderer> dots = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<float> dotAt = new System.Collections.Generic.List<float>();
        readonly Transform ring;

        public FloorTrail(LoginController owner, System.Collections.Generic.List<Vector3> route, float floorY)
        {
            this.owner = owner;
            Root = new GameObject("FloorTrail");
            points = route.ToArray();
            for (int i = 0; i < points.Length; i++)
                points[i].y = floorY;
            startAt = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
                startAt[i] = startAt[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            total = points.Length > 0 ? startAt[points.Length - 1] : 0f;

            Shader shader = Shader.Find("Sprites/Default");
            Texture2D dotTexture = MakeGlowTexture(false);
            float spacing = Mathf.Max(0.25f, owner.trailDotSpacing);
            for (float d = 0.8f; d < total - 0.55f; d += spacing)
            {
                dots.Add(owner.NewFloorQuad(Root.transform, PointAt(d), owner.trailDotSize, shader, dotTexture));
                dotAt.Add(d);
            }
            if (points.Length > 0 && total >= 1.5f)   // no ring under the student's own feet
                ring = owner.NewFloorQuad(Root.transform, points[points.Length - 1], owner.trailRingSize, shader, MakeGlowTexture(true)).transform;
        }

        Vector3 PointAt(float distance)
        {
            for (int i = 1; i < points.Length; i++)
            {
                if (distance <= startAt[i] || i == points.Length - 1)
                {
                    float length = startAt[i] - startAt[i - 1];
                    return Vector3.Lerp(points[i - 1], points[i], length > 0.001f ? (distance - startAt[i - 1]) / length : 0f);
                }
            }
            return points.Length > 0 ? points[0] : Vector3.zero;
        }

        // How far along the route the student is (on the stretch they are closest to).
        float Walked(Vector3 head)
        {
            float best = float.MaxValue, walked = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = points[i - 1], b = points[i];
                Vector3 ab = b - a;
                float length = ab.magnitude;
                if (length < 0.001f)
                    continue;
                Vector3 direction = ab / length;
                float t = Mathf.Clamp(Vector3.Dot(Vector3.ProjectOnPlane(head - a, Vector3.up), direction), 0f, length);
                float off = Vector3.ProjectOnPlane(head - (a + direction * t), Vector3.up).magnitude;
                if (off < best)
                {
                    best = off;
                    walked = startAt[i - 1] + t;
                }
            }
            return walked;
        }

        // Arrow at chest height in front of the student, turned toward the way to walk:
        // the spots on the floor cannot be seen from behind a desk.
        Transform arrow;
        Renderer arrowRenderer;

        public void PointTheWay(Transform head)
        {
            if (total < 1.5f || head == null)
                return;
            if (arrow == null)
            {
                arrowRenderer = owner.NewFloorQuad(Root.transform, head.position, owner.wayArrowSize, Shader.Find("Sprites/Default"), MakeArrowTexture());
                arrow = arrowRenderer.transform;
            }
            Vector3 ahead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (ahead.sqrMagnitude < 1e-4f)
                return;
            ahead.Normalize();
            Vector3 next = PointAt(Mathf.Min(total, Walked(head.position) + 2f));
            Vector3 way = Vector3.ProjectOnPlane(next - head.position, Vector3.up);
            if (way.sqrMagnitude < 0.04f)
            {
                arrowRenderer.enabled = false;
                return;
            }
            arrowRenderer.enabled = true;
            way.Normalize();
            float slide = 0.04f * Mathf.Sin(Time.unscaledTime * 4f);
            // Same distance as the rest of the display, same place and size in the view.
            float depth = HintDistance / 0.7f;
            arrow.localScale = new Vector3(owner.wayArrowSize * depth, owner.wayArrowSize * depth, 1f);
            Vector3 wanted = head.position + (head.forward * 0.7f - head.up * 0.2f) * depth + way * slide * depth;
            float k = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            arrow.position = Vector3.Lerp(arrow.position, wanted, k);
            // Lying flat; the top of the picture (the arrow's tip) points along "way".
            // Tipped up towards the student so it reads clearly: tip up = ahead, tip down = behind you.
            Quaternion tilt = Quaternion.AngleAxis(-55f, Vector3.Cross(Vector3.up, ahead));
            arrow.rotation = Quaternion.Slerp(arrow.rotation, tilt * Quaternion.LookRotation(Vector3.down, way), k);
            Color c = owner.trailColor;
            c.a = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            arrowRenderer.material.color = c;
        }

        public void Tick(Vector3 head)
        {
            float walked = Walked(head);
            for (int d = 0; d < dots.Count; d++)
            {
                Renderer dot = dots[d];
                if (dot == null) continue;
                bool stillAhead = dotAt[d] > walked + 0.35f;
                if (dot.enabled != stillAhead) dot.enabled = stillAhead;
                if (!stillAhead) continue;
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f - d * 0.75f);
                Color c = owner.trailColor;
                c.a = owner.trailColor.a * (0.3f + 0.7f * wave * wave);
                dot.material.color = c;
            }
            if (ring != null)
            {
                float size = owner.trailRingSize * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 2.4f));
                ring.localScale = new Vector3(size, size, 1f);
            }
        }

        public void Destroy()
        {
            if (Root != null)
                UnityEngine.Object.Destroy(Root);
        }
    }

    Renderer NewFloorQuad(Transform parent, Vector3 floorPosition, float size, Shader shader, Texture2D texture)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Spot";
        Collider quadCollider = quad.GetComponent<Collider>();
        if (quadCollider != null) Destroy(quadCollider);
        quad.transform.SetParent(parent, false);
        quad.transform.position = floorPosition + Vector3.up * 0.02f;
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lying on the floor, facing up
        quad.transform.localScale = new Vector3(size, size, 1f);
        Renderer quadRenderer = quad.GetComponent<Renderer>();
        if (shader != null)
            quadRenderer.material = new Material(shader);
        quadRenderer.material.mainTexture = texture != null ? texture : Texture2D.whiteTexture;
        quadRenderer.material.color = trailColor;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        return quadRenderer;
    }

    // A fat arrow pointing to the top of the picture.
    // Three soft chevrons that get brighter towards the tip (top of the picture).
    static Texture2D MakeArrowTexture()
    {
        const int size = 192;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Trilinear;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(Mathf.Abs((x + 0.5f) / size - 0.5f), (y + 0.5f) / size);
                float alpha = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float mid = 0.24f + 0.25f * i;
                    Vector2 tip = new Vector2(0f, mid + 0.13f), end = new Vector2(0.27f, mid - 0.11f);
                    Vector2 along = end - tip;
                    float t = Mathf.Clamp01(Vector2.Dot(p - tip, along) / along.sqrMagnitude);
                    float distance = (p - (tip + along * t)).magnitude;
                    float line = Mathf.Clamp01((0.042f - distance) / 0.012f);
                    float halo = Mathf.Clamp01(1f - distance / 0.11f);
                    float strength = 0.35f + 0.325f * i;
                    alpha = Mathf.Max(alpha, strength * Mathf.Max(line, halo * halo * 0.3f));
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(true);
        return texture;
    }

    // Soft round spot, or a soft ring.
    static Texture2D MakeGlowTexture(bool ring)
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float alpha;
                if (ring)
                {
                    float band = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.78f) / 0.14f);
                    float inside = r < 0.78f ? 0.16f * (1f - r / 0.78f) + 0.06f : 0f;
                    alpha = Mathf.Max(band * band, inside);
                }
                else
                {
                    alpha = Mathf.Clamp01(1f - r);
                    alpha *= alpha;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    GameObject CreateWalkHint(string text, out RectTransform root)
    {
        root = null;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        GameObject card = new GameObject("Login_WalkHint", typeof(RectTransform));
        Canvas canvas = card.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        card.AddComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 4f;
        root = (RectTransform)card.transform;
        root.sizeDelta = new Vector2(820f, 64f);
        root.localScale = Vector3.one * (0.68f / 820f) * (HintDistance / 0.7f);

        GameObject back = new GameObject("Background", typeof(RectTransform));
        back.transform.SetParent(root, false);
        ((RectTransform)back.transform).sizeDelta = root.sizeDelta;
        UnityEngine.UI.Image image = back.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.09f, 0.11f, 0.15f, 0.85f);
        image.raycastTarget = false;

        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(root, false);
        ((RectTransform)label.transform).sizeDelta = root.sizeDelta - new Vector2(30f, 10f);
        TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = null;
        foreach (Canvas other in FindObjectsOfType<Canvas>(true))
        {
            if (other == null || !other.name.Contains("Instruction_Canvas"))
                continue;
            TMP_Text sample = other.GetComponentInChildren<TMP_Text>(true);
            if (sample != null && sample.font != null) { font = sample.font; break; }
        }
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = 28f;
        tmp.enableWordWrapping = false;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        Camera cam = Camera.main;
        if (cam != null)
        {
            root.position = cam.transform.position + cam.transform.forward * HintDistance + cam.transform.up * (HintDistance * 0.15f);
            root.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        }
        return card;
    }

    static Transform LoginPanelRoot(GameObject panel)
    {
        if (panel == null)
            return null;
        Canvas canvas = panel.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return panel.transform;
        return canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform;
    }

    static void PlacePanel(Transform panel, Vector3 position, Quaternion rotation)
    {
        if (panel == null)
            return;
        panel.position = position;
        panel.rotation = rotation;
    }

    // Shrinks a panel (never enlarges it) so it is at most maxWidth wide and 0.85 x maxWidth
    // high in the world.
    void FitPanel(Transform panel, float maxWidth, float maxHeight = -1f)
    {
        if (maxHeight <= 0f)
            maxHeight = maxWidth * 0.85f;
        if (panel == null)
            return;
        if (!panelSceneScales.TryGetValue(panel, out Vector3 sceneScale))
        {
            sceneScale = panel.localScale;
            panelSceneScales[panel] = sceneScale;
        }
        panel.localScale = sceneScale;

        RectTransform rect = panel as RectTransform;
        if (rect == null || maxWidth <= 0f)
            return;
        float width = rect.rect.width * Mathf.Abs(panel.lossyScale.x);
        float height = rect.rect.height * Mathf.Abs(panel.lossyScale.y);
        if (width <= 0.001f || height <= 0.001f)
            return;
        float factor = Mathf.Min(1f, maxWidth / width, maxHeight / height);
        panel.localScale = sceneScale * factor;
    }

    // The spot where the nurse greets at the ward door (the door guide point of Part 0), the
    // direction from there straight to the door, and how far the door is.
    bool doorPoseWarned;

    bool TryGetDoorPose(out Vector3 stand, out Vector3 facing, out float doorDistance)
    {
        stand = Vector3.zero;
        facing = Vector3.forward;
        doorDistance = 0f;

        PediatricVitalSignsPart0Flow part0 = FindObjectOfType<PediatricVitalSignsPart0Flow>(true);
        Transform guide = part0 != null ? part0.DoorGuidePoint : null;
        if (guide == null && entranceSpawn != null)
            guide = entranceSpawn;
        DoorSlideOpener door = part0 != null ? part0.Door : null;
        if (door == null)
            door = FindObjectOfType<DoorSlideOpener>(true);
        if (guide == null || door == null)
        {
            if (!doorPoseWarned)
                Debug.LogWarning("[LoginController] Login at the door is not possible: " +
                    (guide == null ? "door guide point not found. " : "") + (door == null ? "door not found." : ""), this);
            doorPoseWarned = true;
            return false;
        }
        if (!doorPoseWarned)
            Debug.Log($"[LoginController] Door guide='{guide.name}' at {FormatVec(guide.position)}, door='{door.name}' at {FormatVec(door.transform.position)}", this);
        doorPoseWarned = true;

        // Middle of the door, and which way it is thin (that is the way through it).
        bool hasBounds = false;
        Bounds bounds = new Bounds(door.transform.position, Vector3.zero);
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }

        stand = guide.position;
        Vector3 toDoor = Vector3.ProjectOnPlane(bounds.center - stand, Vector3.up);
        if (toDoor.sqrMagnitude < 0.04f)
            return false;

        facing = toDoor.normalized;
        doorDistance = toDoor.magnitude;
        if (hasBounds)
        {
            float thin = Mathf.Min(bounds.size.x, bounds.size.z), wide = Mathf.Max(bounds.size.x, bounds.size.z);
            if (wide > 0.3f && thin < wide * 0.5f)
            {
                // Face the door squarely, even when only one leaf of a double door was found.
                Vector3 through = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
                float along = Vector3.Dot(toDoor, through);
                if (Mathf.Abs(along) > 0.3f)
                {
                    facing = through * Mathf.Sign(along);
                    doorDistance = Mathf.Abs(along);
                }
            }
        }
        return true;
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


