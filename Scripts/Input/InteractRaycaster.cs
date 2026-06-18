using UnityEngine;

[DisallowMultipleComponent]
public class InteractRaycaster : MonoBehaviour
{
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private VRInputRouter router;
    [SerializeField] private string hand = "Right";
    [SerializeField] private string source = "VR";
    [SerializeField] private bool emitPointContinuously = true;
    [SerializeField] private bool throttlePointEvents = false;
    [SerializeField] private float pointEmitInterval = 0.05f;

    public bool HasHit { get; private set; }
    public RaycastHit LastHit { get; private set; }

    private float nextPointEmitTime;

    private void Awake()
    {
        if (rayOrigin == null)
        {
            rayOrigin = transform;
        }

        if (router == null)
        {
            router = FindObjectOfType<VRInputRouter>();
        }
    }

    private void Update()
    {
        if (!emitPointContinuously)
        {
            return;
        }

        if (throttlePointEvents)
        {
            if (Time.unscaledTime < nextPointEmitTime)
            {
                return;
            }

            nextPointEmitTime = Time.unscaledTime + Mathf.Max(0.01f, pointEmitInterval);
        }

        EmitPoint();
    }

    public void SetRayOrigin(Transform origin)
    {
        rayOrigin = origin;
    }

    public void EmitPoint()
    {
        DoRaycast(emitPoint: true, emitClick: false);
    }

    public void EmitClick()
    {
        DoRaycast(emitPoint: true, emitClick: true);
    }

    private void DoRaycast(bool emitPoint, bool emitClick)
    {
        if (router == null || rayOrigin == null)
        {
            return;
        }

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        HasHit = Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore);
        LastHit = hit;

        if (!HasHit)
        {
            return;
        }

        GameObject target = hit.collider != null ? hit.collider.gameObject : null;
        string targetId = ResolveTargetId(target);

        if (emitPoint)
        {
            router.EmitPoint(targetId, target, hit.point, hand, source);
        }

        if (emitClick)
        {
            router.EmitClick(targetId, target, hit.point, hand, source);
        }
    }

    private static string ResolveTargetId(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        VRInteractTrigger trigger = target.GetComponentInParent<VRInteractTrigger>();
        if (trigger != null && !string.IsNullOrEmpty(trigger.targetId))
        {
            return trigger.targetId;
        }

        return target.name;
    }
}
