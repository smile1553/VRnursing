using UnityEngine;

public class YayaAnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private bool logEvents = true;

    [Header("Optional Pose Anchors")]
    [SerializeField] private Transform sittingAnchor;
    [SerializeField] private Transform layingDownAnchor;
    [SerializeField] private Transform layingSleepingAnchor;
    [SerializeField] private Transform kickingOutAnchor;
    [SerializeField] private Vector3 sittingAnchorPositionOffset = Vector3.zero;
    [SerializeField] private bool snapPositionToAnchors = true;
    [SerializeField] private bool snapRotationToAnchors = true;
    [SerializeField] private float layingDownMoveDuration = 0.8f;
    [SerializeField] private bool layingDownMoveHorizontalOnly = true;
    [SerializeField] private bool layingDownRotateYawOnly = true;
    [SerializeField] private Vector3 layingAnchorPositionOffset = new Vector3(0f, -0.45f, 0f);
    [SerializeField] private Vector3 layingAnchorEulerOffset = Vector3.zero;
    [SerializeField] private Vector3 kickingOutAdditionalPositionOffset = new Vector3(0f, 0.45f, 0f);
    [SerializeField] private bool disableRootMotion = true;
    [SerializeField] private bool holdLayingAnchorAfterSnap = true;

    [Header("Animator State Names")]
    [SerializeField] private string sittingIdleState = "sitting_idle";
    [SerializeField] private string sittingDisbeliefState = "sitting_disbelief";
    [SerializeField] private string sittingRubbingArmState = "sitting_rubbing_arm";
    [SerializeField] private string layingSleepingState = "laying_sleeping";
    [SerializeField] private string layingDownState = "lying_down";
    [SerializeField] private string kickingOutState = "kicking_out";

    private Coroutine anchorMoveRoutine;
    private Transform heldLayingAnchor;
    private Vector3 heldAdditionalPositionOffset;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null && disableRootMotion)
            animator.applyRootMotion = false;
    }

    private void LateUpdate()
    {
        if (!holdLayingAnchorAfterSnap || anchorMoveRoutine != null || heldLayingAnchor == null)
            return;

        ApplyLayingAnchor(heldLayingAnchor);
    }

    public void PlaySittingIdle()
    {
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToAnchor(sittingAnchor);
        PlayState(sittingIdleState);
    }

    public void PlaySittingDisbelief()
    {
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToAnchor(sittingAnchor);
        PlayState(sittingDisbeliefState);
    }

    public void PlaySittingRubbingArm()
    {
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToAnchor(sittingAnchor);
        PlayState(sittingRubbingArmState);
    }

    public void PlayLayingSleeping()
    {
        StopAnchorMove();
        SnapToLayingAnchor(GetSharedLayingAnchor());
        PlayState(layingSleepingState);
    }

    public void PlayLayingDown()
    {
        MoveToLayingDownAnchor();
        PlayState(layingDownState);
    }

    public void PlayKickingOut()
    {
        StopAnchorMove();
        heldAdditionalPositionOffset = kickingOutAdditionalPositionOffset;
        PlayState(kickingOutState);
    }

    public void PlayCustom(string stateName)
    {
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        PlayState(stateName);
    }

    private void PlayState(string stateName)
    {
        if (animator == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Animator is missing.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning("[YayaAnimationPlayer] State name is empty.", this);
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(layerIndex, stateHash))
        {
            Debug.LogWarning($"[YayaAnimationPlayer] Animator state not found: {stateName}", this);
            return;
        }

        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Play animation: {stateName}", this);

        animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex);
    }

    private void SnapToAnchor(Transform anchor)
    {
        StopAnchorMove();

        if (anchor == null)
            return;

        if (snapPositionToAnchors)
        {
            Vector3 effectiveOffset = sittingAnchorPositionOffset;
            if (effectiveOffset.y < 0f)
                effectiveOffset.y = 0f;

            transform.position = anchor.position + effectiveOffset;
        }

        if (snapRotationToAnchors)
            transform.rotation = anchor.rotation;
    }

    private void SnapToLayingAnchor(Transform anchor)
    {
        StopAnchorMove();

        if (anchor == null)
            return;

        ApplyLayingAnchor(anchor);
        heldLayingAnchor = anchor;
        heldAdditionalPositionOffset = Vector3.zero;
    }

    private void MoveToLayingDownAnchor()
    {
        StopAnchorMove();

        Transform anchor = GetSharedLayingAnchor();
        if (anchor == null)
            return;

        if (layingDownMoveDuration <= 0f)
        {
            SnapToLayingAnchor(anchor);
            return;
        }

        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        anchorMoveRoutine = StartCoroutine(MoveToAnchorRoutine(anchor, layingDownMoveDuration));
    }

    private Transform GetSharedLayingAnchor()
    {
        if (layingSleepingAnchor != null)
            return layingSleepingAnchor;

        if (layingDownAnchor != null)
            return layingDownAnchor;

        return kickingOutAnchor;
    }

    private System.Collections.IEnumerator MoveToAnchorRoutine(Transform anchor, float duration)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 targetPosition = anchor.position;
        targetPosition += layingAnchorPositionOffset;
        if (ShouldKeepCurrentLayingHeight(anchor))
            targetPosition.y = startPosition.y;

        Quaternion targetRotation = layingDownRotateYawOnly
            ? Quaternion.Euler(0f, anchor.eulerAngles.y + layingAnchorEulerOffset.y, 0f)
            : anchor.rotation * Quaternion.Euler(layingAnchorEulerOffset);

        if (layingDownRotateYawOnly)
            startRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        float elapsed = 0f;

        while (elapsed < duration && anchor != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            if (snapPositionToAnchors)
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);

            if (snapRotationToAnchors)
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            yield return null;
        }

        if (anchor != null)
        {
            ApplyLayingAnchor(anchor);
            heldLayingAnchor = anchor;
            heldAdditionalPositionOffset = Vector3.zero;
        }

        anchorMoveRoutine = null;
    }

    private void StopAnchorMove()
    {
        if (anchorMoveRoutine == null)
            return;

        StopCoroutine(anchorMoveRoutine);
        anchorMoveRoutine = null;
    }

    private bool ShouldKeepCurrentLayingHeight(Transform anchor)
    {
        return layingDownMoveHorizontalOnly && anchor != kickingOutAnchor;
    }

    private void ApplyLayingAnchor(Transform anchor)
    {
        if (anchor == null)
            return;

        if (snapPositionToAnchors)
        {
            Vector3 targetPosition = anchor.position;
            targetPosition += layingAnchorPositionOffset;
            targetPosition += heldAdditionalPositionOffset;
            if (ShouldKeepCurrentLayingHeight(anchor))
                targetPosition.y = transform.position.y;
            transform.position = targetPosition;
        }

        if (snapRotationToAnchors)
        {
            transform.rotation = layingDownRotateYawOnly
                ? Quaternion.Euler(0f, anchor.eulerAngles.y + layingAnchorEulerOffset.y, 0f)
                : anchor.rotation * Quaternion.Euler(layingAnchorEulerOffset);
        }
    }
}
