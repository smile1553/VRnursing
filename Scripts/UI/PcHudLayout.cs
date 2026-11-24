using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Computer version only (Windows / Mac, no headset). Quest and headset play are NOT touched.
//
// In the headset the floating display (prompt, dialogue, medical-record button, emotion bar,
// timer) sits close together in front of the eyes, which looks right there. On a flat screen
// it all ends up in the middle and covers Yaya, so here each piece is moved to an edge:
//   prompt box       top centre
//   dialogue box     bottom centre (like subtitles)
//   record button    top left
//   emotion bar      right side
//   breathing timer  left side
// Positions are in screen fractions (0 = left / bottom, 1 = right / top) and can be changed
// on the "PcHudLayout" object while playing.
[DefaultExecutionOrder(32000)]
public class PcHudLayout : MonoBehaviour
{
    [System.Serializable]
    public class Item
    {
        public string label;
        [Tooltip("Object name contains this (the floating canvas / object).")]
        public string nameContains;
        [Tooltip("Where on the screen (0..1, 0..1).")]
        public Vector2 screenPoint;
        [Tooltip("Which point of the item goes there (0,0 = its bottom-left, 1,1 = its top-right).")]
        public Vector2 itemPivot;
        [Tooltip("Size on the computer compared with the headset (1 = same, 1.5 = half again bigger).")]
        public float scale = 1f;
        [Tooltip("Make the box wider (text gets longer lines, letters keep their size). 1 = as in the headset.")]
        public float widen = 1f;
        [Tooltip("On: moved to Screen Point. Off: stays where the story puts it, only made bigger (quiz, cards, end screen).")]
        public bool moveToScreenPoint = true;
        public bool enabled = true;
    }

    [Tooltip("Field of view on the computer (degrees, up-down). Wider = more of the room visible. 0 = leave as it is.")]
    public float fieldOfView = 0f;
    [Tooltip("Makes every piece bigger on the computer screen (multiplies each item's Scale). The headset sizes look small on a flat screen.")]
    public float hudScale = 1.5f;
    [Tooltip("Computer: show the breathing timer as a bar under the task box (small dial, progress, 00:20 / 01:00) instead of the round timer.")]
    public bool timerBarUnderTask = true;
    [Tooltip("Height of the timer bar (pixels on a 1920x1080 screen).")]
    public float timerBarHeight = 56f;
    [Tooltip("Width of the timer bar as part of the screen width.")]
    [Range(0.2f, 0.9f)] public float timerBarWidth = 0.42f;
    [Tooltip("Space kept free at the screen edges (screen fraction).")]
    public float edgeMargin = 0.02f;

    public List<Item> items = new List<Item>
    {
        new Item { label = "提示框", nameContains = "Instruction_Canvas", screenPoint = new Vector2(0.5f, 0.97f), itemPivot = new Vector2(0.5f, 1f), scale = 1f, widen = 1.6f },
        new Item { label = "對話框", nameContains = "Dialogue_Canva", screenPoint = new Vector2(0.5f, 0.03f), itemPivot = new Vector2(0.5f, 0f), scale = 1f, widen = 1.5f },
        new Item { label = "病歷表按鈕", nameContains = "MedicalRecord_HUD_Canvas", screenPoint = new Vector2(0.02f, 0.70f), itemPivot = new Vector2(0f, 1f), scale = 1.1f },
        new Item { label = "情緒條", nameContains = "EmotionMeter_Canvas", screenPoint = new Vector2(0.98f, 0.70f), itemPivot = new Vector2(1f, 1f), scale = 0.85f },
        // Only made bigger on the computer (they stay where the story puts them).
        new Item { label = "考題", nameContains = "QuizCanvas", scale = 1.35f, moveToScreenPoint = false },
        new Item { label = "拉耳朵提示卡", nameContains = "Part6_EarPullCard", scale = 1.35f, moveToScreenPoint = false },
        new Item { label = "耳溫結果卡", nameContains = "EarThermometer_ReadingCard", scale = 1.35f, moveToScreenPoint = false },
        new Item { label = "血壓卡", nameContains = "Part9_BloodPressureCard", scale = 1.35f, moveToScreenPoint = false },
        new Item { label = "結算畫面", nameContains = "EndScreen_Canvas", scale = 1.35f, moveToScreenPoint = false },
        // The round breathing timer is hidden on the computer; a timer bar under the task box shows it instead.
        new Item { label = "計時器", nameContains = "Timer", screenPoint = new Vector2(0.02f, 0.55f), itemPivot = new Vector2(0f, 0.5f), scale = 0.8f, enabled = false },
    };

