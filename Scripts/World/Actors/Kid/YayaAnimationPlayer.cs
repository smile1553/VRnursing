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
    [Tooltip("Humanoid Avatar used only while a Humanoid clip (e.g. Listen_*.anim) plays. The model itself stays Generic.")]
    [SerializeField] private Avatar humanoidAvatar;
    private Avatar originalAvatar;
    private RuntimeAnimatorController originalController;
    private bool humanoidAvatarActive;
    private SkeletonBone[] restPose;
    private Avatar runtimeHumanoidAvatar;
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
    private AnimationClip currentFallbackClip;
    private Coroutine clipLoopRoutine;
    private Renderer[] mainKidRenderers;

    [Header("Comfort Bear (stroke the teddy with one hand)")]
    [SerializeField] private string comfortBearNames = "bow_bear (1)|bow_bear|bears|bear.002";
    [SerializeField] private float bearStrokeAmplitude = 0.05f;
    [SerializeField] private float bearStrokeSpeed = 0.7f;
    [Tooltip("Only bears within this distance (m) of Yaya are used.")]
    [SerializeField] private float comfortBearMaxDistance = 2.5f;

    [Header("Ear Pain (hand on the ear, head tilted)")]
    [SerializeField] private bool earPainRightSide = true;
    [SerializeField] private float earPainHeadTilt = 14f;
    [SerializeField] private float earPainRubAmplitude = 0.012f;
    private bool earPainActive;
    private float earPainWeight;
    private float earPainStartTime;
    private ArmReachIK.Arm earPainArm;
    private Transform earPainHead;
    private Transform comfortBear;
    private bool bearStrokeActive;
    private float bearStrokeWeight;
    private float bearStrokeStartTime;
    private ArmReachIK.Arm bearArm;

    private void Awake()
    {
        initialSittingPosition = transform.position;
        initialSittingRotation = transform.rotation;

        if (animator == null)
            animator = GetComponent<Animator>();

        // Capture the rest (T) pose before the Animator moves any bone; used to build a
        // Humanoid avatar at runtime if none is assigned.
        if (animator != null)
            restPose = MixamoHumanoidAvatarBuilder.CaptureRestPose(animator.transform);

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

    [Header("Loop one-shot clips until the next action")]
    [SerializeField] private bool loopFinishedStates = true;
    [SerializeField] private string doNotLoopStates = "lying_down|kicking_out|kid_combine|Scene|KidPointEar";

    private void Update()
    {
        if (!loopFinishedStates || animator == null || animator.runtimeAnimatorController == null || animator.IsInTransition(layerIndex))
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);

        // Kicking: the clip is short (about 1.6 s) and is all kicking, so simply play it again
        // from the start each time it ends, for as long as the story has her kicking.
        // (Play, not CrossFade: Unity ignores a cross-fade from a state to itself.)
        if (smoothBedTransitions && Animator.StringToHash(kickingOutState) == info.shortNameHash)
        {
            if (info.normalizedTime >= 1f)
                animator.Play(info.fullPathHash, layerIndex, 0f);
            return;
        }

        if (info.loop || info.normalizedTime < 1f)
            return;

        foreach (string raw in doNotLoopStates.Split('|'))
        {
            if (raw.Length > 0 && Animator.StringToHash(raw) == info.shortNameHash)
                return;
        }

        animator.CrossFadeInFixedTime(info.fullPathHash, 0.3f, layerIndex, 0f);
    }

    private void LateUpdate()
    {
        UpdateBedPose();
        UpdateMeltdown();
        UpdateBearStroke();
        UpdateEarPain();
        UpdateHug();

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
        PlaySitting(0);
    }

    public void PlaySittingDisbelief()
    {
        StopKickingOutLoop();
        HideKidCombinePoseFallback();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        PlaySitting(2);
    }

    public void PlaySittingRubbingArm()
    {
        StopKickingOutLoop();
        HideKidCombinePoseFallback();
        heldLayingAnchor = null;
        heldAdditionalPositionOffset = Vector3.zero;
        SnapToSittingPose();
        PlaySitting(1);
    }

    public void PlayLayingSleeping()
    {
        if (smoothBedTransitions)
        {
            StartLieDown(layingSleepingState, 0f);
            return;
        }

        StopKickingOutLoop();
        StopAnchorMove();
        SnapToLayingAnchor(GetSharedLayingAnchor(), 0f, layingSleepingPositionOffset);
        PlayState(layingSleepingState);
    }

    public void PlayLayingDown()
    {
        // The Mixamo "Lying Down" clip starts from standing, which looked choppy from a sitting
        // pose; the smooth path blends straight from sitting into the sleeping pose instead.
        if (smoothBedTransitions)
        {
            StartLieDown(layingSleepingState, 0f);
            return;
        }

        StopKickingOutLoop();
        MoveToLayingDownAnchor();
        PlayState(layingDownState);
    }

    public void PlayKickingOut()
    {
        if (smoothBedTransitions)
        {
            // Start from the beginning: "Kicking Out Start Time" (2 s) is longer than the whole
            // clip, which made her skip straight to its last frame and just lie there.
            StartLieDown(kickingOutState, 0f);
            return;
        }

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
    // Yaya sits and gently strokes her teddy bear ("小熊這個不會痛的").
    public void PlayComfortBear()
    {
        PlaySittingIdle();

        // Always take the teddy closest to Yaya (other bears may exist in other rooms).
        comfortBear = FindNearestBear();

        if (comfortBear == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Teddy bear not found for comfort stroke. Check 'Comfort Bear Names'.", this);
            return;
        }

        bearArm = default;
        bearStrokeActive = true;
        bearStrokeStartTime = Time.time;
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Comfort bear: {comfortBear.name}", this);
    }

    // ---------------------------------------------------------------------------------
    // Hug bear: Yaya reaches for her teddy, lifts it to her chest and hugs it (rocking gently).
    // The bear goes back to where it was when Yaya does something else.
    // ---------------------------------------------------------------------------------
    [Header("Hug Bear")]
    [SerializeField] private float hugReachTime = 0.6f;
    [SerializeField] private float hugLiftTime = 0.8f;
    [SerializeField] private float hugRockAngle = 6f;
    private Transform hugBear;
    private bool hugActive;
    private float hugStartTime;
    private float hugWeight;
    private Vector3 hugBearOrigPosition;
    private Quaternion hugBearOrigRotation;
    private Vector3 hugBearCenterOffset;
    private float hugBearHalfWidth;
    private Vector3 hugBearSize;
    private bool hugLoggedArrival;
    [Tooltip("How high the bear arcs on its way from the bed into her arms (m).")]
    [SerializeField] private float hugLiftArc = 0.10f;
    [SerializeField] private float hugHeadTilt = 14f;
    private ArmReachIK.Arm hugLeftArm;
    private ArmReachIK.Arm hugRightArm;
    private Coroutine hugReturnRoutine;

    public void PlayHugBear()
    {
        if (hugActive && hugBear != null)
            return; // already hugging: keep going through the next line

        PlaySittingIdle();

        if (hugReturnRoutine != null)
        {
            // The bear was still on its way back: put it home first so "home" stays the bed.
            StopCoroutine(hugReturnRoutine);
            hugReturnRoutine = null;
            if (hugBear != null)
                hugBear.SetPositionAndRotation(hugBearOrigPosition, hugBearOrigRotation);
        }

        Transform bear = FindNearestBear();
        if (bear == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Teddy bear not found for hug.", this);
            return;
        }

        hugBear = bear;
        hugBearOrigPosition = bear.position;
        hugBearOrigRotation = bear.rotation;
        Bounds b = GetBounds(bear);
        hugBearCenterOffset = b.center - bear.position;
        hugBearSize = b.size;
        hugBearHalfWidth = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.75f, 0.03f, 0.2f);
        hugLeftArm = ArmReachIK.FindArm(transform, false);
        hugRightArm = ArmReachIK.FindArm(transform, true);
        hugStartTime = Time.time;
        hugActive = true;
        hugLoggedArrival = false;
        Debug.Log($"[YayaAnimationPlayer] Hug bear: '{GetPath(bear)}' at {b.center:F2}, size {b.size:F2}, " +
                  $"{Vector3.Distance(b.center, GetBodyCenter()):0.00} m from Yaya, arms ok={hugLeftArm.IsValid && hugRightArm.IsValid}", bear);
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }

    // Middle of Yaya's body (hips), not the model pivot, which can be far from the visible body.
    private Vector3 GetBodyCenter()
    {
        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(transform, "Hips");
        return hips != null ? hips.position : transform.position;
    }

    private void EndHug()
    {
        if (!hugActive)
            return;

        hugActive = false;
        if (hugBear != null && isActiveAndEnabled)
            hugReturnRoutine = StartCoroutine(ReturnBearRoutine(hugBear, hugBearOrigPosition, hugBearOrigRotation));
    }

    private IEnumerator ReturnBearRoutine(Transform bear, Vector3 position, Quaternion rotation)
    {
        Vector3 fromPosition = bear.position;
        Quaternion fromRotation = bear.rotation;
        for (float t = 0f; t < 0.45f && bear != null; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
            bear.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, k), Quaternion.Slerp(fromRotation, rotation, k));
            yield return null;
        }
        if (bear != null)
            bear.SetPositionAndRotation(position, rotation);
        hugReturnRoutine = null;
    }

    private void UpdateHug()
    {
        hugWeight = Mathf.MoveTowards(hugWeight, hugActive ? 1f : 0f, Time.deltaTime / (hugActive ? hugReachTime : 0.4f));
        if (hugWeight <= 0f || hugBear == null)
            return;

        Vector3 forward = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
        if (forward.sqrMagnitude < 1e-6f)
            forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Transform chestBone = MixamoHumanoidAvatarBuilder.FindBone(transform, "Spine2")
            ?? MixamoHumanoidAvatarBuilder.FindBone(transform, "Spine1")
            ?? MixamoHumanoidAvatarBuilder.FindBone(transform, "Spine");
        Transform head = MixamoHumanoidAvatarBuilder.FindBone(transform, "Head");
        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(transform, "Hips");
        float torso = hips != null && head != null ? Vector3.Distance(hips.position, head.position) : 0.4f;
        float lift = 0f;

        if (hugActive)
        {
            // 1) arms reach to the bear on the bed, 2) the bear is lifted in an arc onto her lap,
            // against her chest, 3) she rocks it gently.
            float t = Time.time - hugStartTime;
            lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - hugReachTime) / Mathf.Max(0.01f, hugLiftTime)));

            Vector3 chestPoint = chestBone != null ? chestBone.position : GetBodyCenter() + Vector3.up * torso * 0.5f;
            float bearDepth = Mathf.Clamp(Mathf.Min(hugBearSize.x, hugBearSize.z) * 0.5f, 0.02f, 0.12f);
            Vector3 held = chestPoint + forward * (torso * 0.20f + bearDepth * 0.6f);
            // Bear sits on her lap: its bottom above the thighs, its body in front of the chest.
            float lapY = (hips != null ? hips.position.y : chestPoint.y - torso * 0.5f) + torso * 0.15f;
            held.y = Mathf.Max(chestPoint.y - torso * 0.12f, lapY + hugBearSize.y * 0.5f);

            Vector3 from = hugBearOrigPosition + hugBearCenterOffset;
            Vector3 center = Vector3.Lerp(from, held, lift) + Vector3.up * (Mathf.Sin(lift * Mathf.PI) * hugLiftArc);
            float rock = lift * Mathf.Sin(t * Mathf.PI * 2f * 0.45f) * hugRockAngle;
            Quaternion spin = Quaternion.AngleAxis(rock, forward);
            hugBear.SetPositionAndRotation(center - spin * hugBearCenterOffset, spin * hugBearOrigRotation);

            if (!hugLoggedArrival && lift >= 1f)
            {
                hugLoggedArrival = true;
                Debug.Log($"[YayaAnimationPlayer] Bear is in Yaya's arms: moved {Vector3.Distance(from, held):0.00} m (from {from:F2} to {held:F2}).", hugBear);
            }

            // Looks down at the bear.
            if (head != null)
                head.rotation = Quaternion.AngleAxis(hugHeadTilt * lift * hugWeight, right) * head.rotation;
        }

        if (!hugLeftArm.IsValid || !hugRightArm.IsValid)
            return;

        Vector3 bearCenter = hugBear.position + hugBear.rotation * Quaternion.Inverse(hugBearOrigRotation) * hugBearCenterOffset;
        // Hands on the bear's sides, slightly to the front so the arms wrap around it.
        Vector3 wrap = forward * (hugBearHalfWidth * 0.35f * lift);
        ArmReachIK.Solve(hugLeftArm, bearCenter - right * hugBearHalfWidth + wrap, hugWeight);
        ArmReachIK.Solve(hugRightArm, bearCenter + right * hugBearHalfWidth + wrap, hugWeight);
    }

    private static Bounds GetBounds(Transform root)
    {
        Bounds bounds = new Bounds(root.position, Vector3.zero);
        bool has = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled) continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    // ---------------------------------------------------------------------------------
    // Smooth bed transitions: sitting <-> lying is one continuous blend (pose cross-fade +
    // root glide along a small arc), and every frame the back/head are kept just above the
    // bed surface (found with raycasts), so Yaya never sinks into the bed or floats.
    // ---------------------------------------------------------------------------------
    [Header("Smooth Bed Transitions (sit <-> lie, no clipping)")]
    [SerializeField] private bool smoothBedTransitions = true;
    [SerializeField] private float lieDownDuration = 1.6f;
    [SerializeField] private float sitUpDuration = 1.3f;
    [Tooltip("Distance (m) kept between back/head bones and the bed surface.")]
    [SerializeField] private float bedClearance = 0.06f;
    [SerializeField] private LayerMask bedLayers = ~0;
    private bool lyingMode;
    private Vector3 bedBasePosition;
    private Quaternion bedBaseRotation;
    private float bedLift;
    private Coroutine bedRoutine;
    private float nextFadeOverride;
    private Transform[] bedContactBones;

    private void StartLieDown(string stateName, float stateOffset)
    {
        StopKickingOutLoop();
        StopAnchorMove();
        heldLayingAnchor = null;

        Transform anchor = GetSharedLayingAnchor();
        if (anchor == null)
        {
            PlayState(stateName, stateOffset);
            return;
        }

        Vector3 targetPosition = anchor.position + layingAnchorPositionOffset + layingSleepingPositionOffset;
        targetPosition.y += extraLayingHeight;
        Quaternion targetRotation = layingDownRotateYawOnly
            ? Quaternion.Euler(0f, anchor.eulerAngles.y + layingAnchorEulerOffset.y, 0f)
            : anchor.rotation * Quaternion.Euler(layingAnchorEulerOffset);

        bool wasLying = lyingMode;
        if (!wasLying)
        {
            bedBasePosition = transform.position;
            bedBaseRotation = transform.rotation;
            bedLift = 0f;
            lyingMode = true;
            if (bedRoutine != null)
                StopCoroutine(bedRoutine);
            bedRoutine = StartCoroutine(BedTweenRoutine(targetPosition, targetRotation, lieDownDuration, true));
            nextFadeOverride = lieDownDuration * 0.85f;
        }

        PlayState(stateName, stateOffset);
    }

    private void StartSitUp()
    {
        Vector3 targetPosition = initialSittingPosition;
        Quaternion targetRotation = initialSittingRotation;
        if (sittingAnchor != null)
        {
            Vector3 offset = sittingAnchorPositionOffset;
            if (offset.y < 0f) offset.y = 0f;
            targetPosition = sittingAnchor.position + offset;
            targetRotation = sittingAnchor.rotation;
        }

        if (bedRoutine != null)
            StopCoroutine(bedRoutine);
        bedRoutine = StartCoroutine(BedTweenRoutine(targetPosition, targetRotation, sitUpDuration, false));
        nextFadeOverride = sitUpDuration * 0.85f;
    }

    private IEnumerator BedTweenRoutine(Vector3 targetPosition, Quaternion targetRotation, float duration, bool lyingDown)
    {
        Vector3 fromPosition = bedBasePosition;
        Quaternion fromRotation = bedBaseRotation;
        float fromLift = bedLift;
        duration = Mathf.Max(0.05f, duration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            float arc = Mathf.Sin(k * Mathf.PI) * 0.06f; // small lift over the bed edge
            bedBasePosition = Vector3.Lerp(fromPosition, targetPosition, k) + Vector3.up * arc;
            bedBaseRotation = Quaternion.Slerp(fromRotation, targetRotation, k);
            if (!lyingDown)
                bedLift = Mathf.Lerp(fromLift, 0f, k);
            yield return null;
        }

        bedBasePosition = targetPosition;
        bedBaseRotation = targetRotation;
        if (!lyingDown)
        {
            lyingMode = false;
            bedLift = 0f;
            transform.SetPositionAndRotation(targetPosition, targetRotation);
        }
        bedRoutine = null;
    }

    private void UpdateBedPose()
    {
        if (!smoothBedTransitions || !lyingMode)
            return;

        transform.SetPositionAndRotation(bedBasePosition + Vector3.up * bedLift, bedBaseRotation);

        if (bedContactBones == null)
        {
            bedContactBones = new[]
            {
                MixamoHumanoidAvatarBuilder.FindBone(transform, "Hips"),
                MixamoHumanoidAvatarBuilder.FindBone(transform, "Spine1"),
                MixamoHumanoidAvatarBuilder.FindBone(transform, "Spine2"),
                MixamoHumanoidAvatarBuilder.FindBone(transform, "Head")
            };
        }

        float maxNeed = float.NegativeInfinity;
        foreach (Transform bone in bedContactBones)
        {
            if (bone == null)
                continue;
            if (!TryGetBedSurface(bone.position, out float surfaceY))
                continue;
            maxNeed = Mathf.Max(maxNeed, surfaceY + bedClearance - bone.position.y);
        }

        if (float.IsNegativeInfinity(maxNeed))
            return;

        // Clipping: rise quickly. Floating: settle down slowly.
        float target = bedLift + maxNeed;
        float rate = maxNeed > 0f ? 2f : 0.35f;
        bedLift = Mathf.MoveTowards(bedLift, target, rate * Time.deltaTime);
        transform.position = bedBasePosition + Vector3.up * bedLift;
    }

    private bool TryGetBedSurface(Vector3 point, out float surfaceY)
    {
        surfaceY = 0f;
        bool found = false;
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 0.5f, Vector3.down, 1.4f, bedLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;
            if (hit.point.y > point.y + 0.3f)
                continue; // something above the body (e.g. a shelf)
            if (!found || hit.point.y > surfaceY)
            {
                surfaceY = hit.point.y;
                found = true;
            }
        }
        return found;
    }

    // ---------------------------------------------------------------------------------
    // Emotion-driven sitting pose (0 Calm, 1 Uneasy, 2 Crying, 3 Meltdown).
    // The story still decides what Yaya does; while she is just sitting and waiting, her
    // pose is never calmer than her current emotion: Uneasy = rubbing arm, Crying = hands on
    // face, Meltdown = hands on face + rocking / kicking (procedural).
    // ---------------------------------------------------------------------------------
    [Header("Emotion-driven sitting pose")]
    [SerializeField] private bool emotionDrivenIdle = true;
    [SerializeField] private float meltdownRockAngle = 5f;
    [SerializeField] private float meltdownKickAngle = 22f;
    private int emotionLevel;
    private int pendingSittingSeverity = -1;
    private int sittingRequestSeverity = -1;
    private bool emotionRefresh;
    private float meltdownWeight;
    private Transform meltSpine, meltHead, meltLeftKnee, meltRightKnee;

    public int EmotionLevel => emotionLevel;

    // How upset the STORY wants Yaya to be right now (0 calm .. 3 kicking on the bed).
    // This is what the story asked for, not the pose the emotion system swapped in.
    public int StoryRequestedUpsetLevel
    {
        get
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                AnimatorStateInfo info = animator.IsInTransition(layerIndex)
                    ? animator.GetNextAnimatorStateInfo(layerIndex)
                    : animator.GetCurrentAnimatorStateInfo(layerIndex);
                if (info.shortNameHash == Animator.StringToHash(kickingOutState))
                    return 3;
            }

            int level = Mathf.Max(0, sittingRequestSeverity);
            if (earPainActive)
                level = Mathf.Max(level, 1);
            return level;
        }
    }

    public void SetEmotionLevel(int level)
    {
        level = Mathf.Clamp(level, 0, 3);
        if (level == emotionLevel)
            return;

        emotionLevel = level;
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Emotion level {level}", this);

        // Only re-pose when she is in a plain sitting wait state.
        if (emotionDrivenIdle && sittingRequestSeverity >= 0 && !lyingMode)
        {
            emotionRefresh = true;
            PlaySitting(sittingRequestSeverity);
            emotionRefresh = false;
        }
    }

    private void PlaySitting(int severity)
    {
        int effective = emotionDrivenIdle ? Mathf.Max(severity, Mathf.Min(emotionLevel, 2)) : severity;
        pendingSittingSeverity = severity;
        PlayState(effective >= 2 ? sittingDisbeliefState : effective == 1 ? sittingRubbingArmState : sittingIdleState);
    }

    private void UpdateMeltdown()
    {
        bool active = emotionDrivenIdle && emotionLevel >= 3 && sittingRequestSeverity >= 0 && !lyingMode;
        meltdownWeight = Mathf.MoveTowards(meltdownWeight, active ? 1f : 0f, Time.deltaTime / 0.5f);
        if (meltdownWeight <= 0f)
            return;

        if (meltSpine == null)
        {
            meltSpine = MixamoHumanoidAvatarBuilder.FindBone(transform, "Spine1");
            meltHead = MixamoHumanoidAvatarBuilder.FindBone(transform, "Head");
            meltLeftKnee = MixamoHumanoidAvatarBuilder.FindBone(transform, "LeftLeg");
            meltRightKnee = MixamoHumanoidAvatarBuilder.FindBone(transform, "RightLeg");
        }

        Vector3 forward = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float t = Time.time;
        float w = meltdownWeight;
        if (meltSpine != null)
            meltSpine.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 7f) * meltdownRockAngle * w, right) * meltSpine.rotation;
        if (meltHead != null)
            meltHead.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 9f) * 9f * w, Vector3.up) * meltHead.rotation;
        if (meltLeftKnee != null)
            meltLeftKnee.rotation = Quaternion.AngleAxis(-Mathf.Max(0f, Mathf.Sin(t * 10f)) * meltdownKickAngle * w, right) * meltLeftKnee.rotation;
        if (meltRightKnee != null)
            meltRightKnee.rotation = Quaternion.AngleAxis(-Mathf.Max(0f, Mathf.Sin(t * 10f + Mathf.PI)) * meltdownKickAngle * w, right) * meltRightKnee.rotation;
    }

    private void UpdateBearStroke()
    {
        bearStrokeWeight = Mathf.MoveTowards(bearStrokeWeight, bearStrokeActive ? 1f : 0f, Time.deltaTime / 0.35f);
        if (bearStrokeWeight <= 0f || comfortBear == null)
            return;

        Vector3 top = comfortBear.position;
        Renderer[] renderers = comfortBear.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = new Bounds(top, Vector3.zero);
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (hasBounds)
            top = bounds.center + Vector3.up * bounds.extents.y * 0.75f;

        Vector3 axis = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
        float stroke = Mathf.Sin((Time.time - bearStrokeStartTime) * Mathf.PI * 2f * bearStrokeSpeed) * bearStrokeAmplitude;
        Vector3 target = top + axis * stroke;

        if (!bearArm.IsValid)
            bearArm = ArmReachIK.FindClosestArm(transform, target);
        ArmReachIK.Solve(bearArm, target, bearStrokeWeight);
    }

    // "芽芽的耳朵痛痛": Yaya holds her ear with one hand, head tilted toward it, rubbing gently.
    public void PlayEarPain()
    {
        PlaySittingIdle();
        earPainHead = MixamoHumanoidAvatarBuilder.FindBone(transform, "Head");
        earPainArm = ArmReachIK.FindArm(transform, earPainRightSide);
        earPainActive = earPainHead != null && earPainArm.IsValid;
        earPainStartTime = Time.time;
        if (logEvents)
            Debug.Log("[YayaAnimationPlayer] Ear pain pose", this);
    }

    private void UpdateEarPain()
    {
        earPainWeight = Mathf.MoveTowards(earPainWeight, earPainActive ? 1f : 0f, Time.deltaTime / 0.4f);
        if (earPainWeight <= 0f || earPainHead == null || !earPainArm.IsValid)
            return;

        Vector3 forward = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 side = earPainRightSide ? right : -right;

        // Tilt the head toward the sore ear.
        float tilt = earPainHeadTilt * (earPainRightSide ? 1f : -1f) * earPainWeight;
        earPainHead.rotation = Quaternion.AngleAxis(tilt, forward) * earPainHead.rotation;

        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(transform, "Hips");
        float headSize = hips != null ? Vector3.Distance(hips.position, earPainHead.position) * 0.22f : 0.07f;
        float t = (Time.time - earPainStartTime) * Mathf.PI * 2f * 0.6f;
        Vector3 rub = (Vector3.up * Mathf.Sin(t) + forward * Mathf.Cos(t)) * earPainRubAmplitude;
        Vector3 ear = earPainHead.position + side * headSize + Vector3.up * headSize * 0.6f + rub;
        ArmReachIK.Solve(earPainArm, ear + side * headSize * 0.35f, earPainWeight);
    }

    // The teddy on the bed next to Yaya: the closest visible object whose name contains "bear"
    // (or is listed in Comfort Bear Names). Distances are measured between what you SEE
    // (mesh centre and Yaya's hips), because model pivots can be far away from the mesh.
    private Transform FindNearestBear()
    {
        Transform best = null;
        float bestDistance = comfortBearMaxDistance;
        float nearestSeen = float.MaxValue;
        string nearestSeenName = null;
        Vector3 body = GetBodyCenter();
        string[] wanted = string.IsNullOrWhiteSpace(comfortBearNames) ? new string[0] : comfortBearNames.Split('|');
        foreach (Renderer r in FindObjectsOfType<Renderer>())
        {
            if (r == null || !r.enabled || r.transform.IsChildOf(transform))
                continue;

            // Top-most ancestor with a bear name, so the whole toy moves (body, bow, sticker...).
            Transform candidate = null;
            for (Transform t = r.transform; t != null; t = t.parent)
            {
                string n = t.name;
                bool named = n.IndexOf("bear", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("sticker", System.StringComparison.OrdinalIgnoreCase) < 0;
                foreach (string w in wanted)
                    named |= w.Trim().Length > 0 && n == w.Trim();
                if (named)
                    candidate = t;
                else if (candidate != null)
                    break;
            }
            if (candidate == null)
                continue;

            // Not a bear that another character model is holding (e.g. the Kid_Combined pose model).
            Animator owner = candidate.parent != null ? candidate.parent.GetComponentInParent<Animator>() : null;
            if (owner != null && owner.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                continue;

            float d = Vector3.Distance(r.bounds.center, body);
            if (d < nearestSeen)
            {
                nearestSeen = d;
                nearestSeenName = candidate.name;
            }
            if (d < bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }

        if (best == null)
        {
            Debug.LogWarning(nearestSeenName != null
                ? $"[YayaAnimationPlayer] Nearest bear '{nearestSeenName}' is {nearestSeen:0.00} m away, more than Comfort Bear Max Distance ({comfortBearMaxDistance:0.00})."
                : "[YayaAnimationPlayer] No visible object with 'bear' in its name was found.", this);
        }
        return best;
    }

    private static Transform FindVisibleSceneTransform(string names)
    {
        if (string.IsNullOrWhiteSpace(names))
            return null;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (string raw in names.Split('|'))
        {
            string wanted = raw.Trim();
            if (wanted.Length == 0)
                continue;
            foreach (Transform t in all)
            {
                if (t != null && t.gameObject.scene.IsValid() && t.gameObject.activeInHierarchy && t.name == wanted)
                    return t;
            }
        }
        return null;
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
        sittingRequestSeverity = pendingSittingSeverity;
        pendingSittingSeverity = -1;
        if (!emotionRefresh)
        {
            bearStrokeActive = false;
            earPainActive = false;
            EndHug();
        }
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

        if (fixedTimeOffset <= 0f && IsAlreadyPlaying(stateHash))
            return;

        StopClipFallback();
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Play animation: {stateName}", this);

        float fade = nextFadeOverride > 0f ? nextFadeOverride : fadeDuration;
        nextFadeOverride = 0f;
        animator.CrossFadeInFixedTime(stateHash, fade, layerIndex, Mathf.Max(0f, fixedTimeOffset));
    }

    // True when `hash` is already the state playing (or being faded to), so calling it again
    // would only restart it from the beginning (e.g. the crying loop being cut off every 2 s).
    private bool IsAlreadyPlaying(int hash)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        if (animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layerIndex);
            return next.shortNameHash == hash || next.fullPathHash == hash;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (current.shortNameHash != hash && current.fullPathHash != hash)
            return false;

        return current.loop || current.normalizedTime < 1f;
    }

    private void RestartState(string stateName, float fixedTimeOffset)
    {
        sittingRequestSeverity = -1;
        bearStrokeActive = false;
        earPainActive = false;
        EndHug();
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
        sittingRequestSeverity = -1;
        bearStrokeActive = false;
        earPainActive = false;
        EndHug();
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
        sittingRequestSeverity = -1;
        bearStrokeActive = false;
        earPainActive = false;
        EndHug();
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

        // Already looping this clip: keep playing instead of snapping back to frame 0.
        if (loop && currentFallbackClip == assignedClip && clipGraph.IsValid())
            return true;

        StopClipFallback();
        if (!EnsureAvatarForClip(assignedClip))
            return false;
        HideKidCombinePoseFallback();
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Play clip fallback: {assignedClip.name}", this);

        clipGraph = PlayableGraph.Create($"{name}_{assignedClip.name}_Fallback");
        AnimationClipPlayable playable = AnimationClipPlayable.Create(clipGraph, assignedClip);
        playable.SetApplyFootIK(false);
        if (loop)
        {
            assignedClip.wrapMode = WrapMode.Loop;
            playable.SetDuration(double.PositiveInfinity);
        }
        else
        {
            playable.SetDuration(assignedClip.length);
        }
        playable.SetTime(0d);
        playable.SetSpeed(1d);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(clipGraph, "Animation", animator);
        output.SetSourcePlayable(playable);
        clipGraph.Play();
        currentFallbackClip = assignedClip;
        if (loop)
            clipLoopRoutine = StartCoroutine(LoopClipPlayable(playable, assignedClip));
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
        if (clipLoopRoutine != null)
        {
            StopCoroutine(clipLoopRoutine);
            clipLoopRoutine = null;
        }

        if (clipGraph.IsValid())
            clipGraph.Destroy();
        currentFallbackClip = null;

        RestoreOriginalAvatar();
    }

    // Humanoid clips (muscle curves) do nothing on a Generic avatar, so swap in a
    // Humanoid avatar built from the same model only while such a clip plays.
    private bool EnsureAvatarForClip(AnimationClip clip)
    {
        if (clip == null || !clip.humanMotion)
            return true;

        if (animator.avatar != null && animator.avatar.isHuman)
            return true;

        Avatar avatarToUse = humanoidAvatar;
        if (avatarToUse == null || !avatarToUse.isHuman || !avatarToUse.isValid)
        {
            if (runtimeHumanoidAvatar == null)
                runtimeHumanoidAvatar = MixamoHumanoidAvatarBuilder.Build(animator.gameObject, restPose);
            avatarToUse = runtimeHumanoidAvatar;
        }

        if (avatarToUse == null)
        {
            Debug.LogWarning($"[YayaAnimationPlayer] {clip.name} is a Humanoid clip but no Humanoid avatar is available (runtime build failed).", this);
            return false;
        }

        originalAvatar = animator.avatar;
        originalController = animator.runtimeAnimatorController;
        // Detach the Generic state machine so it cannot fight the humanoid clip.
        animator.runtimeAnimatorController = null;
        animator.avatar = avatarToUse;
        animator.Rebind();
        humanoidAvatarActive = true;
        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Using humanoid avatar {avatarToUse.name} for {clip.name}", this);
        return true;
    }

    private void RestoreOriginalAvatar()
    {
        if (!humanoidAvatarActive)
            return;

        humanoidAvatarActive = false;
        if (animator == null)
            return;

        animator.avatar = originalAvatar;
        animator.runtimeAnimatorController = originalController;
        originalAvatar = null;
        originalController = null;
        animator.Rebind();
    }

    private IEnumerator LoopClipPlayable(AnimationClipPlayable playable, AnimationClip clip)
    {
        while (clipGraph.IsValid() && playable.IsValid() && clip != null && clip.length > 0.001f)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, clip.length - 0.02f));
            if (!clipGraph.IsValid() || !playable.IsValid())
                break;

            playable.SetTime(0d);
            playable.SetSpeed(1d);
        }

        clipLoopRoutine = null;
    }
    private static AnimationClip FindAnimationClipByName(params string[] clipNames)
    {
#if UNITY_EDITOR
        AnimationClip editorClip = FindEditorAnimationClipByName(clipNames);
        if (editorClip != null)
            return editorClip;
#endif

        return FindLoadedAnimationClipByName(clipNames);
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
        AnimationClip preferredClip = FindEditorAnimationClipAtPreferredPath(clipNames);
        if (preferredClip != null)
            return preferredClip;

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

    private static AnimationClip FindEditorAnimationClipAtPreferredPath(params string[] clipNames)
    {
        foreach (string rawName in clipNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            string path = null;
            if (rawName.IndexOf("Listen_Mom", System.StringComparison.OrdinalIgnoreCase) >= 0)
                path = "Assets/ForGan/Clips/Listen_Mom.anim";
            else if (rawName.IndexOf("Listen_Kid", System.StringComparison.OrdinalIgnoreCase) >= 0)
                path = "Assets/ForGan/Clips/Listen_Kid.anim";

            if (string.IsNullOrEmpty(path))
                continue;

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
                return clip;
        }

        return null;
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
        if (smoothBedTransitions && lyingMode)
        {
            heldLayingAnchor = null;
            StartSitUp();
            return;
        }

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
