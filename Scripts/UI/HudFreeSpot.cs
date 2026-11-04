using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Helps small head-locked HUD items (bear sticker, emotion bar) find a spot that does not
// cover the prompt box or the dialogue box. Everything is measured in "view units":
// x / z and y / z as seen from the camera, so panels at different distances can be compared.
public static class HudFreeSpot
{
    public static bool TryGetViewRect(Transform cam, RectTransform rt, out Rect rect)
    {
        rect = default;
        if (cam == null || rt == null)
            return false;

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
        foreach (Vector3 corner in corners)
        {
            Vector3 p = cam.InverseTransformPoint(corner);
            if (p.z < 0.05f)
                return false;
            xMin = Mathf.Min(xMin, p.x / p.z);
            xMax = Mathf.Max(xMax, p.x / p.z);
            yMin = Mathf.Min(yMin, p.y / p.z);
            yMax = Mathf.Max(yMax, p.y / p.z);
        }

        rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return rect.width > 0.0005f && rect.height > 0.0005f;
    }

    // The medical-record button in the corner of the view.
    public static bool TryGetRecordButtonRect(Transform cam, out Rect rect)
    {
        rect = default;
        MedicalRecordHudButton hud = Object.FindObjectOfType<MedicalRecordHudButton>(true);
        if (hud == null)
            return false;

        RectTransform target = null;
        Selectable selectable = hud.GetComponentInChildren<Selectable>(true);
        if (selectable != null)
            target = selectable.transform as RectTransform;

        if (target == null)
        {
            foreach (Graphic graphic in hud.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null && graphic.transform != hud.transform)
                {
                    target = graphic.rectTransform;
                    break;
                }
            }
        }

        return TryGetViewRect(cam, target, out rect);
    }

    // Every visible box of the prompt / dialogue canvases attached to the camera
    // (also the ones that are hidden right now, so the result does not change later).
    public static List<Rect> CollectPanelRects(Transform cam)
    {
        List<Rect> rects = new List<Rect>();
        if (cam == null)
            return rects;

        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>(true))
        {
            if (canvas == null || !canvas.transform.IsChildOf(cam))
                continue;

            string n = canvas.name;
            if (!n.Contains("Dialogue_Canva") && !n.Contains("Instruction_Canvas"))
                continue;

            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image == null || image.color.a < 0.05f)
                    continue;
                if (!TryGetViewRect(cam, image.rectTransform, out Rect r))
                    continue;
                if (r.width > 1.3f || r.height > 0.7f)
                    continue; // full-screen helper image, not a box
                rects.Add(r);
            }
        }

        return rects;
    }

    public static bool IsFree(List<Rect> blockers, float x, float halfWidth, float halfHeight, float y, float margin)
    {
        Rect me = Rect.MinMaxRect(x - halfWidth - margin, y - halfHeight - margin, x + halfWidth + margin, y + halfHeight + margin);
        foreach (Rect blocker in blockers)
        {
            if (blocker.Overlaps(me))
                return false;
        }
        return true;
    }

    // Slides an item (centre x, half size) down or up from startY until it covers no box.
    public static bool TrySlide(List<Rect> blockers, float x, float halfWidth, float halfHeight,
        float startY, bool down, float margin, float limit, out float y)
    {
        y = startY;
        for (int i = 0; i < 16; i++)
        {
            Rect me = Rect.MinMaxRect(x - halfWidth - margin, y - halfHeight - margin, x + halfWidth + margin, y + halfHeight + margin);
            bool moved = false;
            foreach (Rect blocker in blockers)
            {
                if (!blocker.Overlaps(me))
                    continue;
                y = down ? blocker.yMin - margin - halfHeight - 0.001f : blocker.yMax + margin + halfHeight + 0.001f;
                moved = true;
                break;
            }

            if (!moved)
                return Mathf.Abs(y) <= limit;
        }

        return false;
    }
}
