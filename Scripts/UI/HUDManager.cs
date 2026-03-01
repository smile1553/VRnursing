using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [Header("HMD Camera (不填就用 Camera.main)")]
    public Transform hmdCamera;

    [Header("UI Roots (拖你的UI根物件進來)")]
    public Transform dialogueUI;      // 對話框 root
    public Transform tipUI;           // 提示框 root
    public Transform recordButtonUI;  // 病歷表按鈕 root

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

    private void Awake()
    {
        if (hmdCamera == null && Camera.main != null)
            hmdCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (hmdCamera == null) return;

        Place(dialogueUI, dialogueOffset);
        Place(tipUI, tipOffset);
        Place(recordButtonUI, recordOffset);
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
}
