using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Part 6: ear temperature measurement on Yaya, result, introduce the blood-pressure cuff, Quiz 6.
// Started automatically by Part5 after Quiz 5 is answered (see PediatricVitalSignsPart5Flow).
// All scene references are found by name at runtime; you can also assign them in the Inspector.
public class PediatricVitalSignsPart6Flow : MonoBehaviour
{
    private enum WaitingForNurseAction
    {
        None,
        PullEarAndExplain,
        ReportTemperatureAndIntroduceBp
    }

    [Header("Panels")]
    [SerializeField] private GameObject nursePromptPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;
    [Tooltip("First name that exists is used.")]
    [SerializeField] private string promptPanelNames = "P6Instruction_Canvas|P5Instruction_Canvas";
    [SerializeField] private string promptTextChildNames = "Prompt_Text|Instruction_Text|NursePromptText|Text (TMP)|Text";
    [SerializeField] private string dialoguePanelChildName = "Dialogue_Panel";
    [SerializeField] private string quizPanelChildName = "Quiz_Panel_6";
    [Tooltip("Seconds the ear-direction hint is shown before the first prompt.")]
    [SerializeField] private float earDirectionHintSeconds = 3.5f;

    [Header("Actors")]
    [SerializeField] private MomAnimationPlayer momAnimation;
    [SerializeField] private YayaAnimationPlayer yayaAnimation;
    [SerializeField] private Part3VisualDemoController part3VisualDemo;

    [Header("Prompt Text")]
    [SerializeField] private TMP_Text nursePromptText;

