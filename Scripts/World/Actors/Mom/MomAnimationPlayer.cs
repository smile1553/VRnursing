using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Animations;
using UnityEngine.Playables;

public class MomAnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private bool logEvents = true;

    [Header("Animator State Names")]
    [SerializeField] private string angryState = "Angry";
    [SerializeField] private string clappingState = "Clapping";
    [SerializeField] private string walkStartState = "Female Start Walking";
    [SerializeField] private string standingIdleState = "MomStanding Idle";
    [SerializeField] private string pointingState = "Pointing";
    [SerializeField] private string quickBowState = "Quick Informal Bow";
    [SerializeField] private string talkingState = "Talking";
    [SerializeField] private string bendState = "Armature|MomBend";
    [SerializeField] private string comfortState = "Petting Animal";

    [Header("Comfort / Walk")]
    [Tooltip("Horizontal distance (m) from Yaya's head to Mom when comforting.")]
    [SerializeField] private float comfortApproachDistance = 0.38f;
    [SerializeField] private float comfortMoveDuration = 0.6f;
    [SerializeField] private float comfortPatAmplitude = 0.025f;
    [SerializeField] private float comfortPatSpeed = 1.6f;
    [SerializeField] private bool returnAfterComfort = true;
    [SerializeField] private float walkSpeed = 0.7f;
    private bool comfortActive;
    private bool suppressComfortEnd;
    private float comfortWeight;
    private float comfortStartTime;
    private float comfortHeadSize;
    private Transform comfortHead;
    private ArmReachIK.Arm comfortArm;
    private bool displacedFromHome;
    private Vector3 homePosition;
    private Vector3 homeForward;
    private bool isWalking;
    private float walkHeartbeat;
    private string[] pendingStates;
    private Coroutine moveRoutine;
    private Transform hipsBone;
    private bool pinHipsWhileWalking;
    private Vector3 pinnedHipsLocal;
    private bool hasStartHome;
    private Vector3 startHomePosition;
    private Vector3 startHomeForward;
    private Coroutine walkRoutine;
    private float hipsPinWeight;

    [Header("Clap Assist (make the palms really meet)")]
    [SerializeField] private bool clapAssist = true;
    [Tooltip("Palm-center distance (m) at the moment of the clap.")]
    [SerializeField] private float clapContactGap = 0.035f;
    private bool clapAssistActive;
    private float clapAssistWeight;
    private float clapMinGap = float.MaxValue;
    private ArmReachIK.Arm clapLeftArm;
    private ArmReachIK.Arm clapRightArm;
    private Transform clapLeftPalm;
    private Transform clapRightPalm;

    [Header("Clip Fallbacks")]
    [SerializeField] private AnimationClip momBendClip;
    [Tooltip("Humanoid Avatar used only while a Humanoid clip (e.g. Listen_*.anim) plays. The model itself stays Generic.")]
    [SerializeField] private Avatar humanoidAvatar;
    private Avatar originalAvatar;
    private RuntimeAnimatorController originalController;
    private bool humanoidAvatarActive;
    private SkeletonBone[] restPose;
    private Avatar runtimeHumanoidAvatar;

    private PlayableGraph clipGraph;
    private AnimationClip currentFallbackClip;
    private Coroutine clipLoopRoutine;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        // Capture the rest (T) pose before the Animator moves any bone; used to build a
        // Humanoid avatar at runtime if none is assigned.
        if (animator != null)
            restPose = MixamoHumanoidAvatarBuilder.CaptureRestPose(animator.transform);
    }

    private void OnDisable()
    {
        StopClipFallback();
    }

    private void OnDestroy()
    {
        StopClipFallback();
    }

    // ---- Read-only state for MomSkinSwap (the new-look Mom follows what this Mom is doing) ----
    public Animator BodyAnimator => animator;
    public AnimationClip ActiveFallbackClip => clipGraph.IsValid() ? currentFallbackClip : null;
    public bool IsComfortPatting => comfortActive && comfortHead != null && !IsWalkingNow();
    public Transform ComfortHead => comfortHead;
    public float ComfortHeadSize => comfortHeadSize;
    public bool IsWalking => IsWalkingNow();

    // The direction Mom is meant to face (set when she is placed / walks somewhere), without
    // the body twist that individual animation clips add.
    private Vector3 intendedForward;
    public Vector3 IntendedForward
    {
        get
        {
            if (intendedForward.sqrMagnitude < 1e-6f)
            {
                intendedForward = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
                intendedForward.y = 0f;
                if (intendedForward.sqrMagnitude < 1e-6f)
                    intendedForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                intendedForward.Normalize();
            }
            return intendedForward;
        }
    }

    // While one of Mom's own lines is on screen: who she is talking to (Yaya or the nurse).
    public bool TryGetTalkFocus(out Vector3 position)
    {
        position = Vector3.zero;
        if (!momSpeaking || string.IsNullOrEmpty(momLine))
            return false;
        Transform target = ResolveTalkTarget();
        if (target == null)
            return false;
        position = target.position;
        return true;
    }

    public void PlayAngry()
    {
        PlayState(angryState);
    }

    public void PlayClapping()
    {
        PlayFirstAvailableState(clappingState, "Base Layer.Clapping", "Clapping");
    }

    public void PlayWalkStart()
    {
        PlayState(walkStartState);
    }

    public void PlayStandingIdle()
    {
        PlayState(standingIdleState);
    }

    public void PlayPointing()
    {
        PlayState(pointingState);
    }

    public void PlayQuickBow()
    {
        PlayState(quickBowState);
    }

    // Comforting gesture (gentle patting) for lines like "芽芽妳別怕 媽媽在這裡陪妳".
    public void PlayComfort()
    {
        if (!PlayFirstAvailableState(comfortState, "Petting Animal", "Base Layer.Petting Animal"))
            PlayState(talkingState);
    }

    // ---------------------------------------------------------------------------------
    // Comfort: Mom walks to Yaya (walk animation, no sliding), then pats her head (arm IK).
    // ---------------------------------------------------------------------------------
    public void PlayComfortToward(Transform yaya)
    {
        if (yaya == null)
        {
            PlayComfort();
            return;
        }

        Transform head = MixamoHumanoidAvatarBuilder.FindBone(yaya, "Head");
        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(yaya, "Hips");
        if (head == null)
        {
            PlayComfort();
            return;
        }

        if (!displacedFromHome)
        {
            homePosition = transform.position;
            homeForward = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
            displacedFromHome = true;
        }

        Vector3 toKid = Vector3.ProjectOnPlane(head.position - transform.position, Vector3.up);
        if (toKid.sqrMagnitude < 1e-6f)
            toKid = transform.forward;
        Vector3 dir = toKid.normalized;
        Vector3 targetPosition = transform.position;
        if (toKid.magnitude > comfortApproachDistance)
            targetPosition = FindFreeComfortSpot(yaya, head, transform.position);
        dir = Vector3.ProjectOnPlane(head.position - targetPosition, Vector3.up).normalized;

        comfortHead = head;
        comfortHeadSize = hips != null ? Vector3.Distance(hips.position, head.position) * 0.28f : 0.08f;
        comfortArm = default;
        comfortActive = true;
        comfortStartTime = Time.time;
        StartWalk(targetPosition, dir, false);
        if (logEvents)
            Debug.Log($"[MomAnimationPlayer] Comfort toward {yaya.name}", this);
    }

    [Header("Comfort: avoid clipping")]
    [Tooltip("Mom's body radius (m) used to find a free spot next to Yaya.")]
    [SerializeField] private float bodyRadius = 0.2f;
    [Tooltip("Minimum horizontal distance (m) between Mom and Yaya's hips / legs / feet.")]
    [SerializeField] private float minDistanceFromYaya = 0.3f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    // Spot around Yaya's head at comfort distance where Mom's body does not overlap the bed,
    // furniture or Yaya, preferring spots reachable in a straight line and close to Mom.
    private Vector3 FindFreeComfortSpot(Transform yaya, Transform head, Vector3 from)
    {
        Vector3 headFlat = new Vector3(head.position.x, from.y, head.position.z);
        Vector3 awayFromKid = Vector3.ProjectOnPlane(from - headFlat, Vector3.up);
        if (awayFromKid.sqrMagnitude < 1e-6f)
            awayFromKid = -transform.forward;
        awayFromKid.Normalize();

        Transform[] kidBones =
        {
            MixamoHumanoidAvatarBuilder.FindBone(yaya, "Hips"),
            MixamoHumanoidAvatarBuilder.FindBone(yaya, "LeftLeg"),
            MixamoHumanoidAvatarBuilder.FindBone(yaya, "RightLeg"),
            MixamoHumanoidAvatarBuilder.FindBone(yaya, "LeftFoot"),
            MixamoHumanoidAvatarBuilder.FindBone(yaya, "RightFoot")
        };

        Vector3 best = headFlat + awayFromKid * comfortApproachDistance;
        float bestScore = float.MaxValue;
        bool foundFree = false;
        float[] radii = { comfortApproachDistance, comfortApproachDistance + 0.08f, comfortApproachDistance + 0.18f };
        foreach (float radius in radii)
        {
            for (int a = -150; a <= 150; a += 15)
            {
                Vector3 candidate = headFlat + Quaternion.AngleAxis(a, Vector3.up) * awayFromKid * radius;
                if (!IsSpotFree(candidate, yaya, kidBones))
                    continue;

                float score = Vector3.Distance(from, candidate) + Mathf.Abs(a) * 0.002f + (radius - comfortApproachDistance) * 2f;
                if (!IsPathFree(from, candidate, yaya))
                    score += 5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    foundFree = true;
                }
            }
            if (foundFree)
                break;
        }

        if (!foundFree)
            Debug.LogWarning("[MomAnimationPlayer] No free spot next to Yaya found; using the default comfort spot.", this);
        return best;
    }

    private bool IsSpotFree(Vector3 spot, Transform yaya, Transform[] kidBones)
    {
        foreach (Transform bone in kidBones)
        {
            if (bone == null)
                continue;
            Vector3 flat = new Vector3(bone.position.x, spot.y, bone.position.z);
            if (Vector3.Distance(flat, spot) < minDistanceFromYaya)
                return false;
        }

        Collider[] hits = Physics.OverlapCapsule(spot + Vector3.up * (0.3f + bodyRadius), spot + Vector3.up * 1.5f, bodyRadius, obstacleLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
        {
            if (c == null || c.transform.IsChildOf(transform) || c.transform.IsChildOf(yaya))
                continue;
            return false;
        }
        return true;
    }

    private bool IsPathFree(Vector3 from, Vector3 to, Transform yaya)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.05f)
            return true;

        RaycastHit[] hits = Physics.CapsuleCastAll(from + Vector3.up * (0.3f + bodyRadius), from + Vector3.up * 1.5f, bodyRadius * 0.9f, delta / distance, distance, obstacleLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(yaya))
                continue;
            return false;
        }
        return true;
    }

    private Transform routeKid;

    // The way from A to B: directly when the straight line is clear, otherwise through one
    // side-step point that keeps her body out of furniture (the bed).
    private System.Collections.Generic.List<Vector3> BuildRoute(Vector3 start, Vector3 goal)
    {
        System.Collections.Generic.List<Vector3> route = new System.Collections.Generic.List<Vector3>();
        if (routeKid == null)
        {
            YayaAnimationPlayer kid = FindObjectOfType<YayaAnimationPlayer>();
            routeKid = kid != null ? kid.transform : transform;
        }

        Vector3 line = goal - start;
        line.y = 0f;
        float length = line.magnitude;
        if (length < 0.3f || IsPathFree(start, goal, routeKid))
        {
            route.Add(goal);
            return route;
        }

        Vector3 dir = line / length;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float[] along = { 0.5f, 0.25f, 0.75f, 0f, 1f };
        float[] aside = { 0.45f, 0.7f, 1.0f, 1.4f };
        bool found = false;
        float bestLength = float.MaxValue;
        Vector3 best = goal;
        foreach (float f in along)
        {
            foreach (float off in aside)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 point = start + dir * (length * f) + side * (off * sign);
                    float total = Vector3.Distance(start, point) + Vector3.Distance(point, goal);
                    if (total >= bestLength)
                        continue;
                    if (!IsStandable(point, routeKid) || !IsPathFree(start, point, routeKid) || !IsPathFree(point, goal, routeKid))
                        continue;
                    bestLength = total;
                    best = point;
                    found = true;
                }
            }
        }

        if (found)
        {
            route.Add(best);
            if (logEvents)
                Debug.Log($"[MomAnimationPlayer] Walking around an obstacle via {best:F2}.", this);
        }
        else if (logEvents)
        {
            Debug.Log("[MomAnimationPlayer] No clear way around was found; walking straight.", this);
        }
        route.Add(goal);
        return route;
    }

    private bool IsStandable(Vector3 spot, Transform kid)
    {
        Collider[] overlaps = Physics.OverlapCapsule(spot + Vector3.up * (0.3f + bodyRadius), spot + Vector3.up * 1.5f, bodyRadius * 0.9f, obstacleLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider c in overlaps)
        {
            if (c == null || c.transform.IsChildOf(transform) || c.transform.IsChildOf(kid))
                continue;
            return false;
        }
        return true;
    }

    // Walk back to where Mom stood before her first WalkTo (e.g. after the listen scene).
    public void WalkBackToStart()
    {
        if (!hasStartHome || !isActiveAndEnabled)
            return;

        comfortActive = false;
        displacedFromHome = false;
        StartWalk(startHomePosition, startHomeForward, false);
    }

    // Blocking walk used by the Part3 listen staging.
    public IEnumerator WalkTo(Vector3 position, Vector3 faceForward)
    {
        comfortActive = false;
        displacedFromHome = false;
        if (walkRoutine != null)
        {
            StopCoroutine(walkRoutine);
            walkRoutine = null;
        }
        yield return WalkRoutine(position, faceForward, true);
    }

    private void StartWalk(Vector3 position, Vector3 faceForward, bool recordStartHome)
    {
        if (!isActiveAndEnabled)
            return;
        if (walkRoutine != null)
            StopCoroutine(walkRoutine);
        walkRoutine = StartCoroutine(WalkRoutine(position, faceForward, recordStartHome));
    }

    private bool IsWalkingNow()
    {
        return isWalking && Time.time - walkHeartbeat < 0.25f;
    }

    private IEnumerator WalkRoutine(Vector3 position, Vector3 faceForward, bool recordStartHome)
    {
        isWalking = true;
        walkHeartbeat = Time.time;
        pendingStates = null;
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (recordStartHome && !hasStartHome)
        {
            hasStartHome = true;
            startHomePosition = transform.position;
            startHomeForward = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
        }

        // The Mixamo walk clip moves the hips forward (not in-place). Pin the hips over the
        // root while walking so the body does not run ahead and snap back.
        if (hipsBone == null)
            hipsBone = MixamoHumanoidAvatarBuilder.FindBone(transform, "Hips");
        if (hipsBone != null)
        {
            pinnedHipsLocal = hipsBone.localPosition;
            pinHipsWhileWalking = true;
        }

        bool rootMotion = animator != null && animator.applyRootMotion;
        if (animator != null)
            animator.applyRootMotion = false;

        Vector3 start = transform.position;
        position.y = start.y;
        if ((position - start).magnitude > 0.05f)
        {
            suppressComfortEnd = true;
            PlayFirstAvailableState(walkStartState, "Female Start Walking ", "Female Start Walking", "Base Layer.Female Start Walking");
            suppressComfortEnd = false;

            // Straight there if nothing is in the way, otherwise around it (e.g. around the bed).
            foreach (Vector3 point in BuildRoute(start, position))
            {
                Vector3 legStart = transform.position;
                Vector3 path = point - legStart;
                float distance = path.magnitude;
                if (distance <= 0.05f)
                    continue;

                Quaternion pathRotation = RootRotationFacing(path);
                Quaternion fromRotation = transform.rotation;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    walkHeartbeat = Time.time;
                    transform.rotation = Quaternion.Slerp(fromRotation, pathRotation, t / 0.3f);
                    KeepWalkClipLooping();
                    yield return null;
                }

                float duration = distance / Mathf.Max(0.1f, walkSpeed);
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    walkHeartbeat = Time.time;
                    transform.position = Vector3.Lerp(legStart, point, Mathf.SmoothStep(0f, 1f, t / duration));
                    transform.rotation = pathRotation;
                    KeepWalkClipLooping();
                    yield return null;
                }
                transform.position = point;
            }
        }

        Quaternion finalRotation = RootRotationFacing(faceForward);
        Quaternion before = transform.rotation;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            walkHeartbeat = Time.time;
            transform.rotation = Quaternion.Slerp(before, finalRotation, t / 0.35f);
            yield return null;
        }
        transform.rotation = finalRotation;

        // Arrived: play whatever the story asked for meanwhile (e.g. Clapping), else idle.
        isWalking = false;
        string[] pending = pendingStates;
        pendingStates = null;
        suppressComfortEnd = true;
        if (pending == null || !PlayFirstAvailableState(pending))
            PlayState(standingIdleState);
        suppressComfortEnd = false;

        // Keep the hips pinned through the cross-fade, then release smoothly.
        yield return new WaitForSeconds(fadeDuration + 0.05f);
        pinHipsWhileWalking = false;
        if (animator != null)
            animator.applyRootMotion = rootMotion;
        walkRoutine = null;
    }

    private void KeepWalkClipLooping()
    {
        if (animator == null || animator.runtimeAnimatorController == null || animator.IsInTransition(layerIndex))
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (!info.loop && info.normalizedTime >= 0.92f)
            animator.Play(info.fullPathHash, layerIndex, 0.3f);
    }

    private void UpdateClapAssist()
    {
        bool clappingNow = IsStateActive(clappingState) || IsStateActive("Clapping");
        if (clappingNow && !clapAssistActive)
            clapMinGap = float.MaxValue;
        clapAssistActive = clappingNow;
        clapAssistWeight = Mathf.MoveTowards(clapAssistWeight, clapAssist && clappingNow && !IsWalkingNow() ? 1f : 0f, Time.deltaTime / 0.25f);
        if (clapAssistWeight <= 0f)
            return;

        if (!clapLeftArm.IsValid || !clapRightArm.IsValid)
        {
            clapLeftArm = ArmReachIK.FindArm(transform, false);
            clapRightArm = ArmReachIK.FindArm(transform, true);
            clapLeftPalm = MixamoHumanoidAvatarBuilder.FindBone(transform, "LeftHandMiddle1") ?? clapLeftArm.hand;
            clapRightPalm = MixamoHumanoidAvatarBuilder.FindBone(transform, "RightHandMiddle1") ?? clapRightArm.hand;
            if (!clapLeftArm.IsValid || !clapRightArm.IsValid || clapLeftPalm == null || clapRightPalm == null)
                return;
        }

        Vector3 left = clapLeftPalm.position;
        Vector3 right = clapRightPalm.position;
        float gap = Vector3.Distance(left, right);
        if (gap < 1e-4f)
            return;

        // The clip's hands never quite meet; shift both palms inward by the amount they miss.
        clapMinGap = Mathf.Min(clapMinGap, gap);
        float closeBy = Mathf.Max(0f, clapMinGap - clapContactGap);
        float newGap = Mathf.Max(clapContactGap, gap - closeBy);
        Vector3 mid = (left + right) * 0.5f;
        Vector3 dir = (right - left) / gap;
        Vector3 leftDelta = (mid - dir * newGap * 0.5f) - left;
        Vector3 rightDelta = (mid + dir * newGap * 0.5f) - right;

        ArmReachIK.Solve(clapLeftArm, clapLeftArm.hand.position + leftDelta, clapAssistWeight);
        ArmReachIK.Solve(clapRightArm, clapRightArm.hand.position + rightDelta, clapAssistWeight);
    }

    private bool IsStateActive(string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
            return false;
        int hash = Animator.StringToHash(stateName);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (current.shortNameHash == hash)
            return true;
        if (animator.IsInTransition(layerIndex))
            return animator.GetNextAnimatorStateInfo(layerIndex).shortNameHash == hash;
        return false;
    }

    [Header("Loop one-shot clips until the next action")]
    [SerializeField] private bool loopFinishedStates = true;
    [SerializeField] private string doNotLoopStates = "Female Start Walking|Female Start Walking |Quick Informal Bow";

    // ---------------------------------------------------------------------------------
    // Talking: whenever Mom has the dialogue line and no special action is playing (she is
    // just standing, or her clapping has finished), she switches to the Talking animation,
    // turned toward whoever she is speaking to (Yaya or the nurse).
    // ---------------------------------------------------------------------------------
    [Header("Talking while Mom has the dialogue line")]
    [SerializeField] private bool autoTalkWhenSpeaking = true;
    [Tooltip("If Mom's line contains one of these (separated by |) she is talking to the nurse; otherwise to Yaya.")]
    [SerializeField] private string nurseLineKeywords = "麻煩|不好意思|請問|她|你們|之前";
    [Tooltip("How quickly she turns toward the listener.")]
    [SerializeField] private float talkTurnSpeed = 4f;
    [Tooltip("Keep her on the spot while talking (the Talking clip drifts sideways).")]
    [SerializeField] private bool keepPlaceWhileTalking = true;
    private NewDialogueManager[] dialogueManagers;
    private float nextManagerScan;
    private bool autoTalking;
    private bool speechInternal;
    private bool momSpeaking;
    private string momLine;
    private float talkFaceWeight;
    private float talkYaw;
    private bool talkPoseValid;
    private Vector3 talkHipsStart;
    private Vector3 talkHipsAverage;
    private Transform talkHips;
    private Transform talkYaya;

    private bool TryGetMomLine(out string content)
    {
        content = null;
        if (dialogueManagers == null || Time.unscaledTime >= nextManagerScan)
        {
            dialogueManagers = FindObjectsOfType<NewDialogueManager>(true);
            nextManagerScan = Time.unscaledTime + 2f;
        }

        foreach (NewDialogueManager manager in dialogueManagers)
        {
            if (manager == null || manager.speakerText == null || !manager.speakerText.gameObject.activeInHierarchy)
                continue;

            string speaker = manager.speakerText.text;
            if (string.IsNullOrEmpty(speaker) || (speaker.IndexOf('媽') < 0 && speaker.IndexOf('嬤') < 0))
                continue;

            content = manager.bodyText != null ? manager.bodyText.text : string.Empty;
            return true;
        }
        return false;
    }

    private void UpdateSpeechPose()
    {
        if (!autoTalkWhenSpeaking || animator == null || animator.runtimeAnimatorController == null)
            return;

        momSpeaking = TryGetMomLine(out momLine);
        if (IsWalkingNow() || comfortActive || animator.IsInTransition(layerIndex))
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (momSpeaking)
        {
            bool idle = info.shortNameHash == Animator.StringToHash(standingIdleState);
            // One full clap (at least 1.5 s), then she talks instead of clapping on and on.
            bool clapDone = info.shortNameHash == Animator.StringToHash(clappingState)
                && info.normalizedTime * info.length >= Mathf.Max(info.length - 0.05f, 1.5f);
            if (idle || clapDone)
            {
                speechInternal = true;
                PlayState(talkingState);
                speechInternal = false;
                autoTalking = true;
            }
        }
        else if (autoTalking)
        {
            autoTalking = false;
            if (info.shortNameHash == Animator.StringToHash(talkingState))
            {
                speechInternal = true;
                PlayState(standingIdleState);
                speechInternal = false;
            }
        }
    }

    private Transform lastTalkTarget;

    // Who Mom is talking to: the nurse (camera) or Yaya. It is decided from her own line; when
    // no line of hers is on screen she keeps facing whoever she was talking to (no turning away
    // just because a prompt appeared).
    private Transform GetTalkTarget()
    {
        if ((!momSpeaking || string.IsNullOrEmpty(momLine)) && lastTalkTarget != null)
            return lastTalkTarget;

        lastTalkTarget = ResolveTalkTarget();
        return lastTalkTarget;
    }

    private Transform ResolveTalkTarget()
    {
        bool toNurse = !momSpeaking || string.IsNullOrEmpty(momLine);
        if (!toNurse && !string.IsNullOrEmpty(nurseLineKeywords))
        {
            foreach (string raw in nurseLineKeywords.Split('|'))
            {
                string key = raw.Trim();
                if (key.Length > 0 && momLine.Contains(key))
                {
                    toNurse = true;
                    break;
                }
            }
        }

        if (!toNurse)
        {
            if (talkYaya == null)
            {
                YayaAnimationPlayer yaya = FindObjectOfType<YayaAnimationPlayer>();
                if (yaya != null)
                {
                    talkYaya = MixamoHumanoidAvatarBuilder.FindBone(yaya.transform, "Hips");
                    if (talkYaya == null)
                        talkYaya = yaya.transform;
                }
            }
            if (talkYaya != null)
                return talkYaya;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    // Runs after the Animator: turns the whole body (from the hips) toward the listener and
    // cancels the sideways drift of the Talking clip, keeping its small natural movements.
    private void UpdateTalkFacing()
    {
        bool talking = false;
        if (animator != null && animator.runtimeAnimatorController != null && !IsWalkingNow())
        {
            AnimatorStateInfo state = animator.IsInTransition(layerIndex)
                ? animator.GetNextAnimatorStateInfo(layerIndex)
                : animator.GetCurrentAnimatorStateInfo(layerIndex);
            talking = state.shortNameHash == Animator.StringToHash(talkingState);
        }

        talkFaceWeight = Mathf.MoveTowards(talkFaceWeight, talking ? 1f : 0f, Time.deltaTime / 0.4f);
        if (talkFaceWeight <= 0f)
        {
            talkPoseValid = false;
            return;
        }

        if (talkHips == null)
            talkHips = MixamoHumanoidAvatarBuilder.FindBone(transform, "Hips");
        if (talkHips == null)
            return;

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.5f, talkTurnSpeed) * Time.deltaTime);
        Vector3 animatedHips = talkHips.position;
        float yaw = talkYaw;
        Transform target = GetTalkTarget();
        if (target != null)
        {
            Vector3 want = Vector3.ProjectOnPlane(target.position - animatedHips, Vector3.up);
            Vector3 have = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
            if (want.sqrMagnitude > 1e-4f && have.sqrMagnitude > 1e-6f)
                yaw = Vector3.SignedAngle(have, want, Vector3.up);
        }

        if (!talkPoseValid)
        {
            talkPoseValid = true;
            talkHipsStart = animatedHips;
            talkHipsAverage = animatedHips;
            talkYaw = yaw;
        }
        else
        {
            talkHipsAverage = Vector3.Lerp(talkHipsAverage, animatedHips, blend);
            talkYaw = Mathf.LerpAngle(talkYaw, yaw, blend);
        }

        if (keepPlaceWhileTalking && talking)
        {
            Vector3 drift = talkHipsStart - talkHipsAverage;
            drift.y = 0f;
            talkHips.position = animatedHips + drift * talkFaceWeight;
        }
        talkHips.rotation = Quaternion.AngleAxis(talkYaw * talkFaceWeight, Vector3.up) * talkHips.rotation;
    }

    private void Update()
    {
        UpdateSpeechPose();

        if (!loopFinishedStates || IsWalkingNow() || animator == null || animator.runtimeAnimatorController == null || animator.IsInTransition(layerIndex))
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (info.loop || info.normalizedTime < 1f)
            return;

        foreach (string raw in doNotLoopStates.Split('|'))
        {
            if (raw.Length > 0 && Animator.StringToHash(raw) == info.shortNameHash)
                return;
        }

        animator.CrossFadeInFixedTime(info.fullPathHash, 0.3f, layerIndex, 0f);
    }

    private void EndComfort()
    {
        if (suppressComfortEnd)
            return;

        comfortActive = false;
        if (returnAfterComfort && displacedFromHome)
        {
            displacedFromHome = false;
            StartWalk(homePosition, homeForward, false);
        }
    }

    // Root rotation that makes the pelvis (not necessarily the root's +Z) face `worldForward`.
    private Quaternion RootRotationFacing(Vector3 worldForward)
    {
        worldForward = Vector3.ProjectOnPlane(worldForward, Vector3.up);
        if (worldForward.sqrMagnitude < 1e-8f)
            return transform.rotation;
        intendedForward = worldForward.normalized;

        Vector3 rootForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        Vector3 pelvisForward = PrefabPerformanceRuntime.GetActorPelvisForward(transform);
        float offset = rootForward.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(rootForward, pelvisForward, Vector3.up) : 0f;
        return Quaternion.LookRotation(worldForward.normalized, Vector3.up) * Quaternion.Euler(0f, -offset, 0f);
    }

    private void StartMove(Vector3 position, Quaternion rotation, float duration)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRoutine(position, rotation, duration));
    }

    private IEnumerator MoveRoutine(Vector3 position, Quaternion rotation, float duration)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        duration = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            transform.SetPositionAndRotation(Vector3.Lerp(startPosition, position, k), Quaternion.Slerp(startRotation, rotation, k));
            yield return null;
        }
        transform.SetPositionAndRotation(position, rotation);
        moveRoutine = null;
    }

    private void LateUpdate()
    {
        hipsPinWeight = pinHipsWhileWalking ? 1f : Mathf.MoveTowards(hipsPinWeight, 0f, Time.deltaTime / 0.3f);
        if (hipsPinWeight > 0f && hipsBone != null)
        {
            Vector3 local = hipsBone.localPosition;
            Vector3 pinned = new Vector3(pinnedHipsLocal.x, local.y, pinnedHipsLocal.z);
            hipsBone.localPosition = Vector3.Lerp(local, pinned, hipsPinWeight);
        }

        UpdateClapAssist();
        UpdateTalkFacing();

        comfortWeight = Mathf.MoveTowards(comfortWeight, comfortActive && !IsWalkingNow() ? 1f : 0f, Time.deltaTime / 0.35f);
        if (comfortWeight <= 0f || comfortHead == null)
            return;

        Vector3 towardMom = Vector3.ProjectOnPlane(transform.position - comfortHead.position, Vector3.up).normalized;
        float pat = Mathf.Abs(Mathf.Sin((Time.time - comfortStartTime) * Mathf.PI * comfortPatSpeed)) * comfortPatAmplitude;
        Vector3 target = comfortHead.position + Vector3.up * (comfortHeadSize + pat) + towardMom * 0.03f;

        if (!comfortArm.IsValid)
            comfortArm = ArmReachIK.FindClosestArm(transform, target);
        ArmReachIK.Solve(comfortArm, target, comfortWeight);
    }

    public void PlayTalking()
    {
        PlayState(talkingState);
    }

    public void PlayBend()
    {
        PlayClipFallback(ref momBendClip, true, "Listen_Mom", "MomBend", "Armature|MomBend", "Mom Bend", "Mom_Bend");
    }

    public void PlayCustom(string stateName)
    {
        PlayState(stateName);
    }

    private void PlayState(string stateName)
    {
        if (!speechInternal)
            autoTalking = false;
        EndComfort();
        if (!suppressComfortEnd && IsWalkingNow())
        {
            pendingStates = new[] { stateName };
            return;
        }
        if (animator == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] Animator is missing.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning("[MomAnimationPlayer] State name is empty.", this);
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(layerIndex, stateHash))
        {
            Debug.LogWarning($"[MomAnimationPlayer] Animator state not found: {stateName}", this);
            return;
        }

        if (IsAlreadyPlaying(stateHash))
            return;

        StopClipFallback();
        if (logEvents)
            Debug.Log($"[MomAnimationPlayer] Play animation: {stateName}", this);

        animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex);
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

    private bool PlayFirstAvailableState(params string[] stateNames)
    {
        autoTalking = false;
        EndComfort();
        if (!suppressComfortEnd && IsWalkingNow())
        {
            pendingStates = stateNames;
            return true;
        }
        if (animator == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] Animator is missing.", this);
            return false;
        }

        foreach (string stateName in stateNames)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(layerIndex, stateHash))
                continue;

            if (IsAlreadyPlaying(stateHash))
                return true;

            StopClipFallback();
            if (logEvents)
                Debug.Log($"[MomAnimationPlayer] Play animation: {stateName}", this);

            animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex);
            return true;
        }

        Debug.LogWarning("[MomAnimationPlayer] None of the requested animator states were found.", this);
        return false;
    }

    private bool PlayFirstAvailableStateDirect(params string[] stateNames)
    {
        if (animator == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] Animator is missing.", this);
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
                Debug.Log($"[MomAnimationPlayer] Play animation: {stateName}", this);

            animator.Play(stateHash, layerIndex, 0f);
            animator.Update(0f);
            return true;
        }

        Debug.LogWarning("[MomAnimationPlayer] None of the requested animator states were found. Trying clip fallback.", this);
        return false;
    }

    private void PlayClipFallback(ref AnimationClip assignedClip, bool loop, params string[] clipNames)
    {
        EndComfort();
        if (animator == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] Animator is missing.", this);
            return;
        }

        if (assignedClip == null)
            assignedClip = FindAnimationClipByName(clipNames);

        if (assignedClip == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] AnimationClip fallback not found. Drag MomBend clip into Mom Bend Clip.", this);
            return;
        }

        // Already looping this clip: keep playing instead of snapping back to frame 0.
        if (loop && currentFallbackClip == assignedClip && clipGraph.IsValid())
            return;

        StopClipFallback();
        if (!EnsureAvatarForClip(assignedClip))
            return;
        if (logEvents)
            Debug.Log($"[MomAnimationPlayer] Play clip fallback: {assignedClip.name}", this);

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
            Debug.LogWarning($"[MomAnimationPlayer] {clip.name} is a Humanoid clip but no Humanoid avatar is available (runtime build failed).", this);
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
            Debug.Log($"[MomAnimationPlayer] Using humanoid avatar {avatarToUse.name} for {clip.name}", this);
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
}