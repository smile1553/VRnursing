using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
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
    [SerializeField] private GameObject kidCombinePoseObject;
    [SerializeField] private string kidCombinePoseObjectNames = "Kid_Combined|Kid_Combined 1|kidtakingbeartemp";
    [SerializeField] private bool hideMainKidWhileCombinedPose = false;

    private Coroutine anchorMoveRoutine;
    private Transform heldLayingAnchor;
    private Vector3 heldAdditionalPositionOffset;
    private float heldLayingHeightAdjustment;
    private Vector3 initialSittingPosition;
    private Quaternion initialSittingRotation;
    private bool keepCurrentHeightThisFrame;
    private Coroutine kickingOutLoopRoutine;
    private PlayableGraph clipGraph;
    private Renderer[] mainKidRenderers;

    private void Awake()
    {
        initialSittingPosition = transform.position;
        initialSittingRotation = transform.rotation;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null && disableRootMotion)
            animator.applyRootMotion = false;

        mainKidRenderers = GetComponentsInChildren<Renderer>(true);
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
        HideKidCombinePoseFallback();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        PlayState(sittingIdleState);
    }

    public void PlaySittingDisbelief()
    {
        StopKickingOutLoop();
        HideKidCombinePoseFallback();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        PlayState(sittingDisbeliefState);
    }

    public void PlaySittingRubbingArm()
    {
        StopKickingOutLoop();
        HideKidCombinePoseFallback();
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
        HideKidCombinePoseFallback();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        if (PlayFirstAvailableStateDirect(kidListenState, "Base Layer.Listen_Kid", "Listen_Kid", "Base Layer.KidArmature|KidListen", "KidArmature|KidListen", "Base Layer.KidArmature|Armature|KidListen", "KidArmature|Armature|KidListen", "Base Layer.KidListen", "KidListen", "Kid Listen", "Kid_Listen", "Base Layer.Kid_Listen"))
            return;

        PlayClipFallback(ref kidListenClip, true, "Listen_Kid", "KidListen", "KidArmature|KidListen", "Kid Listen", "Kid_Listen");
    }


    public void PlayKidCombine()
    {
        StopKickingOutLoop();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        if (PlayFirstAvailableStateDirect(kidCombineState, "kid_combine", "Kid_Combine", "KidCombine", "Kid Combine", "Kid_Combined", "Kid_Combined 1", "Base Layer.kid_combine", "Base Layer.KidCombine", "Base Layer.Kid_Combined"))
            return;

        if (PlayClipFallback(ref kidCombineClip, true, "kid_combine", "Kid_Combine", "KidCombine", "Kid Combine", "Kid_Combined", "Kid_Combined 1"))
            return;

        ShowKidCombinePoseFallback();
    }
    public void PlayKidPointEar()
    {
        StopKickingOutLoop();
        HideKidCombinePoseFallback();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        if (PlayFirstAvailableStateDirect(kidPointEarState, "KidPointEar", "Kid Point Ear", "Base Layer.KidPointEar", "Kid_PointEar", "Base Layer.Kid_PointEar", "Scene", "Base Layer.Scene"))
            return;

        PlayClipFallback(ref kidPointEarClip, true, "KidPointEar", "Kid Point Ear", "Kid_PointEar");
    }
    public void PlayCustom(string stateName)
    {
        StopKickingOutLoop();
        HideKidCombinePoseFallback();
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
    private bool PlayClipFallback(ref AnimationClip assignedClip, bool loop, params string[] clipNames)
    {
        if (animator == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Animator is missing.", this);
            return false;
        }

        if (assignedClip == null)
            assignedClip = FindAnimationClipByName(clipNames);

        if (assignedClip == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] AnimationClip fallback not found. Trying pose fallback if available.", this);
            return false;
        }

        StopClipFallback();
        HideKidCombinePoseFallback();
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
        return true;
    }

    private void ShowKidCombinePoseFallback()
    {
        if (kidCombinePoseObject == null)
            kidCombinePoseObject = FindSceneObjectByNames(kidCombinePoseObjectNames);

        if (kidCombinePoseObject == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Kid combine pose object not found. Add/keep Kid_Combined in the scene or assign Kid Combine Pose Object.", this);
            return;
        }

        StopClipFallback();
        kidCombinePoseObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
        kidCombinePoseObject.SetActive(true);
        SetRenderersVisible(kidCombinePoseObject, true);

        Animator poseAnimator = kidCombinePoseObject.GetComponentInChildren<Animator>(true);
        if (poseAnimator != null)
        {
            poseAnimator.enabled = true;
            poseAnimator.Rebind();
            poseAnimator.Update(0f);
        }

        SetMainKidRenderersVisible(true);
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Show kid combine pose fallback: {kidCombinePoseObject.name}", this);
    }

    private static void SetRenderersVisible(GameObject root, bool visible)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }
    private void HideKidCombinePoseFallback()
    {
        if (kidCombinePoseObject != null)
            kidCombinePoseObject.SetActive(false);

        SetMainKidRenderersVisible(true);
    }

    private void SetMainKidRenderersVisible(bool visible)
    {
        if (mainKidRenderers == null)
            return;

        foreach (Renderer renderer in mainKidRenderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private static GameObject FindSceneObjectByNames(string names)
    {
        if (string.IsNullOrWhiteSpace(names))
            return null;

        string[] splitNames = names.Split('|');
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (string rawName in splitNames)
        {
            string wantedName = rawName.Trim();
            if (string.IsNullOrEmpty(wantedName))
                continue;

            foreach (Transform candidate in transforms)
            {
                if (candidate == null || !candidate.gameObject.scene.IsValid())
                    continue;

                if (string.Equals(candidate.name, wantedName, System.StringComparison.OrdinalIgnoreCase))
                    return candidate.gameObject;
            }
        }

        return null;
    }
    private void StopClipFallback()
    {
        if (clipGraph.IsValid())
            clipGraph.Destroy();
    }

    private static AnimationClip FindAnimationClipByName(params string[] clipNames)
    {
        AnimationClip loadedClip = FindLoadedAnimationClipByName(clipNames);
        if (loadedClip != null)
            return loadedClip;

#if UNITY_EDITOR
        return FindEditorAnimationClipByName(clipNames);
#else
        return null;
#endif
    }

    private static AnimationClip FindLoadedAnimationClipByName(params string[] clipNames)
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

#if UNITY_EDITOR
    private static AnimationClip FindEditorAnimationClipByName(params string[] clipNames)
    {
        foreach (string rawName in clipNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            string[] guids = AssetDatabase.FindAssets($"{rawName} t:AnimationClip");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip != null && clip.name.IndexOf(rawName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return clip;
                }
            }
        }
        return FindEditorAnimationClipByModelName(clipNames);
    }
    private static AnimationClip FindEditorAnimationClipByModelName(params string[] clipNames)
    {
        foreach (string rawName in clipNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            string[] guids = AssetDatabase.FindAssets($"{rawName} t:Model");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip != null && !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                        return clip;
                }
            }
        }

        return null;
    }
#endif
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
