using UnityEngine;

public static class WorldSpaceUiPlacer
{
    private static readonly Vector3 DefaultLocalOffset = new Vector3(0f, -0.02f, 1.2f);
    private static readonly Vector3 DefaultLocalEulerAngles = Vector3.zero;
    private const string DefaultQuizReferenceName = "Quiz_Panel_1";

    public static void PlaceCanvasInFrontOfCamera(GameObject target)
    {
        PlaceCanvasInFrontOfCamera(target, DefaultLocalOffset, DefaultLocalEulerAngles);
    }

    public static void PlaceCanvasInFrontOfCamera(GameObject target, Vector3 localOffset, Vector3 localEulerAngles)
    {
        if (target == null || Camera.main == null)
            return;

        Transform root = FindCanvasRoot(target.transform);
        if (root == null)
            root = target.transform;

        Transform head = Camera.main.transform;
        root.position = head.TransformPoint(localOffset);
        root.rotation = head.rotation * Quaternion.Euler(localEulerAngles);
    }

    public static void MatchQuizPanelToQuizOne(GameObject rootOrPanel, string panelChildName)
    {
        MatchChildRectTransformToReference(rootOrPanel, panelChildName, DefaultQuizReferenceName);
        MatchChildLayoutToReference(rootOrPanel, panelChildName, DefaultQuizReferenceName);
        HideOtherQuizPanels(rootOrPanel, panelChildName);
    }

    public static void MatchChildRectTransformToReference(GameObject rootOrPanel, string panelChildName, string referenceName)
    {
        if (rootOrPanel == null)
            return;

        Transform target = string.IsNullOrWhiteSpace(panelChildName)
            ? rootOrPanel.transform
            : FindDeepChild(rootOrPanel.transform, panelChildName) ?? rootOrPanel.transform;

        RectTransform targetRect = target.GetComponent<RectTransform>();
        RectTransform referenceRect = FindSceneRectTransform(referenceName);

        if (targetRect == null || referenceRect == null || targetRect == referenceRect)
            return;

        targetRect.anchorMin = referenceRect.anchorMin;
        targetRect.anchorMax = referenceRect.anchorMax;
        targetRect.pivot = referenceRect.pivot;
        targetRect.anchoredPosition3D = referenceRect.anchoredPosition3D;
        targetRect.sizeDelta = referenceRect.sizeDelta;
        targetRect.localRotation = referenceRect.localRotation;
        targetRect.localScale = referenceRect.localScale;
    }

    public static void MatchChildLayoutToReference(GameObject rootOrPanel, string panelChildName, string referenceName)
    {
        if (rootOrPanel == null)
            return;

        Transform target = string.IsNullOrWhiteSpace(panelChildName)
            ? rootOrPanel.transform
            : FindDeepChild(rootOrPanel.transform, panelChildName) ?? rootOrPanel.transform;

        RectTransform referenceRect = FindSceneRectTransform(referenceName);
        if (target == null || referenceRect == null || target == referenceRect.transform)
            return;

        CopyChildRectLayout(referenceRect.transform, target);
    }

    public static void HideOtherQuizPanels(GameObject rootOrPanel, string panelChildName)
    {
        if (rootOrPanel == null || string.IsNullOrWhiteSpace(panelChildName))
            return;

        Transform target = FindDeepChild(rootOrPanel.transform, panelChildName);
        Transform root = target != null && target.parent != null ? target.parent : rootOrPanel.transform.parent;
        if (root == null)
            root = rootOrPanel.transform;

        foreach (Transform child in root)
        {
            if (child == null || !child.name.StartsWith("Quiz_Panel_"))
                continue;

            child.gameObject.SetActive(child.name == panelChildName);
        }
    }

    private static Transform FindCanvasRoot(Transform start)
    {
        Transform current = start;
        Transform best = null;

        while (current != null)
        {
            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas != null)
            {
                best = current;
                if (canvas.renderMode == RenderMode.WorldSpace)
                    return current;
            }

            current = current.parent;
        }

        return best;
    }

    private static RectTransform FindSceneRectTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject found = GameObject.Find(objectName);
        if (found != null)
            return found.GetComponent<RectTransform>();

        RectTransform[] rects = Resources.FindObjectsOfTypeAll<RectTransform>();
        foreach (RectTransform rect in rects)
        {
            if (rect != null && rect.gameObject.scene.IsValid() && rect.name == objectName)
                return rect;
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void CopyChildRectLayout(Transform reference, Transform target)
    {
        int count = Mathf.Min(reference.childCount, target.childCount);
        for (int i = 0; i < count; i++)
        {
            Transform referenceChild = reference.GetChild(i);
            Transform targetChild = FindDeepChild(target, referenceChild.name);
            if (targetChild == null)
                targetChild = target.GetChild(i);

            RectTransform referenceRect = referenceChild.GetComponent<RectTransform>();
            RectTransform targetRect = targetChild.GetComponent<RectTransform>();
            if (referenceRect != null && targetRect != null)
            {
                targetRect.anchorMin = referenceRect.anchorMin;
                targetRect.anchorMax = referenceRect.anchorMax;
                targetRect.pivot = referenceRect.pivot;
                targetRect.anchoredPosition3D = referenceRect.anchoredPosition3D;
                targetRect.sizeDelta = referenceRect.sizeDelta;
                targetRect.localRotation = referenceRect.localRotation;
                targetRect.localScale = referenceRect.localScale;
            }

            CopyChildRectLayout(referenceChild, targetChild);
        }
    }
}
