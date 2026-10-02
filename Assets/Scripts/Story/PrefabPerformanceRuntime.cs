using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class PrefabPerformanceRuntime
{
    private enum AlignmentMode { ChildMatch, Root, BoundsCenter, Bones }

    public static bool TryPlay(MonoBehaviour owner, string prefabNames, float duration, params GameObject[] hideWhilePlaying)
    {
        if (owner == null)
            return false;

        GameObject prefabOrSceneObject = FindPrefabOrSceneObject(prefabNames);
        if (prefabOrSceneObject == null)
            return false;

        owner.StartCoroutine(PlayRoutine(prefabOrSceneObject, duration, AlignmentMode.ChildMatch, hideWhilePlaying));
        return true;
    }


    public static bool TryPlayRootAligned(MonoBehaviour owner, string prefabNames, float duration, params GameObject[] hideWhilePlaying)
    {
        if (owner == null)
            return false;

        GameObject prefabOrSceneObject = FindPrefabOrSceneObject(prefabNames);
        if (prefabOrSceneObject == null)
            return false;

        owner.StartCoroutine(PlayRoutine(prefabOrSceneObject, duration, AlignmentMode.Root, hideWhilePlaying));
        return true;
    }

    // Aligns the performance model to the target character by its Mixamo skeleton:
    // same facing, same size (Hips->Head length) and Hips placed on the target's Hips.
    // Use this when the performance prefab comes from a different FBX with a different scale.
    public static bool TryPlayBoneAligned(MonoBehaviour owner, string prefabNames, float duration, params GameObject[] hideWhilePlaying)
    {
        if (owner == null)
            return false;

        GameObject prefabOrSceneObject = FindPrefabOrSceneObject(prefabNames);
        if (prefabOrSceneObject == null)
            return false;

        owner.StartCoroutine(PlayRoutine(prefabOrSceneObject, duration, AlignmentMode.Bones, hideWhilePlaying));
        return true;
    }

    public static bool TryPlayBoundsAligned(MonoBehaviour owner, string prefabNames, float duration, params GameObject[] hideWhilePlaying)
    {
        if (owner == null)
            return false;

        GameObject prefabOrSceneObject = FindPrefabOrSceneObject(prefabNames);
        if (prefabOrSceneObject == null)
            return false;

        owner.StartCoroutine(PlayRoutine(prefabOrSceneObject, duration, AlignmentMode.BoundsCenter, hideWhilePlaying));
        return true;
    }
    private static IEnumerator PlayRoutine(GameObject prefabOrSceneObject, float duration, AlignmentMode alignmentMode, GameObject[] hideWhilePlaying)
    {
        bool isSceneObject = prefabOrSceneObject.scene.IsValid();
        Transform anchor = GetAnchor(hideWhilePlaying);
        GameObject instance = isSceneObject ? prefabOrSceneObject : Object.Instantiate(prefabOrSceneObject);
        if (instance == null)
            yield break;

        instance.SetActive(true);
        SetAllRenderersVisible(instance, true);

        Transform instanceTransform = instance.transform;
        Vector3 originalPosition = instanceTransform.position;
        Quaternion originalRotation = instanceTransform.rotation;
        Vector3 originalScale = instanceTransform.localScale;

        if (alignmentMode == AlignmentMode.Bones)
        {
            // Pose the instance first so the bones are where the clip puts them, then align.
            PlayAnimators(instance);
            if (anchor == null || !AlignByBones(instance.transform, anchor))
            {
                if (anchor != null)
                    Debug.LogWarning($"[PrefabPerformanceRuntime] Bone align failed for {instance.name}; falling back to bounds align.", instance);
                AlignBoundsToTargets(instance, hideWhilePlaying);
            }
        }
        else if (anchor != null)
        {
            if (alignmentMode == AlignmentMode.Root)
                AlignRootToAnchor(instanceTransform, anchor);
            else if (alignmentMode == AlignmentMode.BoundsCenter)
                AlignBoundsToTargets(instance, hideWhilePlaying);
            else
                AlignInstanceToAnchor(instanceTransform, anchor);
        }

        List<Renderer> hiddenRenderers = alignmentMode == AlignmentMode.ChildMatch || alignmentMode == AlignmentMode.Bones
            ? SetRenderersVisible(hideWhilePlaying, false)
            : new List<Renderer>();
        if (alignmentMode != AlignmentMode.Bones)
            PlayAnimators(instance);

        if (alignmentMode == AlignmentMode.Bones)
            yield return SlowThenHoldAnimators(instance, Mathf.Max(0.1f, duration), 0.55f);
        else
            yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

        SetRenderersVisible(hiddenRenderers, true);

        if (isSceneObject)
        {
            instanceTransform.SetPositionAndRotation(originalPosition, originalRotation);
            instanceTransform.localScale = originalScale;
            instance.SetActive(false);
        }
        else
        {
            Object.Destroy(instance);
        }
    }

    // ---------------------------------------------------------------------------------
    // Long-running performance (e.g. ForGan Listen_Mom_Kid: mom + kid + stethoscope).
    // Plays until handle.Stop() is called (or maxDuration). Cleanup lives in the handle,
    // so it still runs if the owner's coroutines are stopped.
    // ---------------------------------------------------------------------------------
    public sealed class PerformanceHandle
    {
        internal GameObject instance;
        internal bool instanceIsSceneObject;
        internal Vector3 sceneObjectPosition;
        internal Quaternion sceneObjectRotation;
        internal Vector3 sceneObjectScale;
        internal readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        internal readonly List<Renderer> instanceRenderers = new List<Renderer>();
        internal Animator[] animators;
        internal readonly Dictionary<Animator, float> swayStart = new Dictionary<Animator, float>();
        public bool IsStopped { get; private set; }
        public bool IsRevealed { get; internal set; } = true;

        // Shows a performance started with startHidden=true and restarts its animation from frame 0.
        public void Reveal()
        {
            if (IsStopped || IsRevealed || instance == null)
                return;

            IsRevealed = true;
            foreach (Renderer renderer in instanceRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
            instanceRenderers.Clear();
            swayStart.Clear();
            PlayAnimators(instance);
        }

        // World position + facing (from the pelvis) of a named actor inside the performance.
        public bool TryGetActorPose(string childName, out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            if (instance == null)
                return false;

            Transform child = FindChildByName(instance.transform, childName);
            if (child == null)
                return false;

            // Use the pelvis (not the FBX root, which can sit elsewhere) for where the actor stands.
            Transform hips = MixamoHumanoidAvatarBuilder.FindBone(child, "Hips");
            position = hips != null ? new Vector3(hips.position.x, child.position.y, hips.position.z) : child.position;
            forward = GetPelvisForward(child);
            return true;
        }

        public void Stop()
        {
            if (IsStopped)
                return;

            IsStopped = true;
            foreach (Renderer renderer in hiddenRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
            hiddenRenderers.Clear();

            if (instance == null)
                return;

            if (instanceIsSceneObject)
            {
                instance.transform.SetPositionAndRotation(sceneObjectPosition, sceneObjectRotation);
                instance.transform.localScale = sceneObjectScale;
                instance.SetActive(false);
            }
            else
            {
                Object.Destroy(instance);
            }
            instance = null;
        }
    }

    /// <param name="alignChildName">Child of the prefab whose skeleton is aligned onto <paramref name="alignTarget"/> (e.g. "Kid").</param>
    /// <param name="secondaryChildName">Optional child (e.g. "Mom") resized to <paramref name="secondaryTarget"/> and put on its floor height.</param>
    public static PerformanceHandle StartBoneAligned(MonoBehaviour owner, string prefabNames, string alignChildName, GameObject alignTarget,
        string secondaryChildName, GameObject secondaryTarget, bool keepSecondaryInPlace, float maxDuration, params GameObject[] alsoHide)
    {
        return StartBoneAligned(owner, prefabNames, alignChildName, alignTarget, secondaryChildName, secondaryTarget,
            keepSecondaryInPlace, 0f, maxDuration, alsoHide);
    }

    /// <param name="alignTurnTowardSecondary">0..1: how much the aligned actor (kid) turns to face the secondary scene actor (mom).</param>
    public static PerformanceHandle StartBoneAligned(MonoBehaviour owner, string prefabNames, string alignChildName, GameObject alignTarget,
        string secondaryChildName, GameObject secondaryTarget, bool keepSecondaryInPlace, float alignTurnTowardSecondary, float maxDuration, params GameObject[] alsoHide)
    {
        return StartBoneAligned(owner, prefabNames, alignChildName, alignTarget, secondaryChildName, secondaryTarget,
            keepSecondaryInPlace, alignTurnTowardSecondary, false, maxDuration, alsoHide);
    }

    /// <param name="startHidden">Build and align the performance but keep it invisible (scene actors stay visible) until handle.Reveal().</param>
    public static PerformanceHandle StartBoneAligned(MonoBehaviour owner, string prefabNames, string alignChildName, GameObject alignTarget,
        string secondaryChildName, GameObject secondaryTarget, bool keepSecondaryInPlace, float alignTurnTowardSecondary, bool startHidden,
        float maxDuration, params GameObject[] alsoHide)
    {
        if (owner == null || alignTarget == null)
            return null;

        GameObject source = FindPrefabOrSceneObject(prefabNames);
        if (source == null)
            return null;

        PerformanceHandle handle = new PerformanceHandle();
        handle.instanceIsSceneObject = source.scene.IsValid();
        GameObject instance = handle.instanceIsSceneObject ? source : Object.Instantiate(source);
        if (instance == null)
            return null;

        handle.instance = instance;
        handle.sceneObjectPosition = instance.transform.position;
        handle.sceneObjectRotation = instance.transform.rotation;
        handle.sceneObjectScale = instance.transform.localScale;

        instance.SetActive(true);
        SetAllRenderersVisible(instance, true);
        PlayAnimators(instance);

        Transform alignChild = FindChildByName(instance.transform, alignChildName) ?? instance.transform;
        if (AlignByBones(instance.transform, alignChild, alignTarget.transform, out float alignScale))
        {
            ScaleStethoscopeFollowers(instance, alignScale);
        }
        else
        {
            Debug.LogWarning($"[PrefabPerformanceRuntime] Bone align failed for {instance.name}; falling back to bounds align.", instance);
            AlignBoundsToTargets(instance, new[] { alignTarget });
        }

        Transform secondaryChild = FindChildByName(instance.transform, secondaryChildName);
        if (keepSecondaryInPlace && secondaryTarget != null && alignTurnTowardSecondary > 0f)
            TurnAroundHipsToward(instance.transform, alignChild, secondaryTarget.transform, alignTurnTowardSecondary);
        if (secondaryChild != null && secondaryTarget != null)
            MatchSecondaryActor(secondaryChild, secondaryTarget.transform, keepSecondaryInPlace ? alignTarget.transform : null);

        List<GameObject> hideRoots = new List<GameObject> { alignTarget, secondaryTarget };
        if (alsoHide != null)
            hideRoots.AddRange(alsoHide);
        foreach (GameObject root in hideRoots)
        {
            if (root == null)
                continue;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.enabled)
                    handle.hiddenRenderers.Add(renderer);
            }
        }

        if (startHidden)
        {
            handle.IsRevealed = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.enabled)
                {
                    renderer.enabled = false;
                    handle.instanceRenderers.Add(renderer);
                }
            }
        }

        owner.StartCoroutine(HoldPerformanceRoutine(handle, maxDuration));
        return handle;
    }

    private static IEnumerator HoldPerformanceRoutine(PerformanceHandle handle, float maxDuration)
    {
        float endTime = maxDuration > 0f ? Time.time + maxDuration : float.PositiveInfinity;
        while (!handle.IsStopped && Time.time < endTime)
        {
            if (!handle.IsRevealed)
            {
                yield return null;
                continue;
            }

            KeepPerformanceAlive(handle);

            // Other scripts (e.g. animation players) may re-enable renderers; keep them hidden.
            foreach (Renderer renderer in handle.hiddenRenderers)
            {
                if (renderer != null && renderer.enabled)
                    renderer.enabled = false;
            }
            yield return null;
        }

        handle.Stop();
    }

    // The stethoscope parts follow the kid's bones with offsets stored in world units of the
    // original prefab scale; after the prefab is resized those offsets must be resized too.
    private static void ScaleStethoscopeFollowers(GameObject instance, float scale)
    {
        if (instance == null || Mathf.Approximately(scale, 1f))
            return;

        foreach (ForGanStethoscopeFollow follow in instance.GetComponentsInChildren<ForGanStethoscopeFollow>(true))
        {
            follow.headPosOffset *= scale;
            follow.handPosOffset *= scale;
            follow.tubeRadius *= scale;
            LineRenderer line = follow.GetComponent<LineRenderer>();
            if (line != null)
                line.startWidth = line.endWidth = follow.tubeRadius * 2f;
        }
    }

    // faceTowards != null: keep the actor where the scene actor stands (no teleport) and turn it toward faceTowards.
    // Rotate the whole performance around the aligned actor's hips so it faces `towards`.
    private static void TurnAroundHipsToward(Transform instance, Transform boneRoot, Transform towards, float amount)
    {
        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(boneRoot, "Hips");
        Transform leftLeg = MixamoHumanoidAvatarBuilder.FindBone(boneRoot, "LeftUpLeg");
        Transform rightLeg = MixamoHumanoidAvatarBuilder.FindBone(boneRoot, "RightUpLeg");
        if (hips == null || leftLeg == null || rightLeg == null || towards == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(Vector3.Cross(rightLeg.position - leftLeg.position, Vector3.up), Vector3.up);
        Vector3 wanted = Vector3.ProjectOnPlane(towards.position - hips.position, Vector3.up);
        if (forward.sqrMagnitude < 1e-8f || wanted.sqrMagnitude < 1e-8f)
            return;

        float yaw = Vector3.SignedAngle(forward, wanted, Vector3.up) * Mathf.Clamp01(amount);
        instance.RotateAround(hips.position, Vector3.up, yaw);
    }

    private static Vector3 GetPelvisForward(Transform root)
    {
        Transform leftLeg = MixamoHumanoidAvatarBuilder.FindBone(root, "LeftUpLeg");
        Transform rightLeg = MixamoHumanoidAvatarBuilder.FindBone(root, "RightUpLeg");
        Vector3 forward = leftLeg != null && rightLeg != null
            ? Vector3.Cross(rightLeg.position - leftLeg.position, Vector3.up)
            : root.forward;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        return forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
    }

    public static Vector3 GetActorPelvisForward(Transform actorRoot)
    {
        return GetPelvisForward(actorRoot);
    }

    // Non-looping clips (e.g. Mom bending to be listened to) would freeze on the last frame.
    // Once a clip has ended, gently sway between its last 25% and the end so it stays alive.
    private static void KeepPerformanceAlive(PerformanceHandle handle)
    {
        if (handle.instance == null)
            return;

        if (handle.animators == null)
            handle.animators = handle.instance.GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in handle.animators)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
                continue;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            bool swaying = handle.swayStart.TryGetValue(animator, out float start);
            if (!swaying)
            {
                if (info.loop || info.normalizedTime < 1f)
                    continue;
                start = Time.time;
                handle.swayStart[animator] = start;
                animator.speed = 0f;
            }

            float period = Mathf.Max(2.5f, info.length * 1.2f);
            float phase = 0.5f - 0.5f * Mathf.Cos((Time.time - start) * Mathf.PI * 2f / period);
            animator.Play(info.fullPathHash, 0, 0.999f - 0.25f * phase);
        }
    }

    private static void MatchSecondaryActor(Transform child, Transform target, Transform faceTowards)
    {
        Transform cHips = MixamoHumanoidAvatarBuilder.FindBone(child, "Hips");
        Transform cHead = MixamoHumanoidAvatarBuilder.FindBone(child, "Head");
        Transform tHips = MixamoHumanoidAvatarBuilder.FindBone(target, "Hips");
        Transform tHead = MixamoHumanoidAvatarBuilder.FindBone(target, "Head");
        if (cHips != null && cHead != null && tHips != null && tHead != null)
        {
            float cLength = Vector3.Distance(cHips.position, cHead.position);
            float tLength = Vector3.Distance(tHips.position, tHead.position);
            if (cLength > 1e-5f && tLength > 1e-5f)
                child.localScale *= tLength / cLength;
        }

        // Feet on the same floor as the scene actor (actor roots are at the feet).
        Vector3 p = child.position;
        if (faceTowards != null)
        {
            p.x = target.position.x;
            p.z = target.position.z;
        }
        p.y = target.position.y;
        child.position = p;

        if (faceTowards == null)
            return;

        Transform lookAt = MixamoHumanoidAvatarBuilder.FindBone(faceTowards, "Hips") ?? faceTowards;
        Transform cLeftLeg = MixamoHumanoidAvatarBuilder.FindBone(child, "LeftUpLeg");
        Transform cRightLeg = MixamoHumanoidAvatarBuilder.FindBone(child, "RightUpLeg");
        Vector3 currentForward = cLeftLeg != null && cRightLeg != null
            ? Vector3.Cross(cRightLeg.position - cLeftLeg.position, Vector3.up)
            : child.forward;
        currentForward = Vector3.ProjectOnPlane(currentForward, Vector3.up);
        Vector3 wantedForward = Vector3.ProjectOnPlane(lookAt.position - child.position, Vector3.up);
        if (currentForward.sqrMagnitude > 1e-8f && wantedForward.sqrMagnitude > 1e-8f)
        {
            float yaw = Vector3.SignedAngle(currentForward, wantedForward, Vector3.up);
            child.RotateAround(child.position, Vector3.up, yaw);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != root && string.Equals(t.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private static bool AlignByBones(Transform instance, Transform target)
    {
        return AlignByBones(instance, instance, target, out _);
    }

    // Moves/rotates/scales `instance`, measuring bones under `boneRoot`.
    private static bool AlignByBones(Transform instance, Transform boneRoot, Transform target, out float scaleFactor)
    {
        scaleFactor = 1f;
        Transform tHips = MixamoHumanoidAvatarBuilder.FindBone(target, "Hips");
        Transform tHead = MixamoHumanoidAvatarBuilder.FindBone(target, "Head");
        Transform tLeftLeg = MixamoHumanoidAvatarBuilder.FindBone(target, "LeftUpLeg");
        Transform tRightLeg = MixamoHumanoidAvatarBuilder.FindBone(target, "RightUpLeg");
        Transform iHips = MixamoHumanoidAvatarBuilder.FindBone(boneRoot, "Hips");
        Transform iHead = MixamoHumanoidAvatarBuilder.FindBone(boneRoot, "Head");
        Transform iLeftLeg = MixamoHumanoidAvatarBuilder.FindBone(boneRoot, "LeftUpLeg");
        Transform iRightLeg = MixamoHumanoidAvatarBuilder.FindBone(boneRoot, "RightUpLeg");
        if (tHips == null || tHead == null || tLeftLeg == null || tRightLeg == null ||
            iHips == null || iHead == null || iLeftLeg == null || iRightLeg == null)
            return false;

        // 1) Facing: rotate around world up so the pelvis faces the same way.
        Vector3 targetForward = Vector3.ProjectOnPlane(Vector3.Cross(tRightLeg.position - tLeftLeg.position, Vector3.up), Vector3.up);
        Vector3 instanceForward = Vector3.ProjectOnPlane(Vector3.Cross(iRightLeg.position - iLeftLeg.position, Vector3.up), Vector3.up);
        if (targetForward.sqrMagnitude > 1e-8f && instanceForward.sqrMagnitude > 1e-8f)
        {
            float yaw = Vector3.SignedAngle(instanceForward, targetForward, Vector3.up);
            instance.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * instance.rotation;
        }

        // 2) Size: match Hips->Head length.
        float targetLength = Vector3.Distance(tHips.position, tHead.position);
        float instanceLength = Vector3.Distance(iHips.position, iHead.position);
        scaleFactor = targetLength > 1e-5f && instanceLength > 1e-5f ? targetLength / instanceLength : 1f;
        instance.localScale *= scaleFactor;

        // 3) Position: put Hips on Hips.
        instance.position += tHips.position - iHips.position;
        Debug.Log($"[PrefabPerformanceRuntime] Bone-aligned {instance.name} to {target.name} (scale x{scaleFactor:0.###}).", instance);
        return true;
    }

    // Plays the clip at `speed` (slower than authored), then keeps the final gesture alive
    // (gentle sway over the last part, e.g. hand stays at the ear) until `duration` ends.
    private static IEnumerator SlowThenHoldAnimators(GameObject instance, float duration, float speed)
    {
        Animator animator = instance != null ? instance.GetComponentInChildren<Animator>(true) : null;
        float endTime = Time.time + duration;
        yield return null;
        if (animator == null || !animator.isActiveAndEnabled)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, endTime - Time.time));
            yield break;
        }

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        float length = info.length / Mathf.Max(0.05f, speed);
        if (length <= 0.05f)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, endTime - Time.time));
            yield break;
        }

        int stateHash = info.fullPathHash;
        animator.speed = 0f;
        float t = 0f;
        while (instance != null && Time.time < endTime)
        {
            t += Time.deltaTime;
            float n;
            if (t < length)
                n = t / length;
            else
            {
                float sway = 0.5f - 0.5f * Mathf.Cos((t - length) * Mathf.PI * 2f / 2.4f);
                n = 1f - 0.15f * sway;
            }
            animator.Play(stateHash, 0, Mathf.Clamp01(n) * 0.999f);
            yield return null;
        }
    }

    // Plays the clip forward, holds briefly, then plays it backward so the pose returns to
    // the start (e.g. Yaya lowers her hand again) instead of freezing on the last frame.
    // Ends as soon as the pose is back at the start, so the scene actor takes over smoothly.
    private static IEnumerator PingPongAnimators(GameObject instance, float maxDuration)
    {
        Animator animator = instance != null ? instance.GetComponentInChildren<Animator>(true) : null;
        yield return null; // let the animator enter its state
        if (animator == null || !animator.isActiveAndEnabled)
        {
            yield return new WaitForSeconds(maxDuration);
            yield break;
        }

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        float length = info.length;
        if (length <= 0.05f)
        {
            yield return new WaitForSeconds(maxDuration);
            yield break;
        }

        const float hold = 0.6f;
        length = Mathf.Min(length, Mathf.Max(0.3f, (maxDuration - hold) * 0.5f));
        int stateHash = info.fullPathHash;
        float previousSpeed = animator.speed;
        animator.speed = 0f;

        float total = length * 2f + hold;
        for (float t = 0f; t < total && instance != null; t += Time.deltaTime)
        {
            float n = t < length ? t / length : (t < length + hold ? 1f : 1f - (t - length - hold) / length);
            animator.Play(stateHash, 0, Mathf.Clamp01(n) * 0.999f);
            yield return null;
        }

        if (animator != null)
            animator.speed = previousSpeed;
    }

    private static void AlignBoundsToTargets(GameObject instance, GameObject[] targets)
    {
        if (instance == null || targets == null)
            return;

        if (!TryGetRendererBounds(instance, out Bounds instanceBounds) || !TryGetRendererBounds(targets, out Bounds targetBounds))
            return;

        instance.transform.position += targetBounds.center - instanceBounds.center;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        return TryBuildBounds(renderers, out bounds);
    }

    private static bool TryGetRendererBounds(GameObject[] roots, out Bounds bounds)
    {
        bounds = new Bounds();
        if (roots == null)
            return false;

        List<Renderer> renderers = new List<Renderer>();
        foreach (GameObject root in roots)
        {
            if (root == null)
                continue;

            renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
        }

        return TryBuildBounds(renderers, out bounds);
    }

    private static bool TryBuildBounds(IEnumerable<Renderer> renderers, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
    private static void AlignRootToAnchor(Transform instance, Transform anchor)
    {
        if (instance == null || anchor == null)
            return;

        instance.SetPositionAndRotation(anchor.position, anchor.rotation);
        instance.localScale = anchor.lossyScale;
    }
    private static void AlignInstanceToAnchor(Transform instance, Transform anchor)
    {
        if (instance == null || anchor == null)
            return;

        instance.localScale = anchor.lossyScale;

        Transform match = FindBestMatchingChild(instance, anchor.name) ?? instance;
        Quaternion rotationDelta = anchor.rotation * Quaternion.Inverse(match.rotation);
        instance.rotation = rotationDelta * instance.rotation;
        instance.position += anchor.position - match.position;
    }

    private static Transform FindBestMatchingChild(Transform root, string anchorName)
    {
        if (root == null || string.IsNullOrWhiteSpace(anchorName))
            return null;

        string normalizedAnchor = NormalizeName(anchorName);
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (NormalizeName(child.name) == normalizedAnchor)
                return child;
        }

        foreach (Transform child in children)
        {
            string normalizedChild = NormalizeName(child.name);
            if (normalizedChild.Contains(normalizedAnchor) || normalizedAnchor.Contains(normalizedChild))
                return child;
        }

        return null;
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }
    private static Transform GetAnchor(GameObject[] roots)
    {
        if (roots == null)
            return null;

        foreach (GameObject root in roots)
        {
            if (root != null)
                return root.transform;
        }

        return null;
    }
    private static void PlayAnimators(GameObject root)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        foreach (Animator animator in animators)
        {
            if (animator == null)
                continue;

            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private static void SetAllRenderersVisible(GameObject root, bool visible)
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
    private static List<Renderer> SetRenderersVisible(GameObject[] roots, bool visible)
    {
        List<Renderer> changed = new List<Renderer>();
        if (roots == null)
            return changed;

        foreach (GameObject root in roots)
        {
            if (root == null)
                continue;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.enabled == visible)
                    continue;

                renderer.enabled = visible;
                changed.Add(renderer);
            }
        }

        return changed;
    }

    private static void SetRenderersVisible(List<Renderer> renderers, bool visible)
    {
        if (renderers == null)
            return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private static GameObject FindPrefabOrSceneObject(string prefabNames)
    {
        if (string.IsNullOrWhiteSpace(prefabNames))
            return null;

        string[] names = prefabNames.Split('|');
        foreach (string rawName in names)
        {
            string wantedName = rawName.Trim();
            if (string.IsNullOrEmpty(wantedName))
                continue;

            GameObject sceneObject = FindSceneObjectByName(wantedName);
            if (sceneObject != null)
                return sceneObject;

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets($"{wantedName} t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.name.IndexOf(wantedName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return prefab;
            }
#endif
        }

        return null;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.name == objectName)
                return candidate.gameObject;
        }

        return null;
    }
}