    static PcHudLayout instance;
    readonly Dictionary<Item, List<Transform>> found = new Dictionary<Item, List<Transform>>();
    float nextScan;
    bool fovApplied;
    readonly Vector3[] corners = new Vector3[4];
    // Size each piece had before we scaled it; if the piece still has the size we gave it last
    // frame, nothing else changed it, so start again from its own size (no growing every frame).
    readonly Dictionary<Transform, Vector3> baseScale = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Vector3> writtenScale = new Dictionary<Transform, Vector3>();

    public static bool IsComputerWithoutHeadset =>
        !Application.isMobilePlatform && !UnityEngine.XR.XRSettings.isDeviceActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Application.isMobilePlatform || FindObjectOfType<PcHudLayout>() != null)
            return;
        GameObject go = new GameObject("PcHudLayout");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PcHudLayout>();
    }

    void LateUpdate()
    {
        if (!IsComputerWithoutHeadset)
            return;
        Camera cam = Camera.main;
        if (cam == null)
            return;

        if (!fovApplied && fieldOfView > 1f)
        {
            cam.fieldOfView = fieldOfView;
            fovApplied = true;
            Debug.Log($"[PcHudLayout] Computer view: field of view {fieldOfView:0}.");
        }

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            Scan(cam.transform);
        }

        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * cam.aspect;
        Transform taskBox = null;
        foreach (Item item in items)
        {
            if (item == null || !item.enabled || !found.TryGetValue(item, out List<Transform> list))
                continue;
            foreach (Transform t in list)
                if (t != null && t.gameObject.activeInHierarchy)
                {
                    if (!item.moveToScreenPoint)
                    {
                        Resize(t, item.scale);   // no hudScale: these are already big
                        continue;
                    }
                    Resize(t, item.scale * hudScale);
                    Widen(t, item.widen);
                    Place(cam.transform, t, item, tanH, tanV);
                    if (taskBox == null && item.nameContains == "Instruction_Canvas")
                        taskBox = t;
                }
        }

        UpdateTimerBar(cam.transform, taskBox, tanH, tanV);
    }

    // ---------------- Timer bar under the task box (computer only) ----------------
    GameObject barRoot;
    RectTransform barRect;
    RectTransform barFill;
    RectTransform barHand;
    Text barText;
    Canvas barCanvas;

    void UpdateTimerBar(Transform cam, Transform taskBox, float tanH, float tanV)
    {
        MinimalRingTimer timer = null;
        bool timerShown = false;
        foreach (MinimalRingTimer t in FindTimers())
        {
            Transform ring = t.transform.Find("RingTimerCanvas");
            if (ring == null || !ring.gameObject.activeInHierarchy)
                continue;
            if (timerBarUnderTask)
            {
                Canvas c = ring.GetComponent<Canvas>();
                if (c != null)
                    c.enabled = false;   // hide the round one; the bar shows the time
            }
            timer = t;
            timerShown = true;
            break;
        }

        if (!timerBarUnderTask || !timerShown || timer == null)
        {
            if (barRoot != null)
                barRoot.SetActive(false);
            return;
        }

        if (barRoot == null)
            BuildTimerBar();
        barRoot.SetActive(true);

        // Under the task box (or near the top if no task box is up).
        float uMin = 0.25f, uMax = 0.75f, vTop = 0.80f;
        if (taskBox != null && TryGetVisibleViewRect(cam, taskBox, out Rect r))
        {
            uMin = (r.xMin / tanH + 1f) * 0.5f;
            uMax = (r.xMax / tanH + 1f) * 0.5f;
            vTop = (r.yMin / tanV + 1f) * 0.5f;
        }
        float scale = barCanvas != null && barCanvas.scaleFactor > 0f ? barCanvas.scaleFactor : 1f;
        float centre = (uMin + uMax) * 0.5f;
        float w = Mathf.Max(320f, timerBarWidth * Screen.width / scale);
        float x = centre * Screen.width / scale - w * 0.5f;
        float yFromTop = (1f - vTop) * Screen.height / scale + 8f;
        barRect.anchoredPosition = new Vector2(x, -yFromTop);
        barRect.sizeDelta = new Vector2(w, timerBarHeight);

        float duration = Mathf.Max(0.01f, timer.duration);
        float elapsed = Mathf.Clamp(duration - timer.Remaining, 0f, duration);
        barFill.anchorMax = new Vector2(elapsed / duration, 1f);
        barHand.localEulerAngles = new Vector3(0f, 0f, -6f * (elapsed % 60f));
        int e = Mathf.FloorToInt(elapsed);
        int d = Mathf.RoundToInt(duration);
        barText.text = $"{e / 60:00}:{e % 60:00} / {d / 60:00}:{d % 60:00}";
    }

    MinimalRingTimer[] timerCache = new MinimalRingTimer[0];
    float nextTimerScan;
    MinimalRingTimer[] FindTimers()
    {
        if (Time.unscaledTime >= nextTimerScan)
        {
            nextTimerScan = Time.unscaledTime + 1f;
            timerCache = FindObjectsOfType<MinimalRingTimer>();
        }
        return timerCache;
    }

    void BuildTimerBar()
    {
        barRoot = new GameObject("PcTimerBar", typeof(RectTransform));
        barRoot.transform.SetParent(transform, false);
        barCanvas = barRoot.AddComponent<Canvas>();
        barCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        barCanvas.sortingOrder = 50;
        CanvasScaler scaler = barRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite circle = MakeCircle(128);
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }

        // No dark strip: just a big dial, a thick bar and big time, with dark edges so they
        // read on any wall colour.
        Image bg = NewImg("Bar", barRoot.transform, null, new Color(0f, 0f, 0f, 0f));
        barRect = bg.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 1f);

        Color edge = new Color(0.1f, 0.12f, 0.16f, 0.85f);

        // Watch face with a turning second hand.
        Image ring = NewImg("DialEdge", barRect, circle, edge);
        RectTransform rr = ring.rectTransform;
        rr.anchorMin = rr.anchorMax = new Vector2(0f, 0.5f);
        rr.pivot = new Vector2(0f, 0.5f);
        rr.anchoredPosition = Vector2.zero;
        rr.sizeDelta = new Vector2(52f, 52f);
        Image face = NewImg("Dial", rr, circle, new Color32(0xF4, 0xF1, 0xEA, 0xFF));
        RectTransform fr = face.rectTransform;
        fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.5f);
        fr.sizeDelta = new Vector2(44f, 44f);
        GameObject handPivot = new GameObject("Hand", typeof(RectTransform));
        handPivot.transform.SetParent(fr, false);
        barHand = (RectTransform)handPivot.transform;
        barHand.anchorMin = barHand.anchorMax = new Vector2(0.5f, 0.5f);
        barHand.sizeDelta = Vector2.zero;
        Image hand = NewImg("Needle", barHand, null, new Color32(0xD9, 0x4A, 0x3D, 0xFF));
        hand.rectTransform.anchorMin = hand.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hand.rectTransform.sizeDelta = new Vector2(3.5f, 18f);
        hand.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        Image hub = NewImg("Hub", fr, circle, new Color32(0xD9, 0x4A, 0x3D, 0xFF));
        hub.rectTransform.anchorMin = hub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hub.rectTransform.sizeDelta = new Vector2(7f, 7f);

        // Thick bar with a dark edge; the green part grows over the minute.
        Image trackEdge = NewImg("TrackEdge", barRect, null, edge);
        RectTransform te = trackEdge.rectTransform;
        te.anchorMin = new Vector2(0f, 0.5f);
        te.anchorMax = new Vector2(1f, 0.5f);
        te.offsetMin = new Vector2(66f, -10f);
        te.offsetMax = new Vector2(-190f, 10f);
        Image track = NewImg("Track", te, null, new Color(1f, 1f, 1f, 0.85f));
        RectTransform tr = track.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(3f, 3f);
        tr.offsetMax = new Vector2(-3f, -3f);
        Image fill = NewImg("Fill", tr, null, new Color32(0x1D, 0x9E, 0x75, 0xFF));
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("Time", typeof(RectTransform));
        textGo.transform.SetParent(barRect, false);
        barText = textGo.AddComponent<Text>();
        barText.font = font;
        barText.fontSize = 34;
        barText.fontStyle = FontStyle.Bold;
        barText.color = Color.white;
        barText.alignment = TextAnchor.MiddleRight;
        barText.raycastTarget = false;
        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = edge;
        outline.effectDistance = new Vector2(2f, -2f);
        RectTransform txr = barText.rectTransform;
        txr.anchorMin = new Vector2(1f, 0f);
        txr.anchorMax = new Vector2(1f, 1f);
        txr.pivot = new Vector2(1f, 0.5f);
        txr.sizeDelta = new Vector2(180f, 0f);
        txr.anchoredPosition = Vector2.zero;
    }

    static Image NewImg(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Sprite MakeCircle(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float rad = res * 0.5f;
        Color32[] px = new Color32[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - rad) * (x + 0.5f - rad) + (y + 0.5f - rad) * (y + 0.5f - rad));
                px[y * res + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(rad - d) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), res);
    }

    // Floating pieces in front of the camera whose name matches (root objects of that piece only).
    void Scan(Transform cam)
    {
        foreach (Item item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.nameContains))
                continue;
            List<Transform> list = new List<Transform>();
            foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
            {
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                    continue;
                Transform t = NamedAncestor(canvas.transform, item.nameContains);
                if (t != null && !list.Contains(t) && (!item.moveToScreenPoint || IsHeadLocked(cam, t)))
                    list.Add(t);
            }
            found[item] = list;
        }
    }

    static Transform NamedAncestor(Transform t, string nameContains)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.Contains(nameContains))
                return p;
        return null;
    }

    // Attached to the camera, or kept in front of it (emotion bar, timer) - not something in the room.
    static bool IsHeadLocked(Transform cam, Transform t)
    {
        if (t.IsChildOf(cam))
            return true;
        Vector3 local = cam.InverseTransformPoint(t.position);
        return local.z > 0.3f && local.z < 2f && Mathf.Abs(local.x) < local.z && Mathf.Abs(local.y) < local.z;
    }

    // Wider box: the canvas and every wide piece inside it (panel, text) get wider, the scale
    // stays the same, so the letters keep their size and the text uses longer lines.
    readonly Dictionary<RectTransform, float> baseWidth = new Dictionary<RectTransform, float>();
    readonly Dictionary<RectTransform, float> writtenWidth = new Dictionary<RectTransform, float>();

    void Widen(Transform t, float factor)
    {
        if (Mathf.Abs(factor - 1f) < 0.001f || !(t is RectTransform root))
            return;
        float rootWidth = BaseWidthOf(root);
        if (rootWidth <= 1f)
            return;
        foreach (RectTransform rt in t.GetComponentsInChildren<RectTransform>(true))
        {
            bool stretched = Mathf.Abs(rt.anchorMax.x - rt.anchorMin.x) > 0.001f;
            if (stretched && rt != root)
                continue;   // follows its parent by itself
            float w = BaseWidthOf(rt);
            if (rt != root && w < rootWidth * 0.5f)
                continue;   // small things (icons, name tags) keep their width
            float target = w * factor;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, target);
            writtenWidth[rt] = rt.rect.width;
        }
    }

    float BaseWidthOf(RectTransform rt)
    {
        float now = rt.rect.width;
        if (!baseWidth.TryGetValue(rt, out float b) || !writtenWidth.TryGetValue(rt, out float wrote) || Mathf.Abs(now - wrote) > 0.01f)
        {
            b = now;   // first time, or another script changed it
            baseWidth[rt] = b;
        }
        return b;
    }

    void Resize(Transform t, float factor)
    {
        Vector3 now = t.localScale;
        if (!baseScale.TryGetValue(t, out Vector3 baseline)
            || !writtenScale.TryGetValue(t, out Vector3 written)
            || (now - written).sqrMagnitude > 1e-12f)
        {
            baseline = now;   // first time, or another script set a new size this frame
            baseScale[t] = baseline;
        }
        Vector3 target = baseline * Mathf.Max(0.1f, factor);
        t.localScale = target;
        writtenScale[t] = target;
    }

    // Move the piece sideways / up-down (at the same distance) so its visible box lands on the
    // chosen screen point. Absolute target, so doing it every frame does not add up.
    void Place(Transform cam, Transform t, Item item, float tanH, float tanV)
    {
        if (!TryGetVisibleViewRect(cam, t, out Rect r))
            return;

        float margin = Mathf.Clamp01(edgeMargin);
        float u = Mathf.Clamp(item.screenPoint.x, margin, 1f - margin);
        float v = Mathf.Clamp(item.screenPoint.y, margin, 1f - margin);
        float targetX = (u * 2f - 1f) * tanH;
        float targetY = (v * 2f - 1f) * tanV;
        float nowX = Mathf.Lerp(r.xMin, r.xMax, item.itemPivot.x);
        float nowY = Mathf.Lerp(r.yMin, r.yMax, item.itemPivot.y);

        float depth = cam.InverseTransformPoint(t.position).z;
        if (depth < 0.05f)
            return;
        Vector3 shift = cam.right * ((targetX - nowX) * depth) + cam.up * ((targetY - nowY) * depth);
        if (shift.sqrMagnitude > 1e-8f)
            t.position += shift;
    }

    // The box around everything that is drawn (images and text), in view units (x / z, y / z).
    bool TryGetVisibleViewRect(Transform cam, Transform root, out Rect rect)
    {
        rect = default;
        bool any = false;
        float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
        foreach (Graphic g in root.GetComponentsInChildren<Graphic>(false))
        {
            if (g == null || !g.enabled || g.color.a < 0.02f)
                continue;
            g.rectTransform.GetWorldCorners(corners);
            foreach (Vector3 c in corners)
            {
                Vector3 p = cam.InverseTransformPoint(c);
                if (p.z < 0.05f)
                    return false;
                xMin = Mathf.Min(xMin, p.x / p.z);
                xMax = Mathf.Max(xMax, p.x / p.z);
                yMin = Mathf.Min(yMin, p.y / p.z);
                yMax = Mathf.Max(yMax, p.y / p.z);
                any = true;
            }
        }
        if (!any)
            return false;
        rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return rect.width > 0.0005f && rect.height > 0.0005f;
    }
}
