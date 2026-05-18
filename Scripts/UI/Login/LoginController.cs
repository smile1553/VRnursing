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

    private void Start()
    {
        Debug.Log("[LoginController] Start()");
        StartCoroutine(AlignOnStart());
    }

    public void OnLoginClicked()
    {
        Debug.Log("LOGIN CLICKED");
        Debug.Log("[LoginController] OnLoginClicked");
        if (wardSpawn != null)
        {
            Debug.Log($"[LoginController] Target spawn={wardSpawn.name} pos={FormatVec(wardSpawn.position)} yaw={wardSpawn.rotation.eulerAngles.y:0.###}");
        }
        MoveRigTo(wardSpawn);

        if (loginUIRoot != null)
        {
            loginUIRoot.SetActive(false);
        }

        EmitSignal("LoginCompleted", null);
    }

    private void MoveRigTo(Transform spawn)
    {
        if (xrRig == null || spawn == null)
        {
            if (xrRig == null)
            {
                Debug.LogWarning("[LoginController] MoveRigTo aborted: xrRig is null.");
            }
            if (spawn == null)
            {
                Debug.LogWarning("[LoginController] MoveRigTo aborted: spawn is null.");
            }
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[LoginController] MoveRigTo aborted: Camera.main is null.");
            return;
        }

        LogPose("[LoginController] MoveRigTo BEFORE", cam.transform, xrRig, spawn);

        var spawnPosition = spawn.position;
        var spawnYaw = spawn.rotation.eulerAngles.y + 180f;
        if (spawn.IsChildOf(cam.transform) || spawn.IsChildOf(xrRig))
        {
            Debug.LogWarning("[LoginController] Spawn is under the HMD/XR rig. Use a fixed scene marker for stable teleport targets.");
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
            Debug.LogWarning("[LoginController] MoveRigToPose aborted: xrRig is null.");
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[LoginController] MoveRigToPose aborted: Camera.main is null.");
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
            Debug.LogWarning("[LoginController] EmitSignal skipped: signalBus not set or does not implement ISignalBus.");
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
            Debug.Log($"[LoginController] AlignOnStart initial delay frame {i + 1}/{initialFrameDelay}");
            yield return null;
        }

        for (int i = 0; i < maxAttempts; i++)
        {
            yield return null;
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[LoginController] AlignOnStart waiting: Camera.main is null.");
                continue;
            }

            if (xrRig == null)
            {
                Debug.LogWarning("[LoginController] AlignOnStart aborted: xrRig is null.");
                yield break;
            }

            Debug.Log($"[LoginController] AlignOnStart attempt {i + 1}/{maxAttempts}");
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

        Debug.Log(
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
