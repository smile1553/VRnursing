using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class PrefabPerformanceRuntime
{
    private enum AlignmentMode { ChildMatch, Root, BoundsCenter }

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

        if (anchor != null)
        {
            if (alignmentMode == AlignmentMode.Root)
                AlignRootToAnchor(instanceTransform, anchor);
            else if (alignmentMode == AlignmentMode.BoundsCenter)
                AlignBoundsToTargets(instance, hideWhilePlaying);
            else
                AlignInstanceToAnchor(instanceTransform, anchor);
        }

        List<Renderer> hiddenRenderers = alignmentMode == AlignmentMode.ChildMatch
            ? SetRenderersVisible(hideWhilePlaying, false)
            : new List<Renderer>();
        PlayAnimators(instance);

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
