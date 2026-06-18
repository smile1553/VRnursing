using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [Header("HMD Camera (不填就用 Camera.main)")]
    public Transform hmdCamera;

    [Header("Mode")]
    [Tooltip("UI 已經掛在 XR Camera/HUDRoot 底下時請保持關閉；只有世界空間 UI 需要自動定位時才開。")]
    public bool manageWorldSpacePlacement = false;

    [Header("UI Roots (拖你的UI根物件進來)")]
    public Transform dialogueUI;      // 對話框 root
    public Transform tipUI;           // 提示框 root
    public Transform recordButtonUI;  // 病歷表按鈕 root

    [Header("Camera Child UI Visibility")]
    public GameObject quizCanvas;          // 有題目才顯示
    public GameObject medicalPanelCanvas;  // 點病歷才顯示

    [Header("Common Distance (meters)")]
    [Range(0.3f, 2.0f)]
    public float distance = 0.85f;

    [Header("Offsets in Camera Space (meters)")]
    // 對話框中上、提示框中下、病歷button左上
    public Vector2 dialogueOffset = new Vector2(0.0f, 0.20f);     // X=右, Y=上
    public Vector2 tipOffset      = new Vector2(0.0f, -0.20f);
    public Vector2 recordOffset   = new Vector2(-0.28f, 0.22f);

    [Header("Rotation")]
    public bool faceCamera = true;     // UI 面向相機
    public bool keepUpright = true;    // UI 上方向固定世界上方(比較不暈)

    [Header("Performance")]
    public bool useMotionThreshold = false;
    public float minMoveDistance = 0.01f;
    public float minRotateAngle = 1f;

    Vector3 _lastCameraPosition;
    Quaternion _lastCameraRotation;
    bool _hasLastCameraPose;

    private void Awake()
    {
        if (hmdCamera == null && Camera.main != null)
            hmdCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (!manageWorldSpacePlacement)
            return;

        if (hmdCamera == null) return;

        if (useMotionThreshold && !CameraMovedEnough())
            return;

        Place(dialogueUI, dialogueOffset);
        Place(tipUI, tipOffset);
        Place(recordButtonUI, recordOffset);
    }

    private bool CameraMovedEnough()
    {
        if (!_hasLastCameraPose)
        {
            CacheCameraPose();
            return true;
        }

        float minMoveSqr = minMoveDistance * minMoveDistance;
        bool moved = (hmdCamera.position - _lastCameraPosition).sqrMagnitude >= minMoveSqr;
        bool rotated = Quaternion.Angle(hmdCamera.rotation, _lastCameraRotation) >= minRotateAngle;
        if (!moved && !rotated)
            return false;

        CacheCameraPose();
        return true;
    }

    private void CacheCameraPose()
    {
        _lastCameraPosition = hmdCamera.position;
        _lastCameraRotation = hmdCamera.rotation;
        _hasLastCameraPose = true;
    }

    private void Place(Transform ui, Vector2 offset)
    {
        if (ui == null) return;

        Vector3 camPos = hmdCamera.position;
        Vector3 pos = camPos
                    + hmdCamera.forward * distance
                    + hmdCamera.right   * offset.x
                    + hmdCamera.up      * offset.y;

        ui.position = pos;

        if (faceCamera)
        {
            Vector3 toCam = (camPos - ui.position);
            if (toCam.sqrMagnitude > 0.0001f)
            {
                Vector3 up = keepUpright ? Vector3.up : hmdCamera.up;
                ui.rotation = Quaternion.LookRotation(-toCam.normalized, up);
            }
        }
    }

    public void SetQuizVisible(bool visible)
    {
        SetActiveIfChanged(quizCanvas, visible);
    }

    public void SetMedicalPanelVisible(bool visible)
    {
        SetActiveIfChanged(medicalPanelCanvas, visible);
    }

    public void ToggleMedicalPanel()
    {
        if (medicalPanelCanvas != null)
            SetMedicalPanelVisible(!medicalPanelCanvas.activeSelf);
    }

    static void SetActiveIfChanged(GameObject target, bool visible)
    {
        if (target != null && target.activeSelf != visible)
            target.SetActive(visible);
    }
}