    [Header("Backend (keywords matched in the nurse's speech)")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool advanceFromBackendJson = true;
    [SerializeField] private bool ignoreFirstBackendJson = true;
    [SerializeField] private string pullEarKeywords = "拉耳朵|耳朵拉|拉一下|輕輕拉|輕輕的|往後拉|往上往後|往下往後|耳溫槍|比較準|準確|不痛|不會痛";
    [SerializeField] private string reportTemperatureKeywords = "沒有發燒|沒發燒|體溫|太棒|很棒|血壓計|量血壓|血壓|帶子|壓脈帶|綁";

    [Header("Ear Thermometer Measurement")]
    [SerializeField] private string thermometerNames = "Thermometer|Thermometer (1)";
    [Tooltip("Mom takes the thermometer from Yaya and holds it to the ear on her side.")]
    [SerializeField] private bool momHoldsThermometer = true;
    [Tooltip("Only when Mom does not hold it: off = Yaya's right ear, on = her left ear.")]
    [SerializeField] private bool measureLeftEar = false;
    [Tooltip("Size of the thermometer (1 = real size).")]
    [SerializeField] private float momThermometerSize = 0.8f;
    [Tooltip("Seconds the thermometer shows itself in front of the nurse (pop + glow) before it is handed to Mom.")]
    [SerializeField] private float handOverPresentSeconds = 0.6f;
    [Tooltip("Seconds it floats from the nurse to Mom's hand.")]
    [SerializeField] private float handOverFlySeconds = 0.6f;
    [Tooltip("Mom's hand at the ear: bigger = more in front of the ear (toward Yaya's face).")]
    [SerializeField] private float momHandInFront = 0.3f;
    [Tooltip("...bigger = more toward Mom's side.")]
    [SerializeField] private float momHandTowardMom = 0.1f;
    [Tooltip("Gap (m) between the probe tip and the ear; smaller = the thermometer closer in.")]
    [SerializeField] private float momGripGap = 0.03f;
    [Tooltip("Mom rests her other hand on top of Yaya's head while measuring.")]
    [SerializeField] private bool momSupportsHead = true;
    [Tooltip("Mom measures the ear Yaya pointed at (off = always the ear on Mom's side).")]
    [SerializeField] private bool measurePointedEar = true;
    [SerializeField] private float measureDuration = 3.2f;
    [SerializeField] private float temperatureResult = 36.0f;
    [Tooltip("Each run Yaya's temperature is a new random value in this range (normal, no fever). Off = always Temperature Result.")]
    [SerializeField] private bool randomTemperature = true;
    [SerializeField] private Vector2 randomTemperatureRange = new Vector2(36.2f, 37.3f);
    [SerializeField] private float temperatureStart = 34.6f;
    [Tooltip("Reading card position in front of the eyes (x right, y up, z forward, meters) - same area as the heartbeat effect.")]
    [SerializeField] private Vector3 readingCardOffset = new Vector3(0f, -0.06f, 0.95f);
    [Tooltip("Moves the reading card to the right (m) so it does not cover Yaya and the thermometer.")]
    [SerializeField] private float readingCardSideShift = 0.24f;
    [SerializeField] private float readingCardWidth = 0.30f;
    [SerializeField] private float readingHoldTime = 2.2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Next part")]
    [SerializeField] private bool startPart7AfterQuiz = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onYayaTemperatureMeasured;
    [SerializeField] private UnityEvent onQuizClosed;

    private const string SubtitleEarDirection =
        "3歲以下，耳朵要向下向後拉；3歲以上，耳朵要向上向後拉";

    private const string PromptPullEar =
        "請一邊輕拉芽芽的耳朵，一邊說明為什麼要拉、這樣量比較準，並安撫她不會痛。";

    private const string PromptMeasuring =
        "媽媽抱著芽芽，請幫芽芽量耳溫，並看著耳溫槍上的數字。";

    private const string PromptReportAndBp =
        "請告訴芽芽量到的體溫結果並稱讚她，接著介紹血壓計，說明要綁帶子才能量血壓。";

    private Coroutine routine;
    private Coroutine quizCompletionRoutine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private bool skipRequested;
    private Button promptSkipButton;
    private int lastStartFrame = -1;

    private GameObject readingCard;
    private TMP_Text readingValueText;
    private TMP_Text readingStatusText;
    private AudioClip beepClip;

    private Transform movedThermometer;
    private Vector3 thermometerHomePosition;
    private Quaternion thermometerHomeRotation;

    // ---------------------------------------------------------------------------------
    // Entry
    // ---------------------------------------------------------------------------------
    public static PediatricVitalSignsPart6Flow FindOrCreate()
    {
        PediatricVitalSignsPart6Flow flow = FindObjectOfType<PediatricVitalSignsPart6Flow>(true);
        if (flow != null)
            return flow;

        GameObject host = null;
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate == null || !candidate.scene.IsValid())
                continue;
            string n = candidate.name.Replace("_", string.Empty).Replace(" ", string.Empty);
            if (string.Equals(n, "Part6Flow", StringComparison.OrdinalIgnoreCase))
            {
                host = candidate;
                break;
            }
        }

        if (host == null)
            host = new GameObject("Part6_Flow");

