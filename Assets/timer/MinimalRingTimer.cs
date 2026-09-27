using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MinimalRingTimer : MonoBehaviour
{
    public enum Corner { TopRight, TopLeft, BottomRight, BottomLeft, Center }
    public enum DisplayMode { WorldSpace, ScreenCorner }

    [Header("Display")]
    public DisplayMode displayMode = DisplayMode.WorldSpace;
    public float worldDiameter = 0.14f;
    public Transform followTarget;
    public Vector3 worldOffset = Vector3.zero;
    public bool faceCamera = true;

    [Header("Camera Corner Placement")]
    public bool useCameraCorner = true;
    public Vector3 cameraCornerOffset = new Vector3(0.24f, 0.30f, 0.85f);

    [Header("Timer")]
    public float duration = 60f;
    public bool startOnAwake = false;
    public float warningTime = 10f;

    [Header("Screen Layout")]
    public Corner corner = Corner.TopRight;
    public float size = 180f;
    public Vector2 margin = new Vector2(40f, 40f);

    [Header("Text")]
    public string labelText = "";
    public Font customFont;

    [Header("Colors")]
    public Color backgroundColor = new Color32(0x1F, 0x23, 0x29, 0xFF);
    public Color trackColor = new Color32(0x3A, 0x3F, 0x47, 0xFF);
    public Color ringColor = new Color32(0x1D, 0x9E, 0x75, 0xFF);
    public Color digitColor = Color.white;
    public Color labelColor = new Color32(0xB4, 0xB2, 0xA9, 0xFF);
    public Color warningRingColor = new Color32(0xE2, 0x4B, 0x4A, 0xFF);
    public Color warningDigitColor = new Color32(0xF0, 0x95, 0x95, 0xFF);

    [Header("Sound")]
    public bool playFinishSound = true;
    public AudioClip customFinishSound;
    public bool playWarningTicks = false;
    public int tickFromSeconds = 5;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("Events")]
    public UnityEvent onFinished;

    public bool IsRunning => running;
    public float Remaining => remaining;

    private float remaining;
    private bool running;
    private bool finished;
    private float finishedElapsed;
    private int lastWholeSecond;

    private GameObject canvasGO;
    private Image ring;
    private Image capStart;
    private Image capEnd;
    private RectTransform capEndPivot;
    private Text digits;
    private AudioSource audioSource;
    private AudioClip finishClip;
    private AudioClip tickClip;

    private void Awake()
    {
        SetupAudio();
        BuildUI();
        remaining = duration;
        Refresh();
        SetVisible(false);
    }

    private void Start()
    {
        if (startOnAwake)
            StartTimer();
    }

    private void Update()
    {
        if (finished)
        {
            finishedElapsed += Time.deltaTime;
            float wait = playFinishSound && finishClip != null ? finishClip.length : 0.5f;
            if (finishedElapsed >= wait)
                SetVisible(false);
            return;
        }

        if (!running)
            return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            running = false;
            finished = true;
            finishedElapsed = 0f;
            Refresh();
            PlayFinishSound();
            onFinished?.Invoke();
            return;
        }

        int whole = Mathf.CeilToInt(remaining);
        if (playWarningTicks && whole != lastWholeSecond && whole <= tickFromSeconds && whole > 0)
        {
            PlayTickSound();
            lastWholeSecond = whole;
        }

        Refresh();
    }

    private void LateUpdate()
    {
        UpdateWorldPlacement();
    }

    private void OnDestroy()
    {
        if (canvasGO != null)
            Destroy(canvasGO);
    }

    public void StartTimer()
    {
        if (duration <= 0f)
            duration = 60f;

        remaining = duration;
        running = true;
        finished = false;
        finishedElapsed = 0f;
        lastWholeSecond = Mathf.CeilToInt(remaining);
        SetVisible(true);
        Refresh();
    }

    public void PauseTimer()
    {
        running = false;
    }

    public void ResumeTimer()
    {
        if (remaining > 0f)
        {
            running = true;
            finished = false;
            SetVisible(true);
        }
    }

    public void ResetTimer()
    {
        remaining = Mathf.Max(0.01f, duration);
        running = false;
        finished = false;
        finishedElapsed = 0f;
        lastWholeSecond = Mathf.CeilToInt(remaining);
        Refresh();
    }

    public void SetVisible(bool visible)
    {
        if (canvasGO != null)
            canvasGO.SetActive(visible);
    }

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;

        finishClip = customFinishSound != null
            ? customFinishSound
            : MakeBeepClip("FinishBeep", 880f, 0.09f, 0.07f, 3, 2, 0.3f);
        tickClip = MakeBeepClip("Tick", 1320f, 0.05f, 0f, 1, 1, 0f);
    }

    private void BuildUI()
    {
        canvasGO = new GameObject("RingTimerCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();

        GameObject rootGO = new GameObject("RingTimer", typeof(RectTransform));
        rootGO.transform.SetParent(canvasGO.transform, false);
        RectTransform root = (RectTransform)rootGO.transform;
        root.sizeDelta = new Vector2(size, size);

        if (displayMode == DisplayMode.WorldSpace)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            scaler.dynamicPixelsPerUnit = 3f;
            RectTransform crt = (RectTransform)canvasGO.transform;
            crt.sizeDelta = new Vector2(size, size);
            crt.localScale = Vector3.one * (worldDiameter / Mathf.Max(1f, size));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            UpdateWorldPlacement();
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Vector2 a = GetAnchor(corner);
            root.anchorMin = root.anchorMax = root.pivot = a;
            root.anchoredPosition = new Vector2(
                a.x == 1f ? -margin.x : (a.x == 0f ? margin.x : 0f),
                a.y == 1f ? -margin.y : (a.y == 0f ? margin.y : 0f));
        }

        float radius = size * 0.5f;
        float ringMid = radius * 0.81f;
        float thick = radius * 0.167f;
        float ringOuter = ringMid + thick * 0.5f;
        float ringInner = ringMid - thick * 0.5f;

        Sprite circle = MakeCircleSprite(256, 0f);
        Sprite ringSprite = MakeCircleSprite(256, ringInner / ringOuter);

        NewImage("Background", root, circle, backgroundColor, new Vector2(size, size), Vector2.zero);
        NewImage("Track", root, ringSprite, trackColor, new Vector2(ringOuter * 2f, ringOuter * 2f), Vector2.zero);

        ring = NewImage("Ring", root, ringSprite, ringColor, new Vector2(ringOuter * 2f, ringOuter * 2f), Vector2.zero);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = false;

        capStart = NewImage("CapStart", root, circle, ringColor, new Vector2(thick, thick), new Vector2(0f, ringMid));

        GameObject pivotGO = new GameObject("CapEndPivot", typeof(RectTransform));
        pivotGO.transform.SetParent(root, false);
        capEndPivot = (RectTransform)pivotGO.transform;
        capEndPivot.anchorMin = capEndPivot.anchorMax = new Vector2(0.5f, 0.5f);
        capEndPivot.sizeDelta = Vector2.zero;
        capEnd = NewImage("CapEnd", capEndPivot, circle, ringColor, new Vector2(thick, thick), new Vector2(0f, ringMid));

        Font font = customFont != null ? customFont : LoadBuiltinFont();
        digits = NewText("Digits", root, font, Mathf.RoundToInt(size * 0.22f), FontStyle.Bold, digitColor, new Vector2(0f, size * 0.02f));

        if (!string.IsNullOrWhiteSpace(labelText))
        {
            Text label = NewText("Label", root, font, Mathf.RoundToInt(size * 0.075f), FontStyle.Normal, labelColor, new Vector2(0f, -size * 0.18f));
            label.text = labelText;
        }
    }

    private void UpdateWorldPlacement()
    {
        if (canvasGO == null || displayMode != DisplayMode.WorldSpace)
            return;

        Transform t = canvasGO.transform;
        Camera cam = Camera.main;

        if (useCameraCorner && cam != null)
        {
            Transform ct = cam.transform;
            t.position = ct.position + ct.right * cameraCornerOffset.x + ct.up * cameraCornerOffset.y + ct.forward * cameraCornerOffset.z + worldOffset;
            t.rotation = Quaternion.LookRotation(t.position - ct.position, ct.up);
            t.localScale = Vector3.one * (worldDiameter / Mathf.Max(1f, size));
            return;
        }

        Transform target = followTarget != null ? followTarget : transform;
        t.position = target.position + worldOffset;
        t.localScale = Vector3.one * (worldDiameter / Mathf.Max(1f, size));

        if (faceCamera && cam != null)
        {
            Vector3 dir = t.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
        else
        {
            t.rotation = transform.rotation;
        }
    }

    private void Refresh()
    {
        float normalized = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        bool warning = remaining <= warningTime;

        if (ring != null)
        {
            ring.fillAmount = normalized;
            ring.color = warning ? warningRingColor : ringColor;
        }

        if (capStart != null)
            capStart.color = warning ? warningRingColor : ringColor;

        if (capEnd != null)
        {
            capEnd.color = warning ? warningRingColor : ringColor;
            capEnd.enabled = normalized > 0.001f;
        }

        if (capEndPivot != null)
            capEndPivot.localEulerAngles = new Vector3(0f, 0f, -360f * normalized);

        if (digits != null)
        {
            int seconds = Mathf.CeilToInt(remaining);
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = seconds % 60;
            digits.text = $"{minutes:00}:{secs:00}";
            digits.color = warning ? warningDigitColor : digitColor;
        }
    }

    private void PlayFinishSound()
    {
        if (playFinishSound && audioSource != null && finishClip != null)
            audioSource.PlayOneShot(finishClip, volume);
    }

    private void PlayTickSound()
    {
        if (audioSource != null && tickClip != null)
            audioSource.PlayOneShot(tickClip, volume * 0.45f);
    }

    private Image NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 sizeDelta, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = position;

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Text NewText(string name, Transform parent, Font font, int fontSize, FontStyle style, Color color, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, fontSize * 1.5f);
        rt.anchoredPosition = position;

        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Vector2 GetAnchor(Corner c)
    {
        switch (c)
        {
            case Corner.TopLeft: return new Vector2(0f, 1f);
            case Corner.BottomRight: return new Vector2(1f, 0f);
            case Corner.BottomLeft: return new Vector2(0f, 0f);
            case Corner.Center: return new Vector2(0.5f, 0.5f);
            default: return new Vector2(1f, 1f);
        }
    }

    private static Font LoadBuiltinFont()
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 32);
        return font;
    }

    private static Sprite MakeCircleSprite(int res, float innerFraction)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float radius = res * 0.5f;
        float inner = radius * innerFraction;
        Color32[] pixels = new Color32[res * res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - d);
                if (innerFraction > 0f)
                    alpha *= Mathf.Clamp01(d - inner);
                pixels[y * res + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), res);
    }

    private static AudioClip MakeBeepClip(string name, float freq, float beepLen, float gapLen, int beepsPerGroup, int groups, float groupGap)
    {
        const int rate = 44100;
        float groupLen = beepsPerGroup * beepLen + Mathf.Max(0, beepsPerGroup - 1) * gapLen;
        float total = groups * groupLen + Mathf.Max(0, groups - 1) * groupGap + 0.05f;
        int samples = Mathf.CeilToInt(total * rate);
        float[] data = new float[samples];
        float fade = 0.005f;

        for (int g = 0; g < groups; g++)
        {
            for (int b = 0; b < beepsPerGroup; b++)
            {
                float start = g * (groupLen + groupGap) + b * (beepLen + gapLen);
                int s0 = Mathf.FloorToInt(start * rate);
                int len = Mathf.FloorToInt(beepLen * rate);

                for (int i = 0; i < len && s0 + i < samples; i++)
                {
                    float t = (float)i / rate;
                    float env = Mathf.Min(1f, t / fade, (beepLen - t) / fade);
                    data[s0 + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f * env;
                }
            }
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}