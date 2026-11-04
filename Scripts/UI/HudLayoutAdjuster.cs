using UnityEngine;

// Nudges the camera-attached HUD canvases once at start:
//  - nurse prompt boxes (*Instruction_Canvas)  -> a little higher
//  - dialogue boxes (*Dialogue_Canvas*)         -> lower and slightly bigger
// Created automatically after the scene loads. To tune the values, add this component
// to any object in the scene (the automatic one is then skipped) and edit it in the Inspector.
[DefaultExecutionOrder(100)]
public class HudLayoutAdjuster : MonoBehaviour
{
    [Header("Prompt boxes (name contains)")]
    [SerializeField] private string promptCanvasNameContains = "Instruction_Canvas";
    [Tooltip("Meters along the camera's up axis.")]
    [SerializeField] private float promptRaise = 0.04f;

    [Header("Dialogue boxes (name contains)")]
    [SerializeField] private string dialogueCanvasNameContains = "Dialogue_Canva";
    [Tooltip("Meters along the camera's up axis (negative = down).")]
    [SerializeField] private float dialogueRaise = 0.04f;
    [SerializeField] private float dialogueScale = 1.15f;

    [SerializeField] private bool logChanges = true;

    private bool applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<HudLayoutAdjuster>(true) != null)
            return;

        new GameObject("HudLayoutAdjuster (auto)").AddComponent<HudLayoutAdjuster>();
    }

    private void Start()
    {
        Apply();
    }

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (applied)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        applied = true;
        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != RenderMode.WorldSpace)
                continue;

            Transform t = canvas.transform;
            if (!t.IsChildOf(cam.transform))
                continue;

            string n = t.name;
            bool isPrompt = !string.IsNullOrEmpty(promptCanvasNameContains) && n.Contains(promptCanvasNameContains);
            bool isDialogue = !string.IsNullOrEmpty(dialogueCanvasNameContains) && n.Contains(dialogueCanvasNameContains);
            if (!string.IsNullOrEmpty(promptCanvasNameContains) && n.Contains(promptCanvasNameContains))
            {
                Shift(t, cam.transform, promptRaise);
                if (logChanges)
                    Debug.Log($"[HudLayoutAdjuster] Prompt '{n}' raised {promptRaise:0.###} m", t);
            }
            else if (!string.IsNullOrEmpty(dialogueCanvasNameContains) && n.Contains(dialogueCanvasNameContains))
            {
                Shift(t, cam.transform, dialogueRaise);
                t.localScale *= dialogueScale;
                if (logChanges)
                    Debug.Log($"[HudLayoutAdjuster] Dialogue '{n}' moved {dialogueRaise:0.###} m, scale x{dialogueScale:0.##}", t);
            }

            // Same distance as the rest of the display (see HudDepth): moved along the line of
            // sight and scaled, so it looks the same size in the same place.
            if (isPrompt || isDialogue)
            {
                Vector3 local = cam.transform.InverseTransformPoint(t.position);
                Vector3 flat = HudDepth.Flatten(local, out float depthFactor);
                if (Mathf.Abs(depthFactor - 1f) > 0.001f)
                {
                    t.position = cam.transform.TransformPoint(flat);
                    t.localScale *= depthFactor;
                    if (logChanges)
                        Debug.Log($"[HudLayoutAdjuster] '{n}' moved from {local.z:0.##} m to {flat.z:0.##} m in front of the eyes", t);
                }
            }
        }
    }

    private static void Shift(Transform t, Transform cam, float metersUp)
    {
        t.position += cam.up * metersUp;
    }
}
