using System.Collections.Generic;
using UnityEngine;

// Mom's new look.
//
// The new character is a completely separate model with its OWN skeleton, its OWN Animator
// and its OWN (humanoid) copies of Mom's animations. Nothing is copied bone-by-bone from the
// old Mom. The old Mom stays in the scene, invisible, only as the "director": the story scripts
// keep telling her what to do, and this script makes the new Mom do the same thing:
//   - same place and same facing,
//   - same animation (idle, talking, clapping, walking, angry, pointing, bow, listening...),
//   - patting Yaya's head when the old Mom would.
//
// Created automatically. To go back to the old Mom: add this component to an object in the
// scene and untick "Swap Enabled".
[DefaultExecutionOrder(20000)]
public class MomSkinSwap : MonoBehaviour
{
    [SerializeField] private bool swapEnabled = true;
    [Tooltip("Character prefab in a Resources folder (Humanoid).")]
    [SerializeField] private string skinResourceName = "MomSkin_CivilianGirl";
    [Tooltip("Animator Controller in a Resources folder with the humanoid copies of Mom's animations.")]
    [SerializeField] private string controllerResourceName = "NewMom_AnimatorController";
    [Tooltip("1 = same height as the old Mom. Smaller = a smaller Mom.")]
    [SerializeField] private float heightMultiplier = 0.95f;
    [Tooltip("Hide other copies of the new character that were placed in the scene by hand (for example the one at the door).")]
    [SerializeField] private bool hideExtraCopiesInScene = true;
    [Tooltip("Turn the new Mom by this many degrees if she faces the wrong way.")]
    [SerializeField] private float facingOffsetDegrees = 0f;
    [Tooltip("Extra up/down shift (m) if her feet float or sink.")]
    [SerializeField] private float footOffset = 0f;
    [SerializeField] private float blendSeconds = 0.25f;
    [Tooltip("States that should play once and then hold instead of looping (separated by |).")]
    [SerializeField] private string playOnceStates = "Quick Informal Bow";
    [SerializeField] private string idleState = "MomStanding Idle";
    [SerializeField] private string listenState = "Listen_Mom";
    [Tooltip("Idle only: degrees the arms are opened outward so the hands do not sink into the thighs.")]
    [SerializeField] private float idleArmSpreadDegrees = 7f;
    [Tooltip("Keep her upper body upright: Mom's animations were made for a model in high heels and lean back a lot (the talking one most of all).")]
    [SerializeField] private bool straightenPosture = true;
    [Tooltip("Lean she ends up with, in degrees (positive = slightly forward).")]
    [SerializeField] private float targetForwardLean = 2f;
    [Tooltip("Turn her so that her body (not just her feet) really faces where she should look.")]
    [SerializeField] private bool correctBodyTwist = true;
    [Tooltip("Listening scene: keep Mom's chest where the stethoscope is held.")]
    [SerializeField] private bool alignChestWhileListening = true;

    [Header("Talking / clapping (her own gestures instead of the old clips)")]
    [Tooltip("Talking: stand naturally and gesture with the hands, instead of the old clip (hand on hip, leaning back, looking away).")]
    [SerializeField] private bool gentleTalk = true;
    [SerializeField] private string talkState = "Talking";
    [Tooltip("0 = hands stay down while talking, 1 = normal gestures.")]
    [Range(0f, 1.5f)] [SerializeField] private float talkGestureAmount = 1f;
    [Tooltip("How much she turns her head to look at whoever she talks to (0-1).")]
    [Range(0f, 1f)] [SerializeField] private float lookAtListener = 0.7f;
    [Tooltip("Clapping: bring the palms together and apart in front of the chest.")]
    [SerializeField] private bool properClap = true;
    [SerializeField] private string clapState = "Clapping";
    [SerializeField] private float clapsPerSecond = 2.4f;
    [Tooltip("How far in front of her shoulders the hands meet (in arm lengths; bigger = further from her body).")]
    [SerializeField] private float clapForward = 0.55f;
    [Tooltip("How far below her shoulders the hands meet (in arm lengths).")]
    [SerializeField] private float clapDrop = 0.30f;
    [Header("Listening scene (stethoscope)")]
    [Tooltip("Distance (m) from her chest bone to the front of her chest, where the stethoscope rests.")]
    [SerializeField] private float stethoscopeChestDepth = 0.11f;
    [Tooltip("Fine tuning (m): x = to her right, y = up, z = toward the stethoscope.")]
    [SerializeField] private Vector3 listenFineOffset = Vector3.zero;
    [SerializeField] private string stethoscopeHeadName = "Diaphragm";

    [Header("Walking")]
    [Tooltip("Size of her steps: 1 = as in the walking animation, smaller = smaller steps.")]
    [Range(0.2f, 1f)] [SerializeField] private float walkStride = 0.55f;
    private Transform[] strideBones;
    private Quaternion[] strideRest;
    private bool strideRestKnown;
    private float walkDamp;

    private bool talking;
    private bool clapping;
    private float talkWeight;
    private float clapWeight;
    private Transform newHead;
    private Transform stethoscopeHead;
    private float nextStethoscopeScan;

