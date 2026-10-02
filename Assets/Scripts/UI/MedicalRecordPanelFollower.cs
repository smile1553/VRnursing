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
        transform.rotation = head.rotation * Quaternion.Euler(localEulerAngles);
        ApplyDistanceScale();
    }

    void ApplyDistanceScale()
    {
        if (scaleApplied)
            return;
        scaleApplied = true;
        if (overrideDistance > 0f && localOffset.z > 0.01f)
            transform.localScale = originalScale * (overrideDistance / localOffset.z) * Mathf.Max(0.01f, sizeMultiplier);
    }

    void LateUpdate()
    {
        if (!keepInFrontWhileOpen || head == null)
            return;

        Vector3 targetPosition = head.TransformPoint(EffectiveOffset());
        Quaternion targetRotation = head.rotation * Quaternion.Euler(localEulerAngles);
        float k = followLerp <= 0f ? 1f : 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, k);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, k);
    }

    Vector3 EffectiveOffset()
    {
        Vector3 offset = localOffset;
        if (overrideDistance > 0f)
            offset.z = overrideDistance;
        return offset;
    }
}
