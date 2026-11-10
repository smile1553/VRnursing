using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MedicalRecordPanelFollower : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] Transform head;
    [SerializeField] Vector3 localOffset = new Vector3(0f, -0.02f, 1.2f);
    [SerializeField] Vector3 localEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] bool placeOnEnable = true;

    [Header("Stay in front of the eyes (like the dialogue box)")]
    [Tooltip("Keeps following the head while open, so it never ends up inside a wall.")]
    [SerializeField] bool keepInFrontWhileOpen = true;
    [Tooltip(">0: distance in front of the head (m), overrides Local Offset Z. Closer = less chance to clip into walls.")]
    [SerializeField] float overrideDistance = 0.95f;
    [Tooltip("On: use the shared HUD distance (HudDepth.Distance), so the record is on the same plane as the prompt, dialogue box, sticker, emotion bar... Off: use Override Distance.")]
    [SerializeField] bool useSharedHudDistance = true;
    [SerializeField] float followLerp = 10f;
    [Tooltip("Keep the same apparent size as before when moved closer (scale x overrideDistance / original distance), times this.")]
    [SerializeField] float sizeMultiplier = 1f;
    Vector3 originalScale;
    bool scaleApplied;

    [Header("Hide other overlays while the record is open")]
    [SerializeField] bool hideOverlaysWhileOpen = true;
    [Tooltip("Canvases whose name contains any of these are hidden (|-separated).")]
    [SerializeField] string hideCanvasNameContains = "Instruction_Canvas";
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    readonly List<Collider> hiddenColliders = new List<Collider>();

    void Awake()
    {
        originalScale = transform.localScale;
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
    }

    void OnEnable()
    {
        if (placeOnEnable)
            PlaceInFrontOfHead();
        if (hideOverlaysWhileOpen)
            HideOverlays();
    }

    void OnDisable()
    {
        RestoreOverlays();
    }

    // Prompt boxes and the bear sticker would cover the record, so hide them while it is open.
    void HideOverlays()
    {
        RestoreOverlays();
        string[] names = hideCanvasNameContains.Split('|');
        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            if (canvas == null || !canvas.isRootCanvas || !canvas.enabled || transform.IsChildOf(canvas.transform))
                continue;
            foreach (string raw in names)
            {
                string n = raw.Trim();
                if (n.Length > 0 && canvas.name.Contains(n))
                {
                    canvas.enabled = false;
                    hiddenCanvases.Add(canvas);
                    break;
                }
            }
        }

        Part3VisualDemoController demo = FindObjectOfType<Part3VisualDemoController>(true);
        GameObject sticker = demo != null ? demo.StickerObject : null;
        if (sticker != null)
        {
            foreach (Renderer r in sticker.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                    hiddenRenderers.Add(r);
                }
            }
            foreach (Collider c in sticker.GetComponentsInChildren<Collider>(true))
            {
                if (c != null && c.enabled)
                {
                    c.enabled = false;
                    hiddenColliders.Add(c);
                }
            }
        }
    }

    void RestoreOverlays()
    {
        foreach (Canvas c in hiddenCanvases)
            if (c != null) c.enabled = true;
        foreach (Renderer r in hiddenRenderers)
            if (r != null) r.enabled = true;
        foreach (Collider c in hiddenColliders)
            if (c != null) c.enabled = true;
        hiddenCanvases.Clear();
        hiddenRenderers.Clear();
        hiddenColliders.Clear();
    }

    public void PlaceInFrontOfHead()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        if (head == null)
            return;

        transform.position = head.TransformPoint(EffectiveOffset());
        transform.rotation = FacingRotation();
        ApplyDistanceScale();
    }

    void ApplyDistanceScale()
    {
        if (scaleApplied)
            return;
        scaleApplied = true;
        float distance = closeUpDistance > 0f ? closeUpDistance : NormalDistance();
        if (distance > 0f && localOffset.z > 0.01f)
            transform.localScale = originalScale * (distance / localOffset.z) * Mathf.Max(0.01f, sizeMultiplier);
    }

    float closeUpDistance;

    // Something stands close in front of the student (a desk, a computer): hold the record
    // nearer to the eyes so it is not cut by it. It is made smaller by the same amount, so it
    // looks the same size. 0 = back to the normal distance.
    public void SetCloseUp(float distance)
    {
        closeUpDistance = Mathf.Max(0f, distance);
        scaleApplied = false;
        if (isActiveAndEnabled)
            PlaceInFrontOfHead();
    }

    void LateUpdate()
    {
        if (!keepInFrontWhileOpen || head == null)
            return;

        Vector3 targetPosition = head.TransformPoint(EffectiveOffset());
        float k = followLerp <= 0f ? 1f : 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, k);
        transform.rotation = FacingRotation();
    }

    // Always squarely facing the eyes from where the record is right now, and upright: while it
    // catches up with a turning head it used to hang at an angle (one side looked bigger), and
    // a tilted head made it lean.
    Quaternion FacingRotation()
    {
        Vector3 away = transform.position - head.position;
        if (away.sqrMagnitude < 1e-6f)
            away = head.forward;
        away.Normalize();
        Vector3 up = Mathf.Abs(Vector3.Dot(away, Vector3.up)) > 0.98f ? head.up : Vector3.up;
        return Quaternion.LookRotation(away, up) * Quaternion.Euler(localEulerAngles);
    }

    Vector3 EffectiveOffset()
    {
        Vector3 offset = localOffset;
        if (closeUpDistance > 0f)
            offset.z = closeUpDistance;
        else if (NormalDistance() > 0f)
            offset.z = NormalDistance();
        return offset;
    }

    float NormalDistance()
    {
        if (useSharedHudDistance && HudDepth.Enabled && HudDepth.Distance > 0.05f)
            return HudDepth.Distance;
        return overrideDistance;
    }
}