    // --- Reaching for / holding something (used by the story, e.g. the ear thermometer) ---
    public static MomSkinSwap Active { get; private set; }
    public bool IsShown => ready && newVisible && newRoot != null;
    public Vector3 ChestPosition => newChest != null ? newChest.position : (newRoot != null ? newRoot.position : transform.position);
    [Tooltip("How much her fingers close around what she holds (0 = open hand, 1 = fist).")]
    [Range(0f, 1f)] [SerializeField] private float holdFingerCurl = 0.5f;
    private int reachFrame = -100;
    private Vector3 reachHandTarget;
    private Transform reachProp;
    private Vector3 reachAim;
    private float reachWeight;
    private bool reachUsesRight;

    // Call every frame while Mom should hold her hand at `handTarget`. If `prop` is given it is
    // kept in that hand, pointing at `aimAt`.
    public void ReachTo(Vector3 handTarget, Transform prop, Vector3 aimAt)
    {
        reachFrame = Time.frameCount;
        reachHandTarget = handTarget;
        reachProp = prop;
        reachAim = aimAt;
    }

    // Call every frame (together with ReachTo) to rest her OTHER hand gently at `target`,
    // e.g. on top of Yaya's head to keep it still while measuring her ear.
    public void SupportWith(Vector3 target)
    {
        supportFrame = Time.frameCount;
        supportTarget = target;
    }
    private int supportFrame = -100;
    private Vector3 supportTarget;
    private float supportWeight;
    [Tooltip("How far her wrist may bend to point what she holds at its target (degrees).")]
    [SerializeField] private float holdMaxWristBend = 70f;

    private MomAnimationPlayer oldMom;
    private Animator oldAnimator;
    private Transform oldRoot;
    private Transform oldHips;
    private readonly HashSet<string> oldMeshNames = new HashSet<string>();
    private readonly List<SkinnedMeshRenderer> sceneMomMeshes = new List<SkinnedMeshRenderer>();
    private readonly List<SkinnedMeshRenderer> performanceMeshes = new List<SkinnedMeshRenderer>();
    private readonly HashSet<int> knownMeshes = new HashSet<int>();
    private float nextScan;

