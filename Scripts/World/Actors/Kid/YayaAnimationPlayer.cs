using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
    [SerializeField] private float layingDownMoveDuration = 0.12f;
    [SerializeField] private bool layingDownMoveHorizontalOnly = false;
    [SerializeField] private bool layingDownRotateYawOnly = true;
    [SerializeField] private Vector3 layingAnchorPositionOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private Vector3 layingSleepingPositionOffset = new Vector3(0f, -0.12f, 0f);
    [SerializeField] private Vector3 layingDownPositionOffset = new Vector3(0f, -0.08f, 0f);
    [SerializeField] private Vector3 kickingOutPositionOffset = Vector3.zero;
    [SerializeField] private float extraLayingHeight = 0f;
    [SerializeField] private float layingDownExtraHeight = 0f;
    [SerializeField] private Vector3 layingAnchorEulerOffset = Vector3.zero;
    [SerializeField] private Vector3 kickingOutAdditionalPositionOffset = Vector3.zero;
    [SerializeField] private float kickingOutStartTimeSeconds = 0f;
    [SerializeField] private bool keepCurrentHeightWhenKickingOut = true;
    [SerializeField] private bool restartKickingOutStateDirectly = true;
    [SerializeField] private bool loopKickingOut = true;
    [SerializeField] private float kickingOutLoopInterval = 1.2f;
    [SerializeField] private bool disableRootMotion = true;
    [SerializeField] private bool holdLayingAnchorAfterSnap = true;

    [Header("Animator State Names")]
    [SerializeField] private string sittingIdleState = "sitting_idle";
    [SerializeField] private string sittingDisbeliefState = "sitting_disbelief";
    [SerializeField] private string sittingRubbingArmState = "sitting_rubbing_arm";
    [SerializeField] private string layingSleepingState = "laying_sleeping";
    [SerializeField] private string layingDownState = "lying_down";
    [SerializeField] private string kickingOutState = "kicking_out";
    [SerializeField] private string kidListenState = "KidArmature|KidListen";
    [SerializeField] private string kidCombineState = "kid_combine";
    [SerializeField] private string kidPointEarState = "KidPointEar";

    [Header("Clip Fallbacks")]
    [SerializeField] private AnimationClip kidListenClip;
    [SerializeField] private AnimationClip kidCombineClip;
    [SerializeField] private AnimationClip kidPointEarClip;

    private Coroutine anchorMoveRoutine;
    private Transform heldLayingAnchor;
    private Vector3 heldAdditionalPositionOffset;
    private float heldLayingHeightAdjustment;
    private Vector3 initialSittingPosition;
    private Quaternion initialSittingRotation;
    private bool keepCurrentHeightThisFrame;
    private Coroutine kickingOutLoopRoutine;
    private PlayableGraph clipGraph;

    private void Awake()
    {
        initialSittingPosition = transform.position;
        initialSittingRotation = transform.rotation;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null && disableRootMotion)
            animator.applyRootMotion = false;
    }

    private void OnDisable()
    {
        StopClipFallback();
    }

    private void OnDestroy()
    {
        StopClipFallback();
    }

    private void LateUpdate()
    {
        if (!holdLayingAnchorAfterSnap || anchorMoveRoutine != null || heldLayingAnchor == null)
            return;

        ApplyLayingAnchor(heldLayingAnchor, heldLayingHeightAdjustment);
    }

    public void PlaySittingIdle()
    {
        StopKickingOutLoop();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        PlayState(sittingIdleState);
    }

    public void PlaySittingDisbelief()
    {
        StopKickingOutLoop();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        PlayState(sittingDisbeliefState);
    }

    public void PlaySittingRubbingArm()
    {
        StopKickingOutLoop();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        PlayState(sittingRubbingArmState);
    }

    public void PlayLayingSleeping()
    {
        StopKickingOutLoop();
        StopAnchorMove();
        SnapToLayingAnchor(GetSharedLayingAnchor(), 0f, layingSleepingPositionOffset);
        PlayState(layingSleepingState);
    }

    public void PlayLayingDown()
    {
        StopKickingOutLoop();
        MoveToLayingDownAnchor();
        PlayState(layingDownState);
    }

    public void PlayKickingOut()
    {
        StopAnchorMove();
        Transform anchor = kickingOutAnchor != null ? kickingOutAnchor : GetSharedLayingAnchor();
        heldAdditionalPositionOffset = kickingOutAdditionalPositionOffset + kickingOutPositionOffset;
        heldLayingHeightAdjustment = 0f;
        if (anchor != null)
        {
            keepCurrentHeightThisFrame = keepCurrentHeightWhenKickingOut;
            ApplyLayingAnchor(anchor);
            keepCurrentHeightThisFrame = false;
            heldLayingAnchor = anchor;
        }
        if (restartKickingOutStateDirectly)
            RestartState(kickingOutState, kickingOutStartTimeSeconds);
        else
            PlayState(kickingOutState, kickingOutStartTimeSeconds);

        if (loopKickingOut)
            StartKickingOutLoop();
    }

    public void PlayKidListen()
    {
        StopKickingOutLoop();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        if (PlayFirstAvailableStateDirect(kidListenState, "Base Layer.KidArmature|KidListen", "KidArmature|KidListen", "Base Layer.KidArmature|Armature|KidListen", "KidArmature|Armature|KidListen", "Base Layer.KidListen", "KidListen", "Kid Listen"))
            return;

        PlayClipFallback(ref kidListenClip, true, "KidListen", "KidArmature|KidListen", "Kid Listen");
    }


    public void PlayKidCombine()
    {
        StopKickingOutLoop();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        if (PlayFirstAvailableStateDirect(kidCombineState, "kid_combine", "Kid_Combine", "KidCombine", "Kid Combine", "Base Layer.kid_combine", "Base Layer.KidCombine"))
            return;

        PlayClipFallback(ref kidCombineClip, true, "kid_combine", "Kid_Combine", "KidCombine", "Kid Combine");
    }

    public void PlayKidPointEar()
    {
        StopKickingOutLoop();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        if (PlayFirstAvailableStateDirect(kidPointEarState, "KidPointEar", "Kid Point Ear", "Base Layer.KidPointEar"))
            return;

        PlayClipFallback(ref kidPointEarClip, true, "KidPointEar", "Kid Point Ear");
    }
    public void PlayCustom(string stateName)
    {
        StopKickingOutLoop();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        PlayState(stateName);
    }

    private void PlayState(string stateName)
    {
        PlayState(stateName, 0f);
    }

    private void PlayState(string stateName, float fixedTimeOffset)
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

        StopClipFallback();
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Play animation: {stateName}", this);

        animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex, Mathf.Max(0f, fixedTimeOffset));
    }

    private void RestartState(string stateName, float fixedTimeOffset)
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

        StopClipFallback();
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Restart animation: {stateName}", this);

        animator.Play(stateHash, layerIndex, 0f);
        if (fixedTimeOffset > 0f)
            animator.Update(fixedTimeOffset);
    }

    private bool PlayFirstAvailableState(params string[] stateNames)
    {
        if (animator == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Animator is missing.", this);
            return false;
        }

        foreach (string stateName in stateNames)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(layerIndex, stateHash))
                continue;

            StopClipFallback();
            if (logEvents)
                Debug.Log($"[YayaAnimationPlayer] Play animation: {stateName}", this);

            animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex);
            return true;
        }

        Debug.LogWarning("[YayaAnimationPlayer] None of the requested animator states were found.", this);
        return false;
    }

    private bool PlayFirstAvailableStateDirect(params string[] stateNames)
    {
        if (animator == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Animator is missing.", this);
            return false;
        }

        foreach (string stateName in stateNames)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(layerIndex, stateHash))
                continue;

            StopClipFallback();
            if (logEvents)
                Debug.Log($"[YayaAnimationPlayer] Play animation: {stateName}", this);

            animator.Play(stateHash, layerIndex, 0f);
            animator.Update(0f);
            return true;
        }

        Debug.LogWarning("[YayaAnimationPlayer] None of the requested animator states were found. Trying clip fallback.", this);
        return false;
    }
    private void PlayClipFallback(ref AnimationClip assignedClip, bool loop, params string[] clipNames)
    {
        if (animator == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Animator is missing.", this);
            return;
        }

        if (assignedClip == null)
            assignedClip = FindAnimationClipByName(clipNames);

        if (assignedClip == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] AnimationClip fallback not found. Drag the clip into the matching Clip Fallback field.", this);
            return;
        }

        StopClipFallback();
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Play clip fallback: {assignedClip.name}", this);

        clipGraph = PlayableGraph.Create($"{name}_{assignedClip.name}_Fallback");
        AnimationClipPlayable playable = AnimationClipPlayable.Create(clipGraph, assignedClip);
        playable.SetApplyFootIK(false);
        playable.SetDuration(loop ? double.PositiveInfinity : assignedClip.length);
        playable.SetTime(0d);
        playable.SetSpeed(1d);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(clipGraph, "Animation", animator);
        output.SetSourcePlayable(playable);
        clipGraph.Play();
    }

    private void StopClipFallback()
    {
        if (clipGraph.IsValid())
            clipGraph.Destroy();
    }

    private static AnimationClip FindAnimationClipByName(params string[] clipNames)
    {
        AnimationClip[] clips = Resources.FindObjectsOfTypeAll<AnimationClip>();
        foreach (string rawName in clipNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            foreach (AnimationClip clip in clips)
            {
                if (clip != null && clip.name.IndexOf(rawName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
        }

        return null;
    }
    private void SnapToSittingPose()
    {
        StopAnchorMove();

        if (sittingAnchor != null)
        {
            SnapToAnchor(sittingAnchor);
            return;
        }

        if (snapPositionToAnchors)
            transform.position = initialSittingPosition;

        if (snapRotationToAnchors)
            transform.rotation = initialSittingRotation;
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

    private void SnapToLayingAnchor(Transform anchor, float heightAdjustment = 0f, Vector3 positionOffset = default)
    {
        StopAnchorMove();

        if (anchor == null)
            return;

        heldAdditionalPositionOffset = positionOffset;
        ApplyLayingAnchor(anchor, heightAdjustment);
        heldLayingAnchor = anchor;
        heldLayingHeightAdjustment = heightAdjustment;
    }

    private void MoveToLayingDownAnchor()
    {
        StopAnchorMove();

        Transform anchor = GetSharedLayingAnchor();
        if (anchor == null)
            return;

        if (layingDownMoveDuration <= 0f)
        {
            SnapToLayingAnchor(anchor, layingDownExtraHeight, layingDownPositionOffset);
            return;
        }

        heldLayingAnchor = null;
        heldAdditionalPositionOffset = layingDownPositionOffset;
        anchorMoveRoutine = StartCoroutine(MoveToAnchorRoutine(anchor, layingDownMoveDuration, layingDownPositionOffset));
    }

    private Transform GetSharedLayingAnchor()
    {
        if (layingSleepingAnchor != null)
            return layingSleepingAnchor;

        if (layingDownAnchor != null)
            return layingDownAnchor;

        return kickingOutAnchor;
    }

    private System.Collections.IEnumerator MoveToAnchorRoutine(Transform anchor, float duration, Vector3 positionOffset)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 targetPosition = anchor.position;
        targetPosition += layingAnchorPositionOffset + positionOffset;
        targetPosition.y += extraLayingHeight + layingDownExtraHeight;
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
            heldAdditionalPositionOffset = positionOffset;
            ApplyLayingAnchor(anchor, layingDownExtraHeight);
            heldLayingAnchor = anchor;
            heldLayingHeightAdjustment = layingDownExtraHeight;
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
        if (keepCurrentHeightThisFrame)
            return true;

        return layingDownMoveHorizontalOnly && anchor != kickingOutAnchor;
    }

    private void ApplyLayingAnchor(Transform anchor, float heightAdjustment = 0f)
    {
        if (anchor == null)
            return;

        if (snapPositionToAnchors)
        {
            Vector3 targetPosition = anchor.position;
            targetPosition += layingAnchorPositionOffset;
            targetPosition.y += extraLayingHeight + heightAdjustment;
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

    private void StartKickingOutLoop()
    {
        StopKickingOutLoop();
        kickingOutLoopRoutine = StartCoroutine(KickingOutLoopRoutine());
    }

    private void StopKickingOutLoop()
    {
        if (kickingOutLoopRoutine == null)
            return;

        StopCoroutine(kickingOutLoopRoutine);
        kickingOutLoopRoutine = null;
    }

    private IEnumerator KickingOutLoopRoutine()
    {
        float interval = Mathf.Max(0.2f, kickingOutLoopInterval);
        while (true)
        {
            yield return new WaitForSeconds(interval);

            if (heldLayingAnchor != null)
                ApplyLayingAnchor(heldLayingAnchor, heldLayingHeightAdjustment);

            if (restartKickingOutStateDirectly)
                RestartState(kickingOutState, kickingOutStartTimeSeconds);
            else
                PlayState(kickingOutState, kickingOutStartTimeSeconds);
        }
    }
}
