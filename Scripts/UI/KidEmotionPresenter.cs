using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows Yaya's emotion:
//   1) Yaya herself: sitting pose follows the emotion (via YayaAnimationPlayer.SetEmotionLevel)
//   2) a round "sticker" badge on the right of the view (same style as the bear avatar on the
//      left): a little Yaya face whose expression changes (smile / worried / crying / wailing),
//      a ring around it that fills up as she gets more upset, a pink name tab and a state chip.
//      Shown once the nurse has walked into Yaya's room.
//   3) a short floating line when the value changes, e.g. "命令口氣 +15"
//
// The value (0-100) is the SUM of two things:
//   - what the story does to her (rubbing arm / crying / kicking push the value up), and
//   - what the nurse says (the backend tension change is added on top).
// Red (>= 75) is only reached when the nurse's tone adds to it. In red the scenario waits
// until the nurse calms Yaya back into orange (<= 60).
// The displayed state always moves one step at a time, up and down.
// Created automatically after the scene loads; add it to an object yourself to tune values.
public class KidEmotionPresenter : MonoBehaviour
{
    [Header("Emotion value (0-100): story poses and the backend add up")]
    [Tooltip("Value Yaya rises to (at least) when the story makes her rub her arm / hold her ear.")]
    [SerializeField] private float storyUneasyValue = 30f;
    [Tooltip("...when the story makes her cover her face and cry.")]
    [SerializeField] private float storyCryingValue = 55f;
    [Tooltip("...when the story makes her kick on the bed (orange, close to red but not red).")]
    [SerializeField] private float storyKickingValue = 68f;
    [Tooltip("Points removed/added by one soothing / harsh sentence when the backend value itself did not move.")]
    [SerializeField] private float intentStep = 8f;
    [Tooltip("When the story has her calm again, the value drifts back toward the backend value (points per second).")]
    [SerializeField] private float calmDriftPerSecond = 5f;

    [Header("Meltdown gate")]
    [Tooltip("At or above this value (red) the scenario waits until the nurse calms Yaya.")]
    [SerializeField] private float meltdownValue = 75f;
    [Tooltip("The scenario continues once the value is back at or below this (well inside orange).")]
    [SerializeField] private float releaseValue = 60f;
    [SerializeField] private bool blockProgressOnMeltdown = true;

    [Header("Stepping")]
    [Tooltip("Minimum seconds Yaya stays in a state before moving one more step.")]
    [SerializeField] private float secondsPerStep = 1.8f;

    [Header("Meter (in front of the eyes)")]
    [SerializeField] private bool showMeter = true;
    [Tooltip("The bar appears once the nurse is this close to Yaya (m, measured on the floor), i.e. inside the room. 0 = always.")]
    [SerializeField] private float showWhenWithinMetersOfYaya = 3.4f;
    [Tooltip("Right side, mirrored from the medical-record button on the left. Off = use Meter Offset as is.")]
    [SerializeField] private bool mirrorRecordButtonColumn = true;
    [Tooltip("x right, y up, z forward (m) from the head. y is the middle of the bar.")]
    [SerializeField] private Vector3 meterOffset = new Vector3(0.34f, -0.06f, 0.95f);
    [Tooltip("Height of the whole badge (name tab to state chip, m).")]
    [SerializeField] private float meterHeight = 0.21f;
    [Tooltip("Strip behind the floating reason line and the '請先安撫芽芽' line.")]
    [SerializeField] private Color cardColor = new Color(1f, 1f, 1f, 0.92f);
    [Tooltip("Pink name tab above the badge (same as the speaker tab on the dialogue box).")]
    [SerializeField] private Color nameTabColor = new Color(1f, 0.84f, 0.87f, 1f);
    [SerializeField] private Color nameTabTextColor = new Color(0.55f, 0.25f, 0.32f, 1f);
    [Tooltip("Badge pops (and shakes when she gets more upset) whenever the state changes.")]
    [SerializeField] private bool animateBadge = true;
    [SerializeField] private float reasonSeconds = 2.5f;
    [SerializeField] private bool hideWhileMedicalRecordOpen = true;
    [SerializeField] private bool hideWhileQuizOpen = true;

    [Header("Debug")]
    [SerializeField] private bool logChanges = true;

    private static readonly string[] StateNames = { "平靜", "不安", "哭泣", "崩潰" };
    private static readonly Color[] StateColors =
    {
        new Color(0.42f, 0.78f, 0.55f),
        new Color(0.97f, 0.76f, 0.28f),
        new Color(0.96f, 0.56f, 0.30f),
        new Color(0.91f, 0.34f, 0.36f)
    };