    private Transform newRoot;
    private Animator newAnimator;
    private Renderer[] newRenderers;
    private bool newVisible = true;
    private int playingState;
    private bool placed;
    private float facingYaw;
    private readonly List<KeyValuePair<Transform, Quaternion>> faceRest = new List<KeyValuePair<Transform, Quaternion>>();
    private ArmReachIK.Arm leftArm, rightArm;
    private float patWeight;
    private float watchWeight;
    private float patStartTime;
    private bool patUsesRight;
    private bool ready;
    private float idleSpreadWeight;
    private Transform newChest;
    private Transform newLowSpine;
    private Transform newNeck;
    private Transform newPelvis, thighL, thighR;
    private float leanBack;        // measured, smoothed (degrees, + = leaning back)
    private float twist;           // measured, smoothed (degrees the body is turned away from her root)
    private Vector3 listenShift;
    private Vector3 basePosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<MomSkinSwap>(true) != null)
            return;
        new GameObject("MomSkinSwap (auto)").AddComponent<MomSkinSwap>();
    }

    private void Awake()
    {
        if (swapEnabled)
            ready = Setup();
        if (ready)
            Active = this;
    }

    private bool Setup()
    {
        oldMom = FindObjectOfType<MomAnimationPlayer>(true);
        if (oldMom == null)
            return false;

        oldRoot = oldMom.transform;
        oldAnimator = oldMom.BodyAnimator != null ? oldMom.BodyAnimator : oldMom.GetComponentInChildren<Animator>(true);
        oldHips = MixamoHumanoidAvatarBuilder.FindBone(oldRoot, "Hips");

        GameObject prefab = Resources.Load<GameObject>(skinResourceName);
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(controllerResourceName);
        if (prefab == null || controller == null)
        {
            Debug.LogWarning($"[MomSkinSwap] Resources/{skinResourceName} or Resources/{controllerResourceName} not found: keeping the old Mom.", this);
            return false;
        }

        // Height of the old Mom as she stands in the scene (from her meshes, not her bones).
        Bounds oldBounds = new Bounds();
        bool hasOld = false;
        foreach (SkinnedMeshRenderer smr in oldRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            oldMeshNames.Add(smr.name);
            if (!hasOld) { oldBounds = smr.bounds; hasOld = true; }
            else oldBounds.Encapsulate(smr.bounds);
        }

        GameObject skin = Instantiate(prefab);
        skin.name = "Mom_NewLook";
        newAnimator = skin.GetComponentInChildren<Animator>(true);
        if (newAnimator == null || newAnimator.avatar == null || !newAnimator.avatar.isHuman)
        {
            Debug.LogWarning("[MomSkinSwap] The new character has no Humanoid avatar: keeping the old Mom.", this);
            Destroy(skin);
            return false;
        }

        newRoot = newAnimator.transform;
        newRenderers = skin.GetComponentsInChildren<Renderer>(true);
        Bounds newBounds = new Bounds();
        bool hasNew = false;
        foreach (Renderer r in newRenderers)
        {
            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null)
                smr.updateWhenOffscreen = true;
            if (!hasNew) { newBounds = r.bounds; hasNew = true; }
            else newBounds.Encapsulate(r.bounds);
        }

        float scale = 1f;
        if (hasOld && hasNew && newBounds.size.y > 0.01f && oldBounds.size.y > 0.01f)
        {
            scale = oldBounds.size.y / newBounds.size.y;
            if (scale < 0.5f || scale > 2f)
                scale = 1f;   // something is off with the measurement: do not distort her
        }
        newRoot.localScale *= scale * Mathf.Max(0.1f, heightMultiplier);
        Debug.Log($"[MomSkinSwap] Old Mom height {oldBounds.size.y:0.00} m, new character {newBounds.size.y:0.00} m -> scale x{scale:0.00}", this);

        if (hideExtraCopiesInScene)
        {
            HashSet<string> skinMeshes = new HashSet<string>();
            foreach (SkinnedMeshRenderer smr in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh != null)
                    skinMeshes.Add(smr.sharedMesh.name);
            }
            foreach (SkinnedMeshRenderer smr in FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh == null || !skinMeshes.Contains(smr.sharedMesh.name) || smr.transform.IsChildOf(skin.transform))
                    continue;
                Animator owner = smr.GetComponentInParent<Animator>();
                GameObject copy = owner != null ? owner.gameObject : smr.gameObject;
                if (copy.activeSelf)
                {
                    copy.SetActive(false);
                    Debug.Log($"[MomSkinSwap] Hid an extra copy of the new character placed in the scene: '{copy.name}'.", copy);
                }
            }
        }

        newAnimator.runtimeAnimatorController = controller;
        newAnimator.applyRootMotion = false;                       // she is moved by this script
        newAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        newAnimator.enabled = true;

        Transform head = FindByName(newRoot, "head");
        newHead = head;
        if (head != null)
        {
            // Mom's animations have no face movement: keep the face in its neutral pose.
            foreach (Transform t in head.GetComponentsInChildren<Transform>(true))
            {
                if (t != head)
                    faceRest.Add(new KeyValuePair<Transform, Quaternion>(t, t.localRotation));
            }
        }

        newChest = FindByName(newRoot, "spine_03") ?? FindByName(newRoot, "spine_02");
        newLowSpine = FindByName(newRoot, "spine_01");
        newNeck = FindByName(newRoot, "neck_01");
        newPelvis = FindByName(newRoot, "pelvis");
        thighL = FindByName(newRoot, "thigh_l");
        thighR = FindByName(newRoot, "thigh_r");
        leftArm = new ArmReachIK.Arm { upper = FindByName(newRoot, "upperarm_l"), lower = FindByName(newRoot, "lowerarm_l"), hand = FindByName(newRoot, "hand_l") };
        rightArm = new ArmReachIK.Arm { upper = FindByName(newRoot, "upperarm_r"), lower = FindByName(newRoot, "lowerarm_r"), hand = FindByName(newRoot, "hand_r") };

        // The old Mom keeps "acting" although nobody sees her.
        if (oldAnimator != null)
            oldAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Debug.Log("[MomSkinSwap] Mom now uses the new character with her own animations.", this);
        return true;
    }

    private static Transform FindByName(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }

    // Stops drawing every old-Mom mesh: the scene Mom and the copy used by the listening performance.
    private void HideOldMom()
    {
        sceneMomMeshes.RemoveAll(m => m == null);
        performanceMeshes.RemoveAll(m => m == null);

        foreach (SkinnedMeshRenderer smr in FindObjectsOfType<SkinnedMeshRenderer>())
        {
            if (smr == null || !knownMeshes.Add(smr.GetInstanceID()))
                continue;
            if (smr.transform.IsChildOf(newRoot))
                continue;

            bool onSceneMom = smr.transform.IsChildOf(oldRoot);
            if (!onSceneMom && !oldMeshNames.Contains(smr.name))
                continue;   // not Mom (Yaya's meshes have other names)

            smr.forceRenderingOff = true;   // stays "enabled" for the other scripts, just not drawn
            if (onSceneMom)
            {
                sceneMomMeshes.Add(smr);
            }
            else
            {
                performanceMeshes.Add(smr);
                // Everything else that belongs to that copy of Mom too (shoe straps, small
                // parts with other names), so no bits of the old Mom are left near her feet.
                Transform actor = smr.transform.parent;
                while (actor != null && actor.name != "Mom")
                    actor = actor.parent;
                if (actor != null)
                {
                    foreach (Renderer other in actor.GetComponentsInChildren<Renderer>(true))
                        other.forceRenderingOff = true;
                }
                foreach (Animator a in smr.GetComponentsInParent<Animator>(true))
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }
    }

    private static bool AnyShown(List<SkinnedMeshRenderer> meshes)
    {
        foreach (SkinnedMeshRenderer smr in meshes)
        {
            if (smr != null && smr.enabled && smr.gameObject.activeInHierarchy)
                return true;
        }
        return false;
    }

    private void SetNewVisible(bool visible)
    {
        if (newVisible == visible)
            return;
        newVisible = visible;
        foreach (Renderer r in newRenderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }

    private void Update()
    {
        if (!ready || oldRoot == null || newRoot == null)
            return;

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            HideOldMom();
        }

        bool performance = AnyShown(performanceMeshes);
        SetNewVisible(performance || AnyShown(sceneMomMeshes));

        FollowPlace();
        FollowAnimation(performance);
    }

    // Stand where the old Mom stands, facing the way she faces.
    private void FollowPlace()
    {
        Vector3 spot = oldHips != null ? oldHips.position : oldRoot.position;
        spot.y = oldRoot.position.y + footOffset;

        // Facing: toward whoever she is talking to while one of her lines is on screen,
        // otherwise the direction the story put her in. (Not the old model's body twist.)
        Vector3 forward = oldMom.IntendedForward;
        if (oldMom.TryGetTalkFocus(out Vector3 focus))
        {
            Vector3 toFocus = Vector3.ProjectOnPlane(focus - spot, Vector3.up);
            if (toFocus.sqrMagnitude > 0.04f)
                forward = toFocus;
        }
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg + facingOffsetDegrees;

        if (!placed)
        {
            placed = true;
            facingYaw = yaw;
            basePosition = spot;
        }
        else
        {
            // Smoothed, so the small sway of the old animation does not shake her.
            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            basePosition = Vector3.Lerp(basePosition, spot, k);
            facingYaw = Mathf.LerpAngle(facingYaw, yaw, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }
        newRoot.position = basePosition + listenShift;
        // Some clips stand her at an angle to her own root; `twist` cancels that.
        newRoot.rotation = Quaternion.Euler(0f, facingYaw - (correctBodyTwist ? twist : 0f), 0f);
    }

    // Play the same animation as the old Mom, on the new Mom's own Animator.
    private void FollowAnimation(bool performance)
    {
        int wanted = 0;
        if (performance)
        {
            wanted = Animator.StringToHash(listenState);
            // Same moment of the bend as the hidden performance copy, so Yaya's stethoscope
            // arrives when Mom's chest is there.
            Animator performer = FindPerformanceAnimator();
            if (performer != null && newAnimator.HasState(0, wanted))
            {
                AnimatorStateInfo source = performer.GetCurrentAnimatorStateInfo(0);
                playingState = wanted;
                talking = false;
                clapping = false;
                newAnimator.Play(wanted, 0, Mathf.Clamp(source.normalizedTime, 0f, 0.999f));
                return;
            }
        }
        else if (oldMom.ActiveFallbackClip != null)
        {
            wanted = Animator.StringToHash(oldMom.ActiveFallbackClip.name);
        }
        else if (oldAnimator != null && oldAnimator.runtimeAnimatorController != null)
        {
            AnimatorStateInfo info = oldAnimator.IsInTransition(0)
                ? oldAnimator.GetNextAnimatorStateInfo(0)
                : oldAnimator.GetCurrentAnimatorStateInfo(0);
            wanted = info.shortNameHash;
        }

        talking = false;
        if (gentleTalk && wanted == Animator.StringToHash(talkState))
        {
            // Her own way of talking: relaxed standing pose + hand gestures (added in LateUpdate).
            talking = true;
            wanted = Animator.StringToHash(idleState);
        }

        // Holding something out (thermometer): relaxed standing pose, the arm is placed in LateUpdate
        // (not the "petting" clip, whose bent pose fights with it).
        if (Time.frameCount - reachFrame <= 2)
            wanted = Animator.StringToHash(idleState);

        clapping = false;
        if (properClap && wanted == Animator.StringToHash(clapState))
        {
            // Her own clap: relaxed standing pose + the arms placed in LateUpdate.
            clapping = true;
            wanted = Animator.StringToHash(idleState);
        }

        if (wanted == 0 || !newAnimator.HasState(0, wanted))
            wanted = Animator.StringToHash(idleState);
        if (!newAnimator.HasState(0, wanted))
            return;

        if (wanted != playingState)
        {
            playingState = wanted;
            newAnimator.CrossFadeInFixedTime(wanted, blendSeconds, 0, 0f);
            return;
        }

        // Loop: when a clip reaches its end, start it again (except the play-once ones).
        if (newAnimator.IsInTransition(0))
            return;
        AnimatorStateInfo now = newAnimator.GetCurrentAnimatorStateInfo(0);
        if (now.shortNameHash != wanted || now.loop || now.normalizedTime < 1f)
            return;
        foreach (string raw in playOnceStates.Split('|'))
        {
            if (raw.Trim().Length > 0 && Animator.StringToHash(raw.Trim()) == wanted)
                return;
        }
        newAnimator.Play(wanted, 0, 0f);
    }

    private Animator FindPerformanceAnimator()
    {
        foreach (SkinnedMeshRenderer smr in performanceMeshes)
        {
            if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy)
                continue;
            Transform bone = smr.rootBone != null ? smr.rootBone : smr.transform;
            foreach (Animator a in bone.GetComponentsInParent<Animator>())
            {
                if (a.isActiveAndEnabled && a.runtimeAnimatorController != null)
                    return a;
            }
        }
        return null;
    }

    // Where the hidden performance copy has its chest (that is where Yaya holds the stethoscope).
    private bool TryGetPerformanceChest(out Vector3 chest)
    {
        chest = Vector3.zero;
        foreach (SkinnedMeshRenderer smr in performanceMeshes)
        {
            if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy)
                continue;
            foreach (Transform bone in smr.bones)
            {
                if (bone == null)
                    continue;
                string n = bone.name;
                if (n.EndsWith("Spine2") || n.EndsWith("Spine1"))
                {
                    chest = bone.position;
                    return true;
                }
            }
        }
        return false;
    }

    private void LateUpdate()
    {
        if (!ready || newRoot == null)
            return;

        DampWalk();

        // Listening scene: slide Mom so her chest is over the spot the stethoscope reaches.
        Vector3 wantedShift = Vector3.zero;
        if (alignChestWhileListening && newChest != null && newVisible && TryGetPerformanceChest(out Vector3 chestTarget))
        {
            Vector3 chestNow = newChest.position - listenShift;
            wantedShift = chestTarget - chestNow;
            wantedShift.y = 0f;

            // Once Yaya's stethoscope is close, put the FRONT of Mom's chest exactly on it
            // (also in height, a few centimetres at most).
            Transform scope = FindStethoscopeHead();
            if (scope != null && leftArm.upper != null && rightArm.upper != null && newNeck != null
                && Vector3.Distance(scope.position, chestTarget) < 0.35f)
            {
                Vector3 across = (rightArm.upper.position - leftArm.upper.position).normalized;
                Vector3 spineUp = (newNeck.position - newChest.position).normalized;
                Vector3 chestFront = Vector3.Cross(across, spineUp).normalized;
                Vector3 surface = chestNow + chestFront * stethoscopeChestDepth;
                Vector3 fine = across * listenFineOffset.x + Vector3.up * listenFineOffset.y + chestFront * listenFineOffset.z;
                wantedShift = scope.position + fine - surface;
                wantedShift.y = 0f;   // never lift her off the floor
            }
            if (wantedShift.magnitude > 0.6f)
                wantedShift = Vector3.zero;   // too far to be the same spot: leave her alone
        }
        listenShift = Vector3.Lerp(listenShift, wantedShift, 1f - Mathf.Exp(-6f * Time.deltaTime));
        newRoot.position = basePosition + listenShift;

        bool listening = playingState == Animator.StringToHash(listenState);
        float smooth = 1f - Mathf.Exp(-3f * Time.deltaTime);

        // How far the animation turns her body away from the direction her root faces.
        if (thighL != null && thighR != null && leftArm.upper != null && rightArm.upper != null)
        {
            Vector3 hipsAcross = Vector3.ProjectOnPlane(thighR.position - thighL.position, Vector3.up);
            Vector3 shouldersAcross = Vector3.ProjectOnPlane(rightArm.upper.position - leftArm.upper.position, Vector3.up);
            Vector3 bodyForward = Vector3.Cross(hipsAcross.normalized + shouldersAcross.normalized, Vector3.up);
            Vector3 rootForward = Vector3.ProjectOnPlane(newRoot.forward, Vector3.up);
            if (bodyForward.sqrMagnitude > 1e-6f && rootForward.sqrMagnitude > 1e-6f && !listening)
                twist = Mathf.LerpAngle(twist, Vector3.SignedAngle(rootForward, bodyForward, Vector3.up), smooth);
            else
                twist = Mathf.LerpAngle(twist, 0f, smooth);
        }

        // Posture: measure how far she leans back and tip the upper body forward by that much.
        if (straightenPosture && newLowSpine != null && newPelvis != null && newNeck != null)
        {
            Vector3 torso = newNeck.position - newPelvis.position;
            Vector3 facing = Vector3.ProjectOnPlane(newRoot.forward, Vector3.up).normalized;
            float measured = Mathf.Atan2(-Vector3.Dot(torso, facing), Mathf.Max(1e-4f, Vector3.Dot(torso, Vector3.up))) * Mathf.Rad2Deg;
            leanBack = Mathf.Lerp(leanBack, listening ? -targetForwardLean : measured, smooth);
            float fix = Mathf.Clamp(leanBack + targetForwardLean, 0f, 25f);
            if (fix > 0.01f)
                newLowSpine.rotation = Quaternion.AngleAxis(fix, newRoot.right) * newLowSpine.rotation;
        }

        // Idle: open the arms a little so the hands clear the thighs.
        bool idle = playingState == Animator.StringToHash(idleState) || playingState == Animator.StringToHash(idleState + " ");
        idleSpreadWeight = Mathf.MoveTowards(idleSpreadWeight, idle ? 1f : 0f, Time.deltaTime / 0.3f);
        if (idleSpreadWeight > 0f && idleArmSpreadDegrees != 0f)
        {
            float angle = idleArmSpreadDegrees * idleSpreadWeight;
            if (rightArm.upper != null)
                rightArm.upper.rotation = Quaternion.AngleAxis(angle, newRoot.forward) * rightArm.upper.rotation;
            if (leftArm.upper != null)
                leftArm.upper.rotation = Quaternion.AngleAxis(-angle, newRoot.forward) * leftArm.upper.rotation;
        }

        UpdateTalkAndClap();

        foreach (KeyValuePair<Transform, Quaternion> face in faceRest)
        {
            if (face.Key != null)
                face.Key.localRotation = face.Value;
        }

        // Watching something (e.g. Yaya's chest while the nurse counts her breathing):
        // the body already faces it (IntendedForward); here the head turns / tilts to it.
        Transform watch = oldMom != null ? oldMom.WatchFocus : null;
        watchWeight = Mathf.MoveTowards(watchWeight, watch != null && newVisible ? 1f : 0f, Time.deltaTime / 0.6f);
        if (watchWeight > 0f && watch != null && newHead != null)
        {
            Vector3 facing = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
            Vector3 toWatch = watch.position - newHead.position;
            if (toWatch.sqrMagnitude > 0.01f)
            {
                Quaternion.FromToRotation(facing, toWatch.normalized).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (axis.sqrMagnitude > 1e-6f)
                    newHead.rotation = Quaternion.AngleAxis(Mathf.Clamp(angle, -45f, 45f) * 0.85f * Mathf.SmoothStep(0f, 1f, watchWeight), axis) * newHead.rotation;
            }
        }

        // Patting Yaya's head, with the new Mom's own arm.
        bool reaching = newVisible && Time.frameCount - reachFrame <= 1;
        bool patting = oldMom != null && oldMom.IsComfortPatting && newVisible && !reaching;
        if (patting && patWeight <= 0f)
        {
            patStartTime = Time.time;
            Vector3 headPosition = oldMom.ComfortHead.position;
            patUsesRight = !leftArm.IsValid
                || (rightArm.IsValid && (rightArm.upper.position - headPosition).sqrMagnitude <= (leftArm.upper.position - headPosition).sqrMagnitude);
        }
        patWeight = Mathf.MoveTowards(patWeight, patting ? 1f : 0f, Time.deltaTime / 0.35f);
        if (patWeight > 0f && oldMom != null && oldMom.ComfortHead != null)
        {
            Transform head = oldMom.ComfortHead;
            Vector3 towardMom = Vector3.ProjectOnPlane(newRoot.position - head.position, Vector3.up).normalized;
            float pat = Mathf.Abs(Mathf.Sin((Time.time - patStartTime) * Mathf.PI * 1.6f)) * 0.025f;
            Vector3 target = head.position + Vector3.up * (oldMom.ComfortHeadSize + pat) + towardMom * 0.03f;
            ArmReachIK.Solve(patUsesRight ? rightArm : leftArm, target, patWeight);
        }

        // Reaching for / holding a prop (ear thermometer).
        if (reaching && reachWeight <= 0f)
        {
            // The arm on the side of what she will point at.
            reachUsesRight = !leftArm.IsValid
                || (rightArm.IsValid && (rightArm.upper.position - reachAim).sqrMagnitude <= (leftArm.upper.position - reachAim).sqrMagnitude);
        }
        reachWeight = Mathf.MoveTowards(reachWeight, reaching ? 1f : 0f, Time.deltaTime / 0.4f);
        if (reachWeight > 0f)
        {
            ArmReachIK.Arm arm = reachUsesRight ? rightArm : leftArm;
            float w = Mathf.SmoothStep(0f, 1f, reachWeight);
            if (arm.IsValid)
            {
                // Lean toward it when it is further than a comfortable reach, and look at it.
                float armLength = Vector3.Distance(arm.upper.position, arm.lower.position) + Vector3.Distance(arm.lower.position, arm.hand.position);
                Vector3 toTarget = reachHandTarget - arm.upper.position;
                Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
                if (newLowSpine != null && flat.sqrMagnitude > 1e-4f)
                {
                    float lean = Mathf.Clamp((toTarget.magnitude / Mathf.Max(0.01f, armLength) - 0.8f) * 70f, 0f, 22f) * w;
                    // Bend down toward something low (Yaya's ear while she sits on the bed),
                    // instead of reaching down with a straight back.
                    float drop = Mathf.Clamp((ChestPosition.y - reachHandTarget.y - 0.2f) * 60f, 0f, 20f) * w;
                    lean = Mathf.Max(lean, drop);
                    if (lean > 0.01f)
                        newLowSpine.rotation = Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, flat.normalized)) * newLowSpine.rotation;
                }
                if (newHead != null)
                {
                    Vector3 facing = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
                    Quaternion.FromToRotation(facing, (reachAim - newHead.position).normalized).ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f) angle -= 360f;
                    if (axis.sqrMagnitude > 1e-6f)
                        newHead.rotation = Quaternion.AngleAxis(Mathf.Clamp(angle, -40f, 40f) * 0.8f * w, axis) * newHead.rotation;
                }

                // Elbow down and a little out to her side (not sticking up or into her body).
                Vector3 outward = Vector3.Cross(Vector3.up, Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward) * (reachUsesRight ? 1f : -1f);
                SolveArm(arm, reachHandTarget, (Vector3.down + outward * 0.45f).normalized, w);
            }
            // Her fingers close softly around it; it points at the target.
            if (reaching && reachProp != null && arm.IsValid)
                HandGrip.Apply(arm.hand, reachUsesRight, reachProp, reachAim, 1f, holdFingerCurl, holdMaxWristBend);
        }

        // Her other hand resting on something (Yaya's head) while she holds the prop.
        bool supporting = newVisible && reaching && Time.frameCount - supportFrame <= 1;
        supportWeight = Mathf.MoveTowards(supportWeight, supporting ? 1f : 0f, Time.deltaTime / 0.45f);
        if (supportWeight > 0f)
        {
            ArmReachIK.Arm other = reachUsesRight ? leftArm : rightArm;
            if (other.IsValid)
            {
                float w = Mathf.SmoothStep(0f, 1f, supportWeight);
                Vector3 outward = Vector3.Cross(Vector3.up, Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward) * (reachUsesRight ? -1f : 1f);
                SolveArm(other, supportTarget, (Vector3.down + outward * 0.5f).normalized, w);
                HandGrip.Curl(other.hand, !reachUsesRight, 0.25f * w);
            }
        }
    }

    // Smaller steps: the walking clip was made for a fast walk, but Mom moves slowly, so her
    // legs (and arm swing) are blended toward her standing pose.
    private void DampWalk()
    {
        if (strideBones == null)
        {
            List<Transform> bones = new List<Transform>();
            foreach (string n in new[] { "thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r", "upperarm_l", "lowerarm_l", "upperarm_r", "lowerarm_r" })
            {
                Transform t = FindByName(newRoot, n);
                if (t != null)
                    bones.Add(t);
            }
            strideBones = bones.ToArray();
            strideRest = new Quaternion[strideBones.Length];
        }

        bool walking = oldMom != null && oldMom.IsWalking;
        bool idle = playingState == Animator.StringToHash(idleState) && !newAnimator.IsInTransition(0);
        if (idle && !walking)
        {
            // Remember the standing pose (straight from the Animator, before anything is added).
            for (int i = 0; i < strideBones.Length; i++)
                strideRest[i] = strideBones[i].localRotation;
            strideRestKnown = true;
        }

        walkDamp = Mathf.MoveTowards(walkDamp, walking ? 1f : 0f, Time.deltaTime / 0.3f);
        if (!strideRestKnown || walkDamp <= 0f)
            return;

        float keep = Mathf.Lerp(1f, Mathf.Clamp01(walkStride), walkDamp);
        for (int i = 0; i < strideBones.Length; i++)
            strideBones[i].localRotation = Quaternion.Slerp(strideRest[i], strideBones[i].localRotation, keep);
    }

    private Transform FindStethoscopeHead()
    {
        if (stethoscopeHead != null && stethoscopeHead.gameObject.activeInHierarchy)
            return stethoscopeHead;
        if (Time.unscaledTime < nextStethoscopeScan)
            return null;
        nextStethoscopeScan = Time.unscaledTime + 0.5f;
        stethoscopeHead = null;
        foreach (SkinnedMeshRenderer smr in performanceMeshes)
        {
            if (smr == null || !smr.gameObject.activeInHierarchy)
                continue;
            foreach (Transform t in smr.transform.root.GetComponentsInChildren<Transform>())
            {
                if (t.name == stethoscopeHeadName)
                {
                    stethoscopeHead = t;
                    return t;
                }
            }
        }
        return null;
    }

    // Her own talking gestures and her own clap, done with the arms of the new model.
    private void UpdateTalkAndClap()
    {
        bool clapping = this.clapping && newVisible;
        talkWeight = Mathf.MoveTowards(talkWeight, talking && newVisible ? 1f : 0f, Time.deltaTime / 0.45f);
        clapWeight = Mathf.MoveTowards(clapWeight, clapping ? 1f : 0f, Time.deltaTime / 0.3f);
        if ((talkWeight <= 0f && clapWeight <= 0f) || newChest == null || newPelvis == null || newNeck == null)
            return;

        // Everything is measured in "torso lengths", so it fits her whatever her scale is.
        float u = Mathf.Max(0.05f, Vector3.Distance(newNeck.position, newPelvis.position));
        Vector3 fwd = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 chest = newChest.position;
        float time = Time.time;

        if (talkWeight > 0f)
        {
            float w = Mathf.SmoothStep(0f, 1f, talkWeight) * Mathf.Clamp(talkGestureAmount, 0f, 1.5f);
            // Right hand: in front of the waist, moving gently as she speaks.
            Vector3 rightHand = chest - Vector3.up * (0.42f * u) + fwd * (0.50f * u) + right * (0.30f * u)
                + Vector3.up * (0.07f * u * Mathf.Sin(time * 2.3f))
                + right * (0.06f * u * Mathf.Sin(time * 1.4f + 1f))
                + fwd * (0.04f * u * Mathf.Sin(time * 1.9f + 2f));
            // Left hand: lower and calmer.
            Vector3 leftHand = chest - Vector3.up * (0.58f * u) + fwd * (0.40f * u) - right * (0.30f * u)
                + Vector3.up * (0.04f * u * Mathf.Sin(time * 1.7f + 0.6f))
                - right * (0.03f * u * Mathf.Sin(time * 1.1f));
            ArmReachIK.Solve(rightArm, rightHand, Mathf.Clamp01(w));
            ArmReachIK.Solve(leftArm, leftHand, Mathf.Clamp01(w * 0.8f));

            // Looks at whoever she is talking to, with a small nod now and then.
            if (newHead != null)
            {
                float nod = 2.5f * Mathf.Sin(time * 2.6f) * talkWeight;
                Quaternion look = Quaternion.identity;
                if (lookAtListener > 0f && oldMom != null && oldMom.TryGetTalkFocus(out Vector3 focus))
                {
                    Vector3 toFocus = focus - newHead.position;
                    if (toFocus.sqrMagnitude > 0.01f)
                    {
                        Quaternion full = Quaternion.FromToRotation(fwd, toFocus.normalized);
                        full.ToAngleAxis(out float angle, out Vector3 axis);
                        if (angle > 180f) angle -= 360f;
                        angle = Mathf.Clamp(angle, -35f, 35f) * lookAtListener * talkWeight;
                        if (axis.sqrMagnitude > 1e-6f)
                            look = Quaternion.AngleAxis(angle, axis);
                    }
                }
                newHead.rotation = look * Quaternion.AngleAxis(nod, right) * newHead.rotation;
            }
        }

        if (clapWeight > 0f && leftArm.IsValid && rightArm.IsValid)
        {
            // A natural clap: elbows bent and close to the body, forearms up in front of the
            // chest, palms facing each other, hands opening and closing.
            float arm = Vector3.Distance(rightArm.upper.position, rightArm.lower.position)
                + Vector3.Distance(rightArm.lower.position, rightArm.hand.position);
            Vector3 shoulders = (leftArm.upper.position + rightArm.upper.position) * 0.5f;
            float beat = time * Mathf.PI * 2f * clapsPerSecond;
            float open = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(beat), 0.8f);           // 0 = palms together
            float halfGap = arm * Mathf.Lerp(0.065f, 0.25f, open);
            Vector3 centre = shoulders + fwd * (arm * clapForward) - Vector3.up * (arm * clapDrop)
                + Vector3.up * (arm * 0.015f * Mathf.Sin(beat));
            // Fingers point forward and up, a little toward the other hand.
            Vector3 fingersRight = (fwd * 0.75f + Vector3.up * 0.65f - right * 0.12f).normalized;
            Vector3 fingersLeft = (fwd * 0.75f + Vector3.up * 0.65f + right * 0.12f).normalized;

            SolveArm(rightArm, centre + right * halfGap, (Vector3.down + right * 0.55f - fwd * 0.15f).normalized, clapWeight);
            SolveArm(leftArm, centre - right * halfGap, (Vector3.down - right * 0.55f - fwd * 0.15f).normalized, clapWeight);
            AimHand(rightArm.hand, fingersRight, clapWeight);
            AimHand(leftArm.hand, fingersLeft, clapWeight);

            // She looks happy about it: a small nod in rhythm.
            if (newHead != null)
                newHead.rotation = Quaternion.AngleAxis(2f * Mathf.Sin(beat * 0.5f) * clapWeight, right) * newHead.rotation;
        }
    }

    // Two-bone IK with a chosen elbow direction.
    private static void SolveArm(ArmReachIK.Arm arm, Vector3 target, Vector3 elbowDirection, float weight)
    {
        if (!arm.IsValid || weight <= 0f)
            return;

        Vector3 a = arm.upper.position;
        float la = Vector3.Distance(a, arm.lower.position);
        float lb = Vector3.Distance(arm.lower.position, arm.hand.position);
        Vector3 toTarget = target - a;
        float d = Mathf.Clamp(toTarget.magnitude, 0.01f, (la + lb) * 0.98f);
        Vector3 dir = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(elbowDirection, dir);
        if (bend.sqrMagnitude < 1e-6f)
            bend = Vector3.ProjectOnPlane(Vector3.down, dir);
        bend.Normalize();

        float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
        Vector3 elbow = a + dir * (cosA * la) + bend * (Mathf.Sqrt(1f - cosA * cosA) * la);

        Quaternion upper = Quaternion.FromToRotation(arm.lower.position - a, elbow - a) * arm.upper.rotation;
        arm.upper.rotation = Quaternion.Slerp(arm.upper.rotation, upper, weight);
        Vector3 wanted = a + dir * d;
        Quaternion lower = Quaternion.FromToRotation(arm.hand.position - arm.lower.position, wanted - arm.lower.position) * arm.lower.rotation;
        arm.lower.rotation = Quaternion.Slerp(arm.lower.rotation, lower, weight);
    }

    // Turns a hand so its fingers point along `fingers`, thumb side up (palms face each other).
    private static void AimHand(Transform hand, Vector3 fingers, float weight)
    {
        Transform index = null, middle = null, pinky = null;
        foreach (Transform child in hand)
        {
            string n = child.name.ToLowerInvariant();
            if (n.Contains("index")) index = child;
            else if (n.Contains("middle")) middle = child;
            else if (n.Contains("pinky") || n.Contains("little")) pinky = child;
        }
        if (index == null || middle == null || pinky == null)
            return;

        Vector3 current = (middle.position - hand.position).normalized;
        Vector3 indexSide = (index.position - pinky.position).normalized;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, fingers).normalized;
        Quaternion turn = Quaternion.LookRotation(fingers, up) * Quaternion.Inverse(Quaternion.LookRotation(current, indexSide));
        hand.rotation = Quaternion.Slerp(hand.rotation, turn * hand.rotation, weight);
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
        foreach (SkinnedMeshRenderer smr in sceneMomMeshes)
        {
            if (smr != null)
                smr.forceRenderingOff = false;
        }
        foreach (SkinnedMeshRenderer smr in performanceMeshes)
        {
            if (smr != null)
                smr.forceRenderingOff = false;
        }
        sceneMomMeshes.Clear();
        performanceMeshes.Clear();
        knownMeshes.Clear();
        if (newRenderers != null)
        {
            newVisible = true;
            SetNewVisible(false);
        }
    }
}