        return host.AddComponent<PediatricVitalSignsPart6Flow>();
    }

    public void StartPart6()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        enabled = true;

        if (lastStartFrame == Time.frameCount && routine != null)
            return;

        lastStartFrame = Time.frameCount;
        StopRoutine();
        ResetBackendGate();
        waitingForNurseAction = WaitingForNurseAction.None;
        skipRequested = false;
        Debug.Log("[Part6] StartPart6: ear temperature measurement + blood pressure intro.", this);
        routine = StartCoroutine(Part6Routine());
    }

    public void DebugSkipCurrentNurseCheck()
    {
        skipRequested = true;
        waitingForNurseAction = WaitingForNurseAction.None;
    }

    public void SkipVoiceAndStartDialogue()
    {
        DebugSkipCurrentNurseCheck();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        ReturnThermometer(true);
        HideReadingCard();
    }

    private void Update()
    {
        if (!advanceFromBackendJson || waitingForNurseAction == WaitingForNurseAction.None)
            return;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();
        if (network == null || string.IsNullOrWhiteSpace(network.LastJson))
            return;

        HandleBackendJson(network.LastJson);
    }

    // ---------------------------------------------------------------------------------
    // Story
    // ---------------------------------------------------------------------------------
    private IEnumerator Part6Routine()
    {
        ResolveReferences();
        yayaAnimation?.PlaySittingIdle();
        momAnimation?.PlayStandingIdle();

        // 0) Short hint (no Skip), then move on by itself.
        ShowNursePrompt(SubtitleEarDirection, WaitingForNurseAction.None);
        yield return new WaitForSeconds(Mathf.Max(0.5f, earDirectionHintSeconds));

        // 1) Nurse explains pulling the ear (keywords or Skip).
        ShowNursePrompt(PromptPullEar, WaitingForNurseAction.PullEarAndExplain);
        yield return WaitForNurseActionOrSkip();

        // 2) Mom holds Yaya, the nurse measures; reading animation on screen.
        ShowNursePrompt(PromptMeasuring, WaitingForNurseAction.None);
        momAnimation?.PlayComfortToward(yayaAnimation != null ? yayaAnimation.transform : null);
        yayaAnimation?.PlaySittingIdle();
        yield return new WaitForSeconds(1.2f); // Mom walks over first
        yield return MeasureEarTemperature();
        onYayaTemperatureMeasured?.Invoke();

        // 3) Nurse reports the result and introduces the blood-pressure cuff (keywords or Skip).
        ShowNursePrompt(PromptReportAndBp, WaitingForNurseAction.ReportTemperatureAndIntroduceBp);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForNurseActionOrSkip();

        // 4) Quiz 6
        ShowQuiz();
        routine = null;
    }

    // The measured value as the nurse may say it: "36.8", "36", "三十六點八", "三十六度".
    private string TemperatureWords()
    {
        int whole = Mathf.FloorToInt(temperatureResult);
        int tenth = Mathf.RoundToInt((temperatureResult - whole) * 10f) % 10;
        string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        string chineseWhole = digits[whole / 10] + "十" + (whole % 10 == 0 ? string.Empty : digits[whole % 10]);
        return "|" + temperatureResult.ToString("0.0") + "|" + whole + "|" + chineseWhole + "點" + digits[tenth] + "|" + chineseWhole;
    }

    private IEnumerator MeasureEarTemperature()
    {
        if (randomTemperature)
        {
            float lo = Mathf.Min(randomTemperatureRange.x, randomTemperatureRange.y);
            float hi = Mathf.Max(randomTemperatureRange.x, randomTemperatureRange.y);
            temperatureResult = Mathf.Round(UnityEngine.Random.Range(lo, hi) * 10f) / 10f;
            Debug.Log($"[Part6] Yaya's ear temperature this run: {temperatureResult:0.0} °C", this);
        }
        MoveThermometerToEar();
        // The nurse hands the thermometer to Mom, who brings it to the ear.
        if (momHoldsThermometer && MomSkinSwap.Active != null && MomSkinSwap.Active.IsShown)
            yield return new WaitForSeconds(handOverPresentSeconds + handOverFlySeconds + 0.9f);
        ShowReadingCard();
        PlayBeep(1);

        float elapsed = 0f;
        while (elapsed < measureDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / measureDuration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            float value = Mathf.Lerp(temperatureStart, temperatureResult, eased);
            SetReading(value, "測量中…", new Color(0.85f, 0.85f, 0.85f));
            PlaceReadingCard();
            yield return null;
        }

        SetReading(temperatureResult, temperatureResult < 37.5f ? "沒有發燒" : "發燒", temperatureResult < 37.5f ? new Color(0.45f, 0.9f, 0.6f) : new Color(1f, 0.5f, 0.45f));
        PlayBeep(2);
        float hold = 0f;
        while (hold < readingHoldTime)
        {
            hold += Time.deltaTime;
            float pulse = 1f + 0.06f * Mathf.Sin(hold * Mathf.PI * 3f) * Mathf.Exp(-hold * 1.5f);
            if (readingCard != null)
                readingCard.transform.localScale = Vector3.one * (readingCardWidth / 300f) * pulse;
            PlaceReadingCard();
            yield return null;
        }

        HideReadingCard();
        ReturnThermometer(false);
    }

    // ---------------------------------------------------------------------------------
    // Thermometer prop
    // ---------------------------------------------------------------------------------
    private void MoveThermometerToEar()
    {
        if (yayaAnimation == null)
            return;

        Transform head = MixamoHumanoidAvatarBuilder.FindBone(yayaAnimation.transform, "Head");
        if (head == null)
            return;

        if (movedThermometer != null)
            Destroy(movedThermometer.gameObject);

        // It appears in front of the nurse ("item get" style) and is handed to Mom.
        Vector3 start = NurseHandPoint(head.position - Vector3.up * 0.3f);
        Transform best = EarThermometerProp.Create(start, Quaternion.LookRotation(Vector3.up, -Vector3.forward), momThermometerSize);

        movedThermometer = best;
        thermometerHomePosition = best.position;
        thermometerHomeRotation = best.rotation;
        StartCoroutine(MoveThermometerRoutine(best, head));
    }

    // Yaya hands the thermometer to Mom; Mom holds it to Yaya's ear (the ear on Mom's side).
    private IEnumerator MoveThermometerRoutine(Transform thermometer, Transform head)
    {
        Vector3 forward = PrefabPerformanceRuntime.GetActorPelvisForward(yayaAnimation.transform);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        MomSkinSwap mom = MomSkinSwap.Active;
        bool momHolds = momHoldsThermometer && mom != null && mom.IsShown;

        if (momHolds)
            right *= measurePointedEar
                ? (yayaAnimation.PointsLeftEar ? -1f : 1f)
                : (Vector3.Dot(mom.ChestPosition - head.position, right) >= 0f ? 1f : -1f);
        else if (measureLeftEar)
            right = -right;

        Vector3 from = thermometer.position;
        Quaternion fromRotation = thermometer.rotation;
        float tipDistance = EarThermometerProp.TipDistance;

        if (momHolds)
        {
            // Where Mom takes it: in front of her chest, toward the nurse.
            Vector3 towardNurse = Vector3.ProjectOnPlane(from - mom.ChestPosition, Vector3.up);
            towardNurse = towardNurse.sqrMagnitude > 1e-4f ? towardNurse.normalized : forward;
            Vector3 meet = mom.ChestPosition + towardNurse * 0.32f - Vector3.up * 0.12f;
            Quaternion lying = thermometer.rotation;
            ItemPresentFx fx = ItemPresentFx.Attach(thermometer, 0.07f * momThermometerSize + 0.03f, ItemPresentFx.Gold);

            // 1) It pops up in front of the nurse with a glow; Mom already reaches out.
            for (float t = 0f; t < handOverPresentSeconds && thermometer == movedThermometer; t += Time.deltaTime)
            {
                float u = t / Mathf.Max(0.05f, handOverPresentSeconds);
                float pop = EaseOutBack(Mathf.Clamp01(u / 0.5f));
                thermometer.SetPositionAndRotation(NurseHandPoint(from) + Vector3.up * (Mathf.Sin(t * 6f) * 0.006f),
                    Quaternion.AngleAxis(Mathf.Sin(u * Mathf.PI) * 35f, Vector3.up) * lying);
                thermometer.localScale = Vector3.one * Mathf.Max(0.01f, pop);
                mom.ReachTo(Vector3.Lerp(mom.ChestPosition - Vector3.up * 0.25f, meet, u), null, meet);
                yield return null;
            }
            if (thermometer == null)
                yield break;
            thermometer.localScale = Vector3.one;

            // 2) It floats over to her hand in a soft arc.
            Vector3 flyFrom = thermometer.position;
            for (float t = 0f; t < handOverFlySeconds && thermometer == movedThermometer; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.05f, handOverFlySeconds));
                thermometer.SetPositionAndRotation(Vector3.Lerp(flyFrom, meet, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.08f), lying);
                mom.ReachTo(meet, null, meet);
                yield return null;
            }
            if (fx != null)
                fx.Stop();
            if (thermometer == null)
                yield break;
            ItemPresentFx.Burst(thermometer.position, 0.08f, ItemPresentFx.Gold);

            // 3) She brings it to the ear and holds it there while it measures, her other hand
            //    resting on top of Yaya's head to keep it still.
            float elapsed = 0f;
            while (thermometer == movedThermometer && (elapsed < 1.5f || (readingCard != null && readingCard.activeSelf)))
            {
                elapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, elapsed / 0.8f);
                Vector3 ear = head.position + right * 0.085f + Vector3.up * 0.05f;
                // Her hand right outside the ear, at ear height: forearm about level, the
                // thermometer pointing straight in (no hand coming from above or behind).
                // Close to the ear and a little in front of it (toward Yaya's face), so the
                // probe goes straight into the ear canal instead of slanting in from behind.
                Vector3 toMom = Vector3.ProjectOnPlane(mom.ChestPosition - ear, Vector3.up).normalized;
                Vector3 away = (right + forward * momHandInFront + toMom * momHandTowardMom).normalized;
                Vector3 grip = ear + away * (tipDistance + momGripGap) - Vector3.up * EarThermometerProp.TipRise;
                Vector3 hand = Vector3.Lerp(meet, grip, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.05f);
                mom.ReachTo(hand, thermometer, ear);
                if (momSupportsHead && k > 0.3f)
                    mom.SupportWith(head.position + Vector3.up * 0.10f - right * 0.02f - forward * 0.03f);
                yield return null;
            }
            yield break;
        }

        for (float t = 0f; t < 0.6f && thermometer == movedThermometer; t += Time.deltaTime)
        {
            Vector3 ear = head.position + right * (0.085f + tipDistance) + Vector3.up * 0.05f;
            Quaternion toRotation = Quaternion.LookRotation(-right, Vector3.up);
            float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            thermometer.SetPositionAndRotation(Vector3.Lerp(from, ear, k), Quaternion.Slerp(fromRotation, toRotation, k));
            yield return null;
        }

        while (thermometer == movedThermometer && readingCard != null && readingCard.activeSelf)
        {
            Vector3 ear = head.position + right * (0.085f + tipDistance) + Vector3.up * 0.05f;
            thermometer.SetPositionAndRotation(ear, Quaternion.LookRotation(-right, Vector3.up));
            yield return null;
        }
    }

    // A little below and in front of the player's eyes: where the nurse "holds out" a thing.
    private static Vector3 NurseHandPoint(Vector3 fallback)
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position + cam.transform.forward * 0.42f - cam.transform.up * 0.16f : fallback;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private void ReturnThermometer(bool immediate)
    {
        if (movedThermometer == null)
            return;

        Transform t = movedThermometer;
        movedThermometer = null;
        if (immediate || !isActiveAndEnabled)
            Destroy(t.gameObject);
        else
            StartCoroutine(ReturnThermometerRoutine(t, thermometerHomePosition, thermometerHomeRotation));
    }

    private IEnumerator ReturnThermometerRoutine(Transform t, Vector3 position, Quaternion rotation)
    {
        Vector3 from = t.position;
        Quaternion fromRotation = t.rotation;
        for (float time = 0f; time < 0.5f; time += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, time / 0.5f);
            t.SetPositionAndRotation(Vector3.Lerp(from, position, k), Quaternion.Slerp(fromRotation, rotation, k));
            yield return null;
        }
        if (t != null)
            Destroy(t.gameObject);
    }

    // ---------------------------------------------------------------------------------
    // Reading card (built at runtime, shown in front of the eyes like the heartbeat effect)
    // ---------------------------------------------------------------------------------
    private void ShowReadingCard()
    {
        if (readingCard == null)
            BuildReadingCard();
        readingCard.SetActive(true);
        readingCard.transform.localScale = Vector3.one * (readingCardWidth / 300f);
        PlaceReadingCard();
    }

    private void HideReadingCard()
    {
        if (readingCard != null)
            readingCard.SetActive(false);
        foreach (Canvas c in hiddenMeterCanvases)
        {
            if (c != null)
                c.enabled = true;
        }
        hiddenMeterCanvases.Clear();
    }

    // The emotion bar sits where the reading card is shown: hidden while the card is up.
    private readonly System.Collections.Generic.List<Canvas> hiddenMeterCanvases = new System.Collections.Generic.List<Canvas>();
    private KidEmotionPresenter emotionPresenter;

    private void HideEmotionBar()
    {
        if (emotionPresenter == null)
            emotionPresenter = FindObjectOfType<KidEmotionPresenter>();
        if (emotionPresenter == null || emotionPresenter.MeterRoot == null)
            return;
        foreach (Canvas c in emotionPresenter.MeterRoot.GetComponentsInChildren<Canvas>(true))
        {
            if (c.enabled)
            {
                c.enabled = false;
                hiddenMeterCanvases.Add(c);
            }
        }
    }

    private void PlaceReadingCard()
    {
        Camera cam = Camera.main;
        if (readingCard == null || cam == null)
            return;

        HideEmotionBar();
        Transform c = cam.transform;
        Transform t = readingCard.transform;
        t.position = c.position + c.right * (readingCardOffset.x + readingCardSideShift) + c.up * readingCardOffset.y + c.forward * readingCardOffset.z;
        t.rotation = Quaternion.LookRotation(t.position - c.position, c.up);
    }

    private void SetReading(float value, string status, Color statusColor)
    {
        if (readingValueText != null)
            readingValueText.text = value.ToString("0.0") + " °C";
        if (readingStatusText != null)
        {
            readingStatusText.text = status;
            readingStatusText.color = statusColor;
        }
    }

    private void BuildReadingCard()
    {
        readingCard = new GameObject("EarThermometer_ReadingCard", typeof(RectTransform));
        Canvas canvas = readingCard.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        readingCard.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        RectTransform root = (RectTransform)readingCard.transform;
        root.sizeDelta = new Vector2(300f, 170f);

        TMP_FontAsset font = nursePromptText != null ? nursePromptText.font : null;

        Image background = NewImage(root, "Background", new Color(0.11f, 0.13f, 0.16f, 0.92f), Vector2.zero, new Vector2(300f, 170f));
        background.raycastTarget = false;
        Image screen = NewImage(root, "Screen", new Color(0.16f, 0.32f, 0.30f, 1f), new Vector2(0f, 2f), new Vector2(260f, 92f));
        screen.raycastTarget = false;

        NewText(root, "Title", font, "耳溫槍", 24f, new Color(0.75f, 0.78f, 0.8f), new Vector2(0f, 64f), new Vector2(280f, 32f));
        readingValueText = NewText(root, "Value", font, temperatureStart.ToString("0.0") + " °C", 60f, Color.white, new Vector2(0f, 4f), new Vector2(260f, 86f));
        readingStatusText = NewText(root, "Status", font, "測量中…", 24f, new Color(0.85f, 0.85f, 0.85f), new Vector2(0f, -64f), new Vector2(280f, 32f));
        readingCard.SetActive(false);
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
        tmp.raycastTarget = false;
        return tmp;
    }

    private void PlayBeep(int count)
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        if (beepClip == null)
        {
            int rate = 44100;
            int length = (int)(rate * 0.12f);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (length - i) / 400f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 2200f * i / rate) * 0.35f * env;
            }
            beepClip = AudioClip.Create("ThermometerBeep", length, 1, rate, false);
            beepClip.SetData(data, 0);
        }
        StartCoroutine(BeepRoutine(count));
    }

    private IEnumerator BeepRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            audioSource.PlayOneShot(beepClip);
            yield return new WaitForSeconds(0.18f);
        }
    }

    // ---------------------------------------------------------------------------------
    // Prompt / quiz / backend helpers (same behaviour as Part5)
    // ---------------------------------------------------------------------------------
    private IEnumerator WaitForNurseActionOrSkip()
    {
        skipRequested = false;
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None || skipRequested);
        waitingForNurseAction = WaitingForNurseAction.None;
        skipRequested = false;
    }

    private void ShowNursePrompt(string prompt, WaitingForNurseAction waitingAction)
    {
        ResolveReferences();
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);
        SetPanelVisible(nursePromptPanel, true, "TopHint_Panel");
        SetPromptSkipButtonsVisible(waitingAction != WaitingForNurseAction.None);

        if (nursePromptText != null)
            nursePromptText.text = prompt;
        else
            Debug.LogWarning("[Part6] Nurse prompt text not found.", this);

        waitingForNurseAction = waitingAction;
    }

    private void ShowQuiz()
    {
        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        part3VisualDemo?.TemporarilyHideStickerForQuiz();
        SetPanelVisible(quizPanel, true, quizPanelChildName);

        QuizPanelRuntimeHelper.BeginQuiz(quizPanel, quizPanelChildName);
        if (quizCompletionRoutine != null)
            StopCoroutine(quizCompletionRoutine);
        quizCompletionRoutine = StartCoroutine(WaitForQuizPanelClosed());
    }

    private IEnumerator WaitForQuizPanelClosed()
    {
        GameObject watched = null;
        if (quizPanel != null)
        {
            Transform child = FindDeepChild(quizPanel.transform, quizPanelChildName);
            watched = child != null ? child.gameObject : quizPanel;
        }

        yield return null;
        while (watched != null && watched.activeInHierarchy)
            yield return null;

        quizCompletionRoutine = null;
        QuizPanelRuntimeHelper.EndQuiz();
        part3VisualDemo?.RestoreStickerAfterQuiz();
        Debug.Log("[Part6] Quiz 6 closed.", this);
        onQuizClosed?.Invoke();

        if (startPart7AfterQuiz)
            PediatricVitalSignsPart7Flow.FindOrCreate().StartPart7();
    }

    private void HandleBackendJson(string json)
    {
        if (string.Equals(json, lastProcessedBackendJson, StringComparison.Ordinal))
            return;

        lastProcessedBackendJson = json;
        string speechText = ExtractBackendSpeechText(json);
        if (string.IsNullOrWhiteSpace(speechText))
            return;

        string keywords = waitingForNurseAction == WaitingForNurseAction.PullEarAndExplain ? pullEarKeywords
            : waitingForNurseAction == WaitingForNurseAction.ReportTemperatureAndIntroduceBp ? reportTemperatureKeywords + TemperatureWords()
            : string.Empty;

        if (ContainsAnyKeyword(speechText, keywords))
        {
            // Yaya is in meltdown (red): the nurse must calm her before the story moves on.
            if (KidEmotionGate.Blocking)
            {
                Debug.Log("[Part6] Keywords matched, but Yaya must be calmed first.", this);
                return;
            }

            Debug.Log($"[Part6] Backend matched {waitingForNurseAction}. text={speechText}", this);
            waitingForNurseAction = WaitingForNurseAction.None;
            return;
        }

        Debug.Log($"[Part6] Waiting for {waitingForNurseAction}, speech did not match. text={speechText}", this);
    }

    private void ResetBackendGate()
    {
        lastProcessedBackendJson = null;
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();
        if (network != null && ignoreFirstBackendJson && !string.IsNullOrWhiteSpace(network.LastJson))
            lastProcessedBackendJson = network.LastJson;
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        SetPromptSkipButtonsVisible(false);
        HideReadingCard();
        ReturnThermometer(true);
    }

    private void ResolveReferences()
    {
        if (nursePromptPanel == null)
        {
            foreach (string raw in promptPanelNames.Split('|'))
            {
                nursePromptPanel = FindSceneObjectByName(raw.Trim());
                if (nursePromptPanel != null)
                    break;
            }
        }

        if (dialoguePanel == null)
            dialoguePanel = FindSceneObjectByName("Newnewnew Dialogue_Canvas") ?? FindSceneObjectByName("Dialogue_Canvas");
        if (quizPanel == null)
            quizPanel = FindSceneObjectByName("NewQuizCanvas") ?? FindSceneObjectByName("QuizCanvas");
        if (momAnimation == null)
            momAnimation = FindObjectOfType<MomAnimationPlayer>(true);
        if (yayaAnimation == null)
            yayaAnimation = FindObjectOfType<YayaAnimationPlayer>(true);
        if (part3VisualDemo == null)
            part3VisualDemo = FindObjectOfType<Part3VisualDemoController>(true);
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        ResolvePromptText();
    }

    private void ResolvePromptText()
    {
        if (nursePromptPanel == null)
            return;
        if (nursePromptText != null && nursePromptText.transform.IsChildOf(nursePromptPanel.transform))
            return;

        foreach (string raw in promptTextChildNames.Split('|'))
        {
            Transform child = FindDeepChild(nursePromptPanel.transform, raw.Trim());
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                nursePromptText = text;
                return;
            }
        }

        foreach (TMP_Text text in nursePromptPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && text.name.IndexOf("Skip", StringComparison.OrdinalIgnoreCase) < 0)
            {
                nursePromptText = text;
                return;
            }
        }
    }

    private void SetPromptSkipButtonsVisible(bool visible)
    {
        EnsurePromptSkipButton();
        if (promptSkipButton != null)
            promptSkipButton.gameObject.SetActive(visible);
    }

    private void EnsurePromptSkipButton()
    {
        if (nursePromptPanel == null)
            return;

        if (promptSkipButton == null || !promptSkipButton.transform.IsChildOf(nursePromptPanel.transform))
        {
            Transform existing = FindDeepChild(nursePromptPanel.transform, "SkipVoice_Button");
            if (existing != null)
            {
                promptSkipButton = existing.GetComponent<Button>();
                if (promptSkipButton == null)
                    promptSkipButton = existing.gameObject.AddComponent<Button>();
            }
        }

        if (promptSkipButton == null)
            return;

        // Part5 uses the same panel/button: make sure the click goes to the flow that is running.
        promptSkipButton.onClick.RemoveListener(DebugSkipCurrentNurseCheck);
        promptSkipButton.onClick.AddListener(DebugSkipCurrentNurseCheck);
    }

    private static bool ContainsAnyKeyword(string text, string keywords)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keywords))
            return false;
        foreach (string part in keywords.Split('|'))
        {
            string keyword = part.Trim();
            if (keyword.Length > 0 && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static string ExtractBackendSpeechText(string json)
    {
        try
        {
            BackendSpeechResponse response = JsonUtility.FromJson<BackendSpeechResponse>(json);
            if (response != null)
                return ((response.llm_window_text ?? "") + " " + (response.text ?? "") + " " + (response.raw_text ?? "")).Trim();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Part6] Failed to parse backend json: " + e.Message);
        }
        return string.Empty;
    }

    private static void SetPanelVisible(GameObject target, bool visible, string preferredChildName)
    {
        if (target == null)
            return;
        if (visible && !target.activeSelf)
            target.SetActive(true);

        if (target.GetComponent<Canvas>() != null)
        {
            Transform child = FindDeepChild(target.transform, preferredChildName);
            if (child != null)
            {
                child.gameObject.SetActive(visible);
                return;
            }
        }
        target.SetActive(visible);
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;
            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;
        GameObject active = GameObject.Find(objectName);
        if (active != null)
            return active;
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate != null && candidate.name == objectName && candidate.scene.IsValid())
                return candidate;
        }
        return null;
    }

    [Serializable]
    private class BackendSpeechResponse
    {
        public string text;
        public string raw_text;
        public string llm_window_text;
    }
}