    private static readonly Dictionary<string, string> IntentLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "reassure", "溫柔安撫" }, { "comfort", "溫柔安撫" }, { "encourage", "鼓勵稱讚" }, { "praise", "鼓勵稱讚" },
        { "distract", "轉移注意" }, { "ask_consent", "徵求同意" }, { "explain", "說明解釋" },
        { "command", "命令口氣" }, { "threat", "威脅語氣" }, { "threaten", "威脅語氣" }
    };

    private static readonly HashSet<string> SoothingIntents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "reassure", "comfort", "encourage", "praise", "distract", "ask_consent" };
    private static readonly HashSet<string> HarshIntents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "command", "threat", "threaten" };

    private EmotionStateManager manager;
    private YayaAnimationPlayer yaya;
    private MedicalRecordPanelFollower medicalRecord;
    private Transform quizRoot;

    private float emotionValue;      // 0..100, what the meter shows
    private float backendValue;      // last backend tension as 0..100
    private bool hasBackendValue;
    private int lastStoryLevel;
    private int shownLevel;
    private float nextStepTime;
    private float displayValue;

    private GameObject meter;
    private RectTransform badge;
    private Image faceImage;
    private Image ringFill;
    private Image glow;
    private RectTransform reasonGroup;
    private float reasonShowTime;
    private int badgeLevel = -1;
    private float badgeChangeTime = -10f;
    private bool badgeChangedUp;
    private float settleTarget = -1f;
    private TMP_Text stateText;
    private Image stateChip;
    private TMP_Text reasonText;
    private TMP_Text gateText;
    private GameObject reasonBack;
    private GameObject gateBack;
    private float reasonHideTime;
    private bool enteredRoom;
    private bool spotSolved;
    private float spotSolveTime;
    private Vector3 solvedOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        KidEmotionGate.Blocking = false;
        holdSteady = false;
        if (FindObjectOfType<KidEmotionPresenter>(true) != null)
            return;
        new GameObject("KidEmotionPresenter (auto)").AddComponent<KidEmotionPresenter>();
    }

    private void OnEnable()
    {
        TryBind();
    }

    private void OnDisable()
    {
        if (manager != null)
            manager.OnEmotionChanged -= HandleEmotionChanged;
        manager = null;
        KidEmotionGate.Blocking = false;
    }

    private void TryBind()
    {
        if (manager != null)
            return;

        EmotionStateManager found = EmotionStateManager.Instance != null
            ? EmotionStateManager.Instance
            : FindObjectOfType<EmotionStateManager>(true);

        if (found == null)
        {
            // No manager in the scene: put one next to the AudioUploader so backend replies reach us.
            AudioUploader uploader = FindObjectOfType<AudioUploader>(true);
            if (uploader != null)
                found = uploader.gameObject.AddComponent<EmotionStateManager>();
        }

        if (found == null)
            return;

        manager = found;
        manager.OnEmotionChanged += HandleEmotionChanged;
        if (logChanges)
            Debug.Log("[KidEmotionPresenter] Listening to EmotionStateManager.", this);
    }

    private void HandleEmotionChanged(EmotionSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        // Backend tension is -5..5 (higher = more upset) -> 0..100.
        float value = Mathf.Clamp((snapshot.tension + 5f) * 10f, 0f, 100f);
        string intent = snapshot.llm != null ? snapshot.llm.intent : null;
        float coercion = snapshot.llm != null ? snapshot.llm.coercion : 0f;

        string reason = null;
        if (!string.IsNullOrEmpty(intent))
            IntentLabels.TryGetValue(intent, out reason);
        if (reason == null && coercion >= 0.5f)
            reason = "語氣強硬";

        ApplyBackend(value, intent, coercion, reason);
    }

    // The backend change is ADDED to whatever the story already did to Yaya.
    private void ApplyBackend(float value, string intent, float coercion, string reason)
    {
        float delta = hasBackendValue ? value - backendValue : value;
        backendValue = value;
        hasBackendValue = true;

        // The nurse is talking to someone else (e.g. explaining to Mom): Yaya stays as she is.
        if (holdSteady)
        {
            if (logChanges)
                Debug.Log($"[KidEmotionPresenter] Held steady: ignored backend change {delta:+0;-0;0} ({reason}).", this);
            return;
        }

        // The backend value is clamped (e.g. already fully calm), so a soothing sentence may not
        // move it. It must still calm a story-upset Yaya, and a harsh one must still upset her.
        if (Mathf.Abs(delta) < 0.5f)
        {
            if (!string.IsNullOrEmpty(intent) && SoothingIntents.Contains(intent))
                delta = -intentStep;
            else if ((!string.IsNullOrEmpty(intent) && HarshIntents.Contains(intent)) || coercion >= 0.5f)
                delta = intentStep;
        }

        AddToValue(delta, reason);
    }

    // Story events that calm Yaya a little (for example: she was given a sticker).
    // The bar on screen (null until it has been built).
    public Transform MeterRoot => meter != null ? meter.transform : null;

    // While held, what the nurse says does not change Yaya's emotion (use it when the nurse
    // talks to Mom, not to Yaya). Call Hold(false) when the step is over.
    private static bool holdSteady;
    public static bool IsHeld => holdSteady;

    public static void Hold(bool hold)
    {
        holdSteady = hold;
    }

    public static void Calm(float amount, string reason)
    {
        KidEmotionPresenter presenter = FindObjectOfType<KidEmotionPresenter>();
        if (presenter != null && amount > 0f)
            presenter.AddToValue(-amount, reason);
    }

    private void AddToValue(float delta, string reason)
    {
        float before = emotionValue;
        emotionValue = Mathf.Clamp(emotionValue + delta, 0f, 100f);
        float applied = emotionValue - before;
        if (Mathf.Abs(applied) >= 0.5f || !string.IsNullOrEmpty(reason))
            ShowReason(reason, applied);
        if (logChanges)
            Debug.Log($"[KidEmotionPresenter] value={emotionValue:0} ({applied:+0;-0;0}) reason={reason}", this);
    }

    private float StoryValue(int storyLevel)
    {
        return storyLevel >= 3 ? storyKickingValue : storyLevel == 2 ? storyCryingValue : storyLevel == 1 ? storyUneasyValue : 0f;
    }

    private int LevelFromValue(float value)
    {
        return value >= meltdownValue ? 3 : value >= 50f ? 2 : value >= 25f ? 1 : 0;
    }

    private void Update()
    {
        if (manager == null)
            TryBind();
        if (yaya == null)
            yaya = FindObjectOfType<YayaAnimationPlayer>(true);

        // Story poses push the value up when they start (rubbing arm < crying < kicking).
        int storyLevel = yaya != null ? yaya.StoryRequestedUpsetLevel : 0;
        if (storyLevel > lastStoryLevel)
        {
            float floor = StoryValue(storyLevel);
            if (emotionValue < floor)
                emotionValue = floor;
            settleTarget = -1f;
        }
        else if (storyLevel < lastStoryLevel && storyLevel > 0)
        {
            // The story eases her one step (e.g. crying -> rubbing her arm): settle down to that
            // level, but not lower; what the nurse said is still added on top.
            settleTarget = Mathf.Max(backendValue, StoryValue(storyLevel));
        }
        lastStoryLevel = storyLevel;

        if (settleTarget >= 0f && !KidEmotionGate.Blocking)
        {
            if (emotionValue > settleTarget)
                emotionValue = Mathf.MoveTowards(emotionValue, settleTarget, calmDriftPerSecond * Time.deltaTime);
            else
                settleTarget = -1f;
        }

        // Once the story has her calm again, drift back toward the backend value (not while gated).
        if (storyLevel == 0 && !KidEmotionGate.Blocking && emotionValue > backendValue)
            emotionValue = Mathf.MoveTowards(emotionValue, backendValue, calmDriftPerSecond * Time.deltaTime);

        // Meltdown gate: red -> wait until the nurse calms her back into orange.
        if (blockProgressOnMeltdown)
        {
            if (!KidEmotionGate.Blocking && emotionValue >= meltdownValue)
            {
                KidEmotionGate.Blocking = true;
                Debug.Log("[KidEmotionPresenter] Meltdown: scenario waits until Yaya is calmed.", this);
            }
            else if (KidEmotionGate.Blocking && emotionValue <= releaseValue)
            {
                KidEmotionGate.Blocking = false;
                Debug.Log("[KidEmotionPresenter] Yaya calmed down: scenario can continue.", this);
            }
        }
        else
        {
            KidEmotionGate.Blocking = false;
        }

        // One step at a time, in both directions.
        int targetLevel = LevelFromValue(emotionValue);
        if (shownLevel != targetLevel && Time.time >= nextStepTime)
        {
            shownLevel += targetLevel > shownLevel ? 1 : -1;
            nextStepTime = Time.time + Mathf.Max(0.2f, secondsPerStep);
            if (yaya != null)
                yaya.SetEmotionLevel(shownLevel);
            if (logChanges)
                Debug.Log($"[KidEmotionPresenter] Yaya -> {StateNames[shownLevel]} (target {StateNames[targetLevel]})", this);
        }
    }

    private void LateUpdate()
    {
        if (!showMeter)
        {
            if (meter != null && meter.activeSelf)
                meter.SetActive(false);
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
            return;

        // Only once the nurse has entered Yaya's room (not at the login / in the corridor).
        if (!enteredRoom)
        {
            if (showWhenWithinMetersOfYaya <= 0f)
            {
                enteredRoom = true;
            }
            else if (yaya != null)
            {
                Vector3 d = cam.transform.position - yaya.transform.position;
                d.y = 0f;
                enteredRoom = d.magnitude <= showWhenWithinMetersOfYaya;
            }

            if (!enteredRoom)
            {
                if (meter != null && meter.activeSelf)
                    meter.SetActive(false);
                return;
            }

            if (logChanges)
                Debug.Log("[KidEmotionPresenter] Nurse entered the room: emotion bar on.", this);
        }

        if (meter == null)
            BuildMeter();

        bool hidden = false;
        if (hideWhileMedicalRecordOpen)
        {
            if (medicalRecord == null)
                medicalRecord = FindObjectOfType<MedicalRecordPanelFollower>(true);
            hidden = medicalRecord != null && medicalRecord.isActiveAndEnabled;
        }
        if (!hidden && hideWhileQuizOpen)
            hidden = IsQuizOpen();
        if (meter.activeSelf == hidden)
            meter.SetActive(!hidden);
        if (hidden)
            return;

        Transform c = cam.transform;
        Transform t = meter.transform;
        Vector3 offset = HudDepth.Flatten(GetMeterOffset(c), out float depthFactor);
        t.position = c.position + c.right * offset.x + c.up * offset.y + c.forward * offset.z;
        // Parallel to the view (same as the other HUD panels) so it stays level, not skewed.
        t.rotation = c.rotation;
        t.localScale = Vector3.one * (meterHeight / BarHeight) * depthFactor;

        displayValue = Mathf.MoveTowards(displayValue, emotionValue, 60f * Time.deltaTime);
        UpdateBadge();

        bool gated = KidEmotionGate.Blocking;
        if (gateBack != null && gateBack.activeSelf != gated)
            gateBack.SetActive(gated);
        if (gated && gateBack != null)
        {
            float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 6f);
            gateBack.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        // Floating reason line: pops in under the chip, drifts down a little and fades out.
        bool reasonOn = reasonGroup != null && reasonGroup.gameObject.activeSelf;
        if (reasonOn)
        {
            float u = Mathf.Clamp01((Time.time - reasonShowTime) / Mathf.Max(0.3f, reasonSeconds));
            if (u >= 1f)
            {
                reasonGroup.gameObject.SetActive(false);
            }
            else
            {
                float pop = u < 0.12f ? Mathf.SmoothStep(0.6f, 1f, u / 0.12f) : 1f;
                reasonGroup.localScale = new Vector3(pop, pop, 1f);
                reasonGroup.anchoredPosition = new Vector2(0f, ReasonY - 10f * u);
                float a = 1f - Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f);
                reasonText.alpha = a;
                if (reasonBack != null)
                {
                    Image back = reasonBack.GetComponent<Image>();
                    Color strip = cardColor;
                    strip.a *= a;
                    back.color = strip;
                }
            }
        }
    }

    // Face, ring, chip and the little pop / shake / sob animations.
    private void UpdateBadge()
    {
        if (badge == null)
            return;

        int level = LevelFromValue(displayValue);
        if (level != badgeLevel)
        {
            badgeChangedUp = badgeLevel >= 0 && level > badgeLevel;
            if (badgeLevel >= 0)
                badgeChangeTime = Time.time;
            badgeLevel = level;
            faceImage.sprite = FaceSprite(level);
            stateText.text = StateNames[level];
            stateChip.color = StateColors[level];
        }

        ringFill.fillAmount = Mathf.Clamp01(displayValue / 100f);
        ringFill.color = StateColors[level];

        float t = Time.time;
        float scale = 1f;
        Vector2 shift = Vector2.zero;
        float tilt = 0f;
        if (animateBadge)
        {
            float since = t - badgeChangeTime;
            if (since < 0.4f)
            {
                float u = since / 0.4f;
                scale += 0.2f * Mathf.Sin(u * Mathf.PI) * (1f - u * 0.4f);
                if (badgeChangedUp)
                    shift.x += Mathf.Sin(since * 55f) * 5f * (1f - u);
            }

            switch (level)
            {
                case 0: scale *= 1f + 0.015f * Mathf.Sin(t * 2.2f); break;              // calm breathing
                case 1: tilt = Mathf.Sin(t * 3f) * 3f; break;                             // fidgeting
                case 2: shift.y += Mathf.Abs(Mathf.Sin(t * 5f)) * 3f; break;              // sobbing
                case 3: shift.x += Mathf.Sin(t * 38f) * 2.5f; tilt = Mathf.Sin(t * 19f) * 4f; break; // wailing
            }
        }

        badge.localScale = new Vector3(scale, scale, 1f);
        badge.anchoredPosition = new Vector2(0f, BadgeY) + shift;
        badge.localRotation = Quaternion.Euler(0f, 0f, tilt);

        if (glow != null)
        {
            bool on = level >= 3;
            if (glow.gameObject.activeSelf != on)
                glow.gameObject.SetActive(on);
            if (on)
            {
                Color g = StateColors[3];
                g.a = 0.30f + 0.25f * Mathf.Sin(t * 6f);
                glow.color = g;
            }
        }
    }

    // Right-hand column: mirror of the medical-record button, top level with it, and moved
    // down if a prompt / dialogue box would be covered.
    private Vector3 GetMeterOffset(Transform cam)
    {
        if (!mirrorRecordButtonColumn)
            return meterOffset;
        if (spotSolved && Time.unscaledTime - spotSolveTime < 3f)
            return solvedOffset;

        if (!HudFreeSpot.TryGetRecordButtonRect(cam, out Rect button))
            return spotSolved ? solvedOffset : meterOffset;

        float z = Mathf.Max(0.3f, meterOffset.z);
        float unit = meterHeight / BarHeight / z;          // one canvas unit in view units
        float halfHeight = CardHeight * 0.5f * unit;       // the card: bar + state word above it
        float halfWidth = CardWidth * 0.5f * unit;
        float centerShift = CardCenterY * unit;             // the word makes the block top-heavy
        float startY = button.yMax - halfHeight;            // top level with the record button
        float x = Mathf.Abs(button.center.x);
        float y = startY;

        // First try to stay at that height and step outward; only then move down.
        List<Rect> blockers = HudFreeSpot.CollectPanelRects(cam);
        bool placed = false;
        for (int i = 0; i < 4 && !placed; i++)
        {
            float tryX = x + 0.03f * i;
            if (HudFreeSpot.IsFree(blockers, tryX, halfWidth, halfHeight, startY, 0.01f))
            {
                x = tryX;
                placed = true;
            }
        }
        if (!placed && !HudFreeSpot.TrySlide(blockers, x, halfWidth, halfHeight, startY, true, 0.01f, 0.45f, out y))
            y = startY;

        solvedOffset = new Vector3(x * z, (y - centerShift) * z, z);
        if (!spotSolved && logChanges)
            Debug.Log($"[KidEmotionPresenter] Emotion bar spot: x={solvedOffset.x:0.000} y={solvedOffset.y:0.000} z={z:0.00}", this);
        spotSolved = true;
        spotSolveTime = Time.unscaledTime;
        return solvedOffset;
    }

    private bool IsQuizOpen()
    {
        if (quizRoot == null)
        {
            GameObject go = GameObject.Find("NewQuizCanvas");
            if (go == null)
                return false;
            quizRoot = go.transform;
        }

        foreach (Transform child in quizRoot)
        {
            if (child.gameObject.activeInHierarchy && child.name.StartsWith("Quiz_Panel"))
                return true;
        }
        return false;
    }

    private void ShowReason(string reason, float delta)
    {
        if (reasonText == null)
        {
            if (!showMeter || Camera.main == null)
                return;
            BuildMeter();
        }

        string change = Mathf.Abs(delta) >= 0.5f ? (delta > 0f ? " +" : " -") + Mathf.Abs(delta).ToString("0") : string.Empty;
        string label = string.IsNullOrEmpty(reason) ? "緊張值" : reason;
        reasonText.text = label + change;
        reasonText.color = delta > 0.5f ? new Color(0.85f, 0.25f, 0.28f) : delta < -0.5f ? new Color(0.20f, 0.62f, 0.38f) : new Color(0.35f, 0.35f, 0.40f);
        reasonGroup.gameObject.SetActive(true);
        reasonShowTime = Time.time;
        reasonHideTime = Time.time + reasonSeconds;
    }

    // Layout in canvas units. BarHeight is only the scale reference: Meter Height (m) = 200 units.
    private const float BarHeight = 200f;
    // Block used to keep the badge clear of other panels (centre relative to the canvas centre).
    private const float CardWidth = 140f;
    private const float CardHeight = 206f;
    private const float CardCenterY = 0f;
    private const float BadgeY = 6f;
    private const float BadgeSize = 124f;
    private const float TabY = 84f;
    private const float ChipY = -78f;
    private const float ReasonY = -116f;
    private const float GateY = -154f;

    // One white circle texture; drawn 9-sliced it gives rounded rectangles and pills of any size.
    private static Sprite roundSprite;
    private const float RoundRadius = 32f;
    private static Sprite RoundSprite()
    {
        if (roundSprite != null)
            return roundSprite;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = (byte)(Mathf.Clamp01(c + 0.5f - d) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        roundSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
        return roundSprite;
    }

    // Plain (not sliced) disc and ring sprites, used as simple / radial-filled images.
    private static Sprite discSprite;
    private static Sprite ringSprite;
    private static Sprite DiscSprite()
    {
        if (discSprite == null)
            discSprite = MakeAnnulus(128, 0f);
        return discSprite;
    }

    private static Sprite RingSprite()
    {
        if (ringSprite == null)
            ringSprite = MakeAnnulus(128, 0.80f);
        return ringSprite;
    }

    private static Sprite MakeAnnulus(int size, float inner)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        float r = c;
        float ri = r * inner;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r + 0.5f - d);
                if (inner > 0f)
                    a *= Mathf.Clamp01(d - ri + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Rounded rectangle with the given corner radius (canvas units).
    private static Image NewRound(RectTransform parent, string name, Color color, Vector2 position, Vector2 size, float radius)
    {
        Image image = NewImage(parent, name, color, position, size);
        image.sprite = RoundSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = RoundRadius / Mathf.Max(1f, radius);
        return image;
    }

    private static Image NewDisc(RectTransform parent, string name, Sprite sprite, Color color, Vector2 position, float diameter)
    {
        Image image = NewImage(parent, name, color, position, new Vector2(diameter, diameter));
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        return image;
    }

    //   [ 芽芽 ]          pink name tab (like the speaker tab of the dialogue box)
    //   ( face )          white-bordered sticker with Yaya's face, ring = how upset she is
    //   [ 平靜 ]          chip in the state colour
    //   命令口氣 +15       floating reason line
    //   請先安撫芽芽        only while the scenario waits for her to calm down
    private void BuildMeter()
    {
        if (meter != null)
            return;

        meter = new GameObject("EmotionMeter_Canvas", typeof(RectTransform));
        Canvas canvas = meter.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        meter.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        RectTransform root = (RectTransform)meter.transform;
        root.sizeDelta = new Vector2(CardWidth + 60f, CardHeight + 140f);

        TMP_FontAsset font = FindChineseFont();

        // Badge (everything inside moves / pops together).
        badge = (RectTransform)new GameObject("Badge", typeof(RectTransform)).transform;
        badge.SetParent(root, false);
        badge.anchoredPosition = new Vector2(0f, BadgeY);
        badge.sizeDelta = new Vector2(BadgeSize, BadgeSize);

        glow = NewDisc(badge, "Glow", DiscSprite(), new Color(StateColors[3].r, StateColors[3].g, StateColors[3].b, 0.4f), Vector2.zero, BadgeSize + 22f);
        glow.gameObject.SetActive(false);
        NewDisc(badge, "Shadow", DiscSprite(), new Color(0f, 0f, 0f, 0.22f), new Vector2(0f, -4f), BadgeSize + 2f);
        NewDisc(badge, "Border", DiscSprite(), Color.white, Vector2.zero, BadgeSize);
        NewDisc(badge, "RingTrack", RingSprite(), new Color(0.90f, 0.90f, 0.92f, 1f), Vector2.zero, BadgeSize - 10f);
        ringFill = NewDisc(badge, "RingFill", RingSprite(), StateColors[0], Vector2.zero, BadgeSize - 10f);
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Bottom;
        ringFill.fillClockwise = true;
        ringFill.fillAmount = 0f;
        NewDisc(badge, "Inner", DiscSprite(), new Color(1f, 0.97f, 0.93f, 1f), Vector2.zero, BadgeSize - 30f);
        faceImage = NewDisc(badge, "Face", FaceSprite(0), Color.white, Vector2.zero, BadgeSize - 34f);

        // Name tab.
        NewRound(root, "NameTabShadow", new Color(0f, 0f, 0f, 0.15f), new Vector2(0f, TabY - 2f), new Vector2(78f, 30f), 15f);
        NewRound(root, "NameTab", nameTabColor, new Vector2(0f, TabY), new Vector2(76f, 28f), 14f);
        TMP_Text nameText = NewText(root, "Name", font, "芽芽", 18f, nameTabTextColor, new Vector2(0f, TabY), new Vector2(76f, 28f));
        nameText.outlineWidth = 0f;

        // State chip.
        NewRound(root, "StateChipShadow", new Color(0f, 0f, 0f, 0.18f), new Vector2(0f, ChipY - 3f), new Vector2(92f, 34f), 17f);
        stateChip = NewRound(root, "StateChip", StateColors[0], new Vector2(0f, ChipY), new Vector2(90f, 32f), 16f);
        stateText = NewText(root, "State", font, StateNames[0], 21f, Color.white, new Vector2(0f, ChipY), new Vector2(90f, 32f));
        stateText.outlineWidth = 0.12f;
        stateText.outlineColor = new Color32(0, 0, 0, 70);

        // Floating reason line.
        reasonGroup = (RectTransform)new GameObject("ReasonGroup", typeof(RectTransform)).transform;
        reasonGroup.SetParent(root, false);
        reasonGroup.anchoredPosition = new Vector2(0f, ReasonY);
        reasonGroup.sizeDelta = new Vector2(180f, 32f);
        reasonBack = NewRound(reasonGroup, "ReasonBack", cardColor, Vector2.zero, new Vector2(170f, 32f), 16f).gameObject;
        reasonText = NewText(reasonGroup, "Reason", font, string.Empty, 19f, Color.white, Vector2.zero, new Vector2(220f, 30f));
        reasonText.outlineWidth = 0f;
        reasonGroup.gameObject.SetActive(false);

        // "Please calm Yaya first".
        gateBack = NewRound(root, "GateBack", StateColors[3], new Vector2(0f, GateY), new Vector2(170f, 34f), 17f).gameObject;
        gateText = NewText((RectTransform)gateBack.transform, "Gate", font, "請先安撫芽芽", 19f, Color.white, Vector2.zero, new Vector2(200f, 30f));
        gateText.outlineWidth = 0f;
        gateBack.SetActive(false);

        badgeLevel = -1;
    }

    // ---- Yaya's face, painted once per state into a small texture ----
    private static readonly Sprite[] faceSprites = new Sprite[4];

    private static Sprite FaceSprite(int level)
    {
        level = Mathf.Clamp(level, 0, 3);
        if (faceSprites[level] == null)
            faceSprites[level] = PaintFace(level, 192);
        return faceSprites[level];
    }

    private static readonly Color Skin = new Color(1f, 0.87f, 0.76f);
    private static readonly Color Hair = new Color(0.45f, 0.29f, 0.19f);
    private static readonly Color Ink = new Color(0.30f, 0.20f, 0.18f);
    private static readonly Color Tear = new Color(0.45f, 0.75f, 0.98f);
    private static readonly Color MouthIn = new Color(0.62f, 0.20f, 0.24f);
    private static readonly Color Tongue = new Color(0.98f, 0.55f, 0.58f);

    // Face space: x, y in [-1.15, 1.15], face circle radius 1, y up.
    private static Sprite PaintFace(int level, int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        const float extent = 1.15f;
        float pixel = 2f * extent / size;

        for (int iy = 0; iy < size; iy++)
        {
            for (int ix = 0; ix < size; ix++)
            {
                float x = -extent + (ix + 0.5f) * pixel;
                float y = -extent + (iy + 0.5f) * pixel;
                Color c = new Color(0f, 0f, 0f, 0f);

                // Pigtails behind the head, then the face.
                c = Over(c, Hair, Cover(Circle(x, y, -0.92f, 0.42f, 0.30f), pixel));
                c = Over(c, Hair, Cover(Circle(x, y, 0.92f, 0.42f, 0.30f), pixel));
                c = Over(c, Skin, Cover(Circle(x, y, 0f, -0.04f, 0.96f), pixel));

                // Hair cap with a soft fringe.
                float fringe = 0.30f + 0.07f * Mathf.Abs(Mathf.Sin(x * 5.5f)) - 0.12f * x * x;
                float cap = Mathf.Max(Circle(x, y, 0f, 0.02f, 1.0f), fringe - y);
                c = Over(c, Hair, Cover(cap, pixel));

                // Cheeks (redder when upset).
                float blush = level >= 2 ? 0.75f : 0.5f;
                Color cheek = new Color(0.98f, 0.55f, 0.55f, blush);
                float cheekR = level >= 3 ? 0.20f : 0.15f;
                c = Over(c, cheek, Cover(Ellipse(x, y, -0.55f, -0.24f, cheekR * 1.3f, cheekR), pixel));
                c = Over(c, cheek, Cover(Ellipse(x, y, 0.55f, -0.24f, cheekR * 1.3f, cheekR), pixel));

                c = PaintExpression(c, level, x, y, pixel);
                px[iy * size + ix] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Color PaintExpression(Color c, int level, float x, float y, float pixel)
    {
        const float ex = 0.34f;   // eye x
        const float ey = 0.0f;    // eye y
        const float stroke = 0.045f;

        switch (level)
        {
            case 0: // calm: round eyes with a shine, gentle smile
                for (int s = -1; s <= 1; s += 2)
                {
                    c = Over(c, Ink, Cover(Ellipse(x, y, s * ex, ey, 0.085f, 0.11f), pixel));
                    c = Over(c, Color.white, Cover(Circle(x, y, s * ex + 0.03f, ey + 0.04f, 0.03f), pixel));
                }
                c = Over(c, Ink, Cover(ArcLower(x, y, 0f, -0.26f, 0.20f, stroke), pixel));
                break;

            case 1: // uneasy: small eyes, worried brows, wobbly mouth, a sweat drop
                for (int s = -1; s <= 1; s += 2)
                {
                    c = Over(c, Ink, Cover(Ellipse(x, y, s * ex, ey - 0.02f, 0.07f, 0.09f), pixel));
                    c = Over(c, Color.white, Cover(Circle(x, y, s * ex + 0.025f, ey + 0.01f, 0.022f), pixel));
                    c = Over(c, Ink, Cover(Segment(x, y, s * 0.18f, 0.25f, s * 0.46f, 0.16f, stroke * 0.8f), pixel));
                }
                c = Over(c, Ink, Cover(Wave(x, y, -0.20f, 0.20f, -0.40f, 0.035f, 17f, stroke * 0.9f), pixel));
                c = Over(c, Tear, Cover(Drop(x, y, 0.74f, 0.30f, 0.10f), pixel));
                break;

            case 2: // crying: closed eyes, tear streams, frown
                for (int s = -1; s <= 1; s += 2)
                {
                    c = Over(c, Tear, Cover(Segment(x, y, s * ex, ey - 0.06f, s * (ex + 0.02f), -0.55f, 0.06f), pixel));
                    c = Over(c, Ink, Cover(ArcUpper(x, y, s * ex, ey - 0.06f, 0.11f, stroke), pixel));
                    c = Over(c, Ink, Cover(Segment(x, y, s * 0.18f, 0.27f, s * 0.46f, 0.18f, stroke * 0.8f), pixel));
                }
                c = Over(c, MouthIn, Cover(Ellipse(x, y, 0f, -0.44f, 0.15f, 0.09f), pixel));
                c = Over(c, Ink, Cover(ArcUpper(x, y, 0f, -0.50f, 0.17f, stroke), pixel));
                break;

            default: // meltdown: squeezed > < eyes, wailing mouth, tears flying
                for (int s = -1; s <= 1; s += 2)
                {
                    float cx = s * ex;
                    c = Over(c, Ink, Cover(Segment(x, y, cx - s * 0.10f, ey + 0.08f, cx + s * 0.04f, ey, stroke), pixel));
                    c = Over(c, Ink, Cover(Segment(x, y, cx + s * 0.04f, ey, cx - s * 0.10f, ey - 0.08f, stroke), pixel));
                    c = Over(c, Tear, Cover(Drop(x, y, s * 0.80f, 0.02f, 0.09f), pixel));
                    c = Over(c, Tear, Cover(Drop(x, y, s * 0.92f, -0.28f, 0.07f), pixel));
                }
                c = Over(c, MouthIn, Cover(Ellipse(x, y, 0f, -0.42f, 0.27f, 0.21f), pixel));
                c = Over(c, Tongue, Cover(Mathf.Max(Ellipse(x, y, 0f, -0.55f, 0.16f, 0.09f), Ellipse(x, y, 0f, -0.42f, 0.27f, 0.21f)), pixel));
                break;
        }
        return c;
    }

    // ---- tiny signed-distance helpers (negative inside) ----
    private static float Cover(float d, float pixel) { return Mathf.Clamp01(0.5f - d / pixel); }

    private static Color Over(Color under, Color top, float coverage)
    {
        float a = top.a * coverage;
        if (a <= 0f)
            return under;
        float outA = a + under.a * (1f - a);
        Color rgb = (top * a + under * under.a * (1f - a)) / Mathf.Max(0.0001f, outA);
        rgb.a = outA;
        return rgb;
    }

    private static float Circle(float x, float y, float cx, float cy, float r)
    {
        return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
    }

    private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
    }

    private static float Segment(float x, float y, float ax, float ay, float bx, float by, float halfWidth)
    {
        float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
        float h = Mathf.Clamp01((px * vx + py * vy) / Mathf.Max(1e-6f, vx * vx + vy * vy));
        float dx = px - vx * h, dy = py - vy * h;
        return Mathf.Sqrt(dx * dx + dy * dy) - halfWidth;
    }

    // Lower half of a circle outline (a smile), with round ends.
    private static float ArcLower(float x, float y, float cx, float cy, float r, float halfWidth)
    {
        if (y <= cy)
            return Mathf.Abs(Circle(x, y, cx, cy, r)) - halfWidth;
        float d1 = Mathf.Sqrt((x - cx + r) * (x - cx + r) + (y - cy) * (y - cy));
        float d2 = Mathf.Sqrt((x - cx - r) * (x - cx - r) + (y - cy) * (y - cy));
        return Mathf.Min(d1, d2) - halfWidth;
    }

    // Upper half of a circle outline (a closed eye or a frown), with round ends.
    private static float ArcUpper(float x, float y, float cx, float cy, float r, float halfWidth)
    {
        return ArcLower(x, 2f * cy - y, cx, cy, r, halfWidth);
    }

    private static float Wave(float x, float y, float x0, float x1, float cy, float amp, float freq, float halfWidth)
    {
        float cx = Mathf.Clamp(x, x0, x1);
        float wy = cy + amp * Mathf.Sin((cx - x0) * freq);
        float dx = x - cx, dy = y - wy;
        return Mathf.Sqrt(dx * dx + dy * dy) - halfWidth;
    }

    // Teardrop pointing up.
    private static float Drop(float x, float y, float cx, float cy, float r)
    {
        float round = Circle(x, y, cx, cy, r);
        float tip = Segment(x, y, cx, cy + r * 1.9f, cx, cy, 0f) - r * Mathf.Clamp01((cy + r * 1.9f - y) / (r * 1.9f));
        return Mathf.Min(round, tip);
    }

    private static TMP_FontAsset FindChineseFont()
    {
        // Use the font of an existing prompt text so Chinese renders the same as the other panels.
        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            if (canvas == null || !canvas.name.Contains("Instruction_Canvas"))
                continue;
            TMP_Text text = canvas.GetComponentInChildren<TMP_Text>(true);
            if (text != null && text.font != null)
                return text.font;
        }
        TMP_Text any = FindObjectOfType<TMP_Text>(true);
        return any != null ? any.font : null;
    }

    private static Image NewImage(RectTransform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text NewText(RectTransform parent, string name, TMP_FontAsset font, string text, float size, Color color, Vector2 position, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = box;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.fontStyle = FontStyles.Bold;
        // No panel behind the text any more: a dark outline keeps it readable.
        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.raycastTarget = false;
        return tmp;
    }

    // ---- Test without the backend: right-click the component header in the Inspector ----
    [ContextMenu("Test/Soothing sentence (溫柔安撫)")]
    private void TestSoothe() { ApplyBackend(backendValue, "reassure", 0f, "溫柔安撫"); }

    [ContextMenu("Test/Harsh sentence (命令口氣 +15)")]
    private void TestHarsh() { ApplyBackend(Mathf.Min(100f, backendValue + 15f), "command", 0.8f, "命令口氣"); }

    [ContextMenu("Test/Jump to meltdown (80)")]
    private void TestMeltdown() { AddToValue(80f - emotionValue, "威脅語氣"); }

    [ContextMenu("Test/Reset to calm")]
    private void TestReset() { backendValue = 0f; AddToValue(-emotionValue, null); }
}

// Shared flag read by the story flows: while true, the nurse's keywords do not advance the story.
public static class KidEmotionGate
{
    public static bool Blocking;
}
