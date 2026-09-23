using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class LoginController : MonoBehaviour
{
    [Header("References (assign via Inspector)")]
    [SerializeField] private Transform xrRig;
    [SerializeField] private Transform entranceSpawn;
    [SerializeField] private Transform wardSpawn;
    [SerializeField] private GameObject loginUIRoot;
    [SerializeField] private MonoBehaviour signalBus;

    [Header("Ward Entry Flow")]
    [SerializeField] private ScenarioController scenarioController;
    [SerializeField] private bool requireGreetingBeforeWardEntry = true;
    [SerializeField] private string greetingStepId = "intro_nurse";

    private bool loginCompleted;
    private bool wardEntered;
    private bool listeningForScenarioCompletion;

    private void Awake()
    {
        ResolveScenarioController();
    }

    private void OnEnable()
    {
        SubscribeToScenario();
    }

    private void OnDisable()
    {
        UnsubscribeFromScenario();
    }

    private void Start()
    {
        SubscribeToScenario();
        RuntimeLog.Info("[LoginController] Start()");
        StartCoroutine(AlignOnStart());
    }

    public void OnLoginClicked()
    {
        RuntimeLog.Info("LOGIN CLICKED");
        RuntimeLog.Info("[LoginController] OnLoginClicked");

        loginCompleted = true;
        wardEntered = false;

        Transform loginDestination = requireGreetingBeforeWardEntry ? entranceSpawn : wardSpawn;
        if (loginDestination != null)
        {
            RuntimeLog.Info($"[LoginController] Target spawn={loginDestination.name} pos={FormatVec(loginDestination.position)} yaw={loginDestination.rotation.eulerAngles.y:0.###}");
        }
        else if (requireGreetingBeforeWardEntry)
        {
            RuntimeLog.Warning("[LoginController] entranceSpawn is not assigned. The player cannot wait outside the ward before greeting.");
        }
        MoveRigTo(loginDestination);

        if (loginUIRoot != null)
        {
            loginUIRoot.SetActive(false);
        }

        EmitSignal("LoginCompleted", null);

        ResolveScenarioController();
        SubscribeToScenario();
        if (requireGreetingBeforeWardEntry && scenarioController != null)
        {
            // Restart at the greeting step after login so speech captured on the
            // login screen cannot unlock the ward entrance.
            scenarioController.StartScenario();
        }
        else if (requireGreetingBeforeWardEntry)
        {
            RuntimeLog.Warning("[LoginController] ScenarioController not found. Greeting completion cannot open the ward flow.");
        }
    }

    private void ResolveScenarioController()
    {
        if (scenarioController == null)
            scenarioController = FindObjectOfType<ScenarioController>();
    }

    private void SubscribeToScenario()
    {
        if (listeningForScenarioCompletion)
            return;

        ResolveScenarioController();
        if (scenarioController == null || scenarioController.stepCompleted == null)
            return;

        scenarioController.stepCompleted.AddListener(OnScenarioStepCompleted);
        listeningForScenarioCompletion = true;
    }

    private void UnsubscribeFromScenario()
    {
        if (!listeningForScenarioCompletion)
            return;

        if (scenarioController != null && scenarioController.stepCompleted != null)
            scenarioController.stepCompleted.RemoveListener(OnScenarioStepCompleted);
        listeningForScenarioCompletion = false;
    }

    private void OnScenarioStepCompleted(string stepId)
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

    private void MoveRigTo(Transform spawn)
    {
        if (xrRig == null || spawn == null)
        {
            if (xrRig == null)
            {
                RuntimeLog.Warning("[LoginController] MoveRigTo aborted: xrRig is null.");
            }
            if (spawn == null)
            {
                RuntimeLog.Warning("[LoginController] MoveRigTo aborted: spawn is null.");
            }
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            RuntimeLog.Warning("[LoginController] MoveRigTo aborted: Camera.main is null.");
            return;
        }

        LogPose("[LoginController] MoveRigTo BEFORE", cam.transform, xrRig, spawn);

        var spawnPosition = spawn.position;
        var spawnYaw = spawn.rotation.eulerAngles.y + 180f;
        if (spawn.IsChildOf(cam.transform) || spawn.IsChildOf(xrRig))
        {
            RuntimeLog.Warning("[LoginController] Spawn is under the HMD/XR rig. Use a fixed scene marker for stable teleport targets.");
        }

        var yawDelta = spawnYaw - cam.transform.rotation.eulerAngles.y;
        xrRig.RotateAround(cam.transform.position, Vector3.up, yawDelta);

        var camOffset = cam.transform.position - xrRig.position;
        xrRig.position = new Vector3(
            spawnPosition.x - camOffset.x,
            spawnPosition.y,
            spawnPosition.z - camOffset.z
        );

        LogPose("[LoginController] MoveRigTo AFTER", cam.transform, xrRig, spawn);
    }

    private void MoveRigToPose(Vector3 camWorldPos, float yawDeg)
    {
        if (xrRig == null)
        {
            RuntimeLog.Warning("[LoginController] MoveRigToPose aborted: xrRig is null.");
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            RuntimeLog.Warning("[LoginController] MoveRigToPose aborted: Camera.main is null.");
            return;
        }

        xrRig.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        LogPose("[LoginController] MoveRigToPose BEFORE", cam.transform, xrRig, null);

        var camOffset = cam.transform.position - xrRig.position;
        xrRig.position = camWorldPos - camOffset;

        LogPose("[LoginController] MoveRigToPose AFTER", cam.transform, xrRig, null);
    }

    private void EmitSignal(string signalName, object payload)
    {
        if (signalBus is ISignalBus emitter)
        {
            emitter.Emit(signalName);
        }
        else
        {
            RuntimeLog.Warning("[LoginController] EmitSignal skipped: signalBus not set or does not implement ISignalBus.");
        }
    }

    public interface ISignalBus
    {
        void Emit(string signalName);
    }

    private IEnumerator AlignOnStart()
    {
        var targetCamPos = new Vector3(5.173f, 1.077f, -1.985f);
        const float targetYaw = 180.954f;
        const int initialFrameDelay = 3;
        const int maxAttempts = 6;

        for (int i = 0; i < initialFrameDelay; i++)
        {
            RuntimeLog.Info($"[LoginController] AlignOnStart initial delay frame {i + 1}/{initialFrameDelay}");
            yield return null;
        }

        for (int i = 0; i < maxAttempts; i++)
        {
            yield return null;
            var cam = Camera.main;
            if (cam == null)
            {
                RuntimeLog.Warning("[LoginController] AlignOnStart waiting: Camera.main is null.");
                continue;
            }

            if (xrRig == null)
            {
                RuntimeLog.Warning("[LoginController] AlignOnStart aborted: xrRig is null.");
                yield break;
            }

            RuntimeLog.Info($"[LoginController] AlignOnStart attempt {i + 1}/{maxAttempts}");
            MoveRigToPose(targetCamPos, targetYaw);
            yield return null;
            MoveRigToPose(targetCamPos, targetYaw);
        }
    }

    private void LogPose(string label, Transform cam, Transform rig, Transform spawn)
    {
        var camYaw = cam != null ? cam.rotation.eulerAngles.y : 0f;
        var rigYaw = rig != null ? rig.rotation.eulerAngles.y : 0f;
        var spawnYaw = spawn != null ? spawn.rotation.eulerAngles.y : 0f;

        RuntimeLog.Info(
            $"{label} | camPos={FormatVec(cam != null ? cam.position : Vector3.zero)} camYaw={camYaw:0.###} " +
            $"| rigPos={FormatVec(rig != null ? rig.position : Vector3.zero)} rigYaw={rigYaw:0.###} " +
            $"| spawnPos={(spawn != null ? FormatVec(spawn.position) : "(none)")} spawnYaw={(spawn != null ? spawnYaw.ToString("0.###") : "(none)")}"
        );
    }

    private static string FormatVec(Vector3 v)
    {
        return $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";
    }
}
