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
    [Header("Ear-pull tip card (instead of the grey hint bar)")]
    [Tooltip("Show the picture card (under 3: pull down and back / 3 and over: up and back, Yaya 4 years) instead of the grey hint text.")]
    [SerializeField] private bool useEarPullCard = true;
    [SerializeField] private string earPullCardResource = "Part6_EarPullCard";
    [Tooltip("How long the card stays (s); the orange bar at the bottom counts down.")]
    [SerializeField] private float earPullCardSeconds = 4.5f;
    [Tooltip("Width of the card in front of the eyes (m, at the shared HUD distance).")]
    [SerializeField] private float earPullCardWidth = 0.62f;
    [Tooltip("x right, y up, z forward from the eyes (m).")]
    [SerializeField] private Vector3 earPullCardOffset = new Vector3(0f, -0.02f, 1.0f);

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
    [Tooltip("New script: Mom holds Yaya and the NURSE takes the ear temperature (the thermometer goes from the nurse's hand to the ear). Off = Mom measures.")]
    [SerializeField] private bool nurseMeasures = true;
    [Tooltip("On: Mom's own hand holds the thermometer at Yaya's ear (her other hand steadies Yaya's head), even when Nurse Measures is on. Off: the thermometer goes from the nurse to the ear by itself.")]
    [SerializeField] private bool momHandHoldsThermometer = true;
    private bool NurseMeasuresNow => nurseMeasures && !momHandHoldsThermometer;
    [Tooltip("Where Yaya's ear opening is, from her head bone (m): x = out to the side, y = up, z = toward her face. Can be changed while playing.")]
    [SerializeField] private Vector3 nurseEarSpot = new Vector3(0.075f, 0.055f, 0.01f);
    [Tooltip("Gap (m) between the probe tip and the ear when the nurse measures.")]
    [SerializeField] private float nurseProbeGap = 0.005f;
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
    [Tooltip("New hold: Mom measures the ear on HER side with the hand nearest to it; her other arm stays relaxed (no hand on Yaya's head, so her arms never cross). Off = the older hold.")]
    [SerializeField] private bool momSimpleEarHold = true;
    [Header("Mom's ear-thermometer hold: one fixed pose")]
    [Tooltip("Mom always walks to the same spot beside Yaya and holds the thermometer at the same ear, straight in from the side. Does not depend on where she was standing (that made her walk through Yaya / the bed).")]
    [SerializeField] private bool momFixedEarHold = true;
    [Tooltip("On = Mom stands at Yaya's LEFT side and measures her left ear (Mom's usual side). Off = right side.")]
    [SerializeField] private bool momFixedOnYayasLeft = true;
    [Tooltip("Mom's spot, from Yaya's hips (m): x = out to the side, z = toward the front (negative = back). Can be changed between runs.")]
    [SerializeField] private Vector2 momFixedSpot = new Vector2(0.58f, 0.10f);
    [Tooltip("How far her hand (the thermometer grip) is out from the ear (m).")]
    [SerializeField] private float momFixedGripOut = 0.02f;
    private Vector3 momFixedHome;
    private Vector3 momFixedHomeForward;
    private bool momFixedWalked;
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
        Texture2D earCard = useEarPullCard ? Resources.Load<Texture2D>(earPullCardResource) : null;
        if (earCard != null)
        {
            // The grey hint bar is not used here: the picture card says it instead.
            SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
            SetPromptSkipButtonsVisible(false);
            yield return ShowEarPullCard(earCard);
        }
        else
        {
            if (useEarPullCard)
                Debug.LogWarning($"[Part6] Resources/{earPullCardResource}.png not found: showing the text hint instead.", this);
            ShowNursePrompt(SubtitleEarDirection, WaitingForNurseAction.None);
            yield return new WaitForSeconds(Mathf.Max(0.5f, earDirectionHintSeconds));
        }

        // 1) Nurse explains pulling the ear (keywords or Skip).
        ShowNursePrompt(PromptPullEar, WaitingForNurseAction.PullEarAndExplain);
        yield return WaitForNurseActionOrSkip();

        // 2) Mom holds Yaya, the nurse measures; reading animation on screen.
        ShowNursePrompt(PromptMeasuring, WaitingForNurseAction.None);
        yayaAnimation?.PlaySittingIdle();
        if (momFixedEarHold && momAnimation != null && yayaAnimation != null && FixedMomSpot(out Vector3 momSpot, out Vector3 momFace))
        {
            // Always the same: Mom walks to her spot beside Yaya and faces Yaya's head.
            momFixedHome = momAnimation.transform.position;
            momFixedHomeForward = momAnimation.IntendedForward;
            momFixedWalked = true;
            yield return momAnimation.WalkTo(momSpot, momFace);
            momAnimation.PlayStandingIdle();
        }
        else
        {
            momAnimation?.PlayComfortToward(yayaAnimation != null ? yayaAnimation.transform : null);
            yield return new WaitForSeconds(1.2f); // Mom walks over first
        }
        yield return MeasureEarTemperature();
        onYayaTemperatureMeasured?.Invoke();
        if (momFixedWalked && momAnimation != null)
        {
            momFixedWalked = false;
            StartCoroutine(momAnimation.WalkTo(momFixedHome, momFixedHomeForward));
        }

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
        if (NurseMeasuresNow)
            yield return new WaitForSeconds(handOverPresentSeconds + handOverFlySeconds + 0.3f);
        else if (momHoldsThermometer && MomSkinSwap.Active != null && MomSkinSwap.Active.IsShown)
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

    // Yaya's forward (horizontal) and the side Mom stands on (her left by default).
    private void YayaAxes(out Vector3 forward, out Vector3 momSide)
    {
        forward = Vector3.ProjectOnPlane(PrefabPerformanceRuntime.GetActorPelvisForward(yayaAnimation.transform), Vector3.up);
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.ProjectOnPlane(yayaAnimation.transform.forward, Vector3.up);
        forward.Normalize();
        Vector3 yayaRight = Vector3.Cross(Vector3.up, forward).normalized;
        momSide = momFixedOnYayasLeft ? -yayaRight : yayaRight;
    }

    private bool FixedMomSpot(out Vector3 spot, out Vector3 face)
    {
        spot = Vector3.zero;
        face = Vector3.forward;
        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(yayaAnimation.transform, "Hips");
        if (hips == null)
            return false;
        YayaAxes(out Vector3 forward, out Vector3 side);
        spot = hips.position + side * momFixedSpot.x + forward * momFixedSpot.y;
        spot.y = momAnimation.transform.position.y;
        face = -side;
        return true;
    }

    // Yaya hands the thermometer to Mom; Mom holds it to Yaya's ear (the ear on Mom's side).
    private IEnumerator MoveThermometerRoutine(Transform thermometer, Transform head)
    {
        Vector3 forward = PrefabPerformanceRuntime.GetActorPelvisForward(yayaAnimation.transform);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        MomSkinSwap mom = MomSkinSwap.Active;
        bool momHolds = !NurseMeasuresNow && momHoldsThermometer && mom != null && mom.IsShown;

        if (momHolds && momFixedEarHold)
        {
            YayaAxes(out Vector3 f, out Vector3 side);
            forward = f;
            right = side;   // the ear on Mom's (fixed) side
        }
        else if (momHolds)
            right *= (measurePointedEar && !momSimpleEarHold)
                ? (yayaAnimation.PointsLeftEar ? -1f : 1f)
                : (Vector3.Dot(mom.ChestPosition - head.position, right) >= 0f ? 1f : -1f);
        else if (measurePointedEar ? yayaAnimation.PointsLeftEar : measureLeftEar)
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
                // Her open hand points toward the thermometer (the nurse), not back at herself.
                // The very first frame decides which arm she uses: the one nearest Yaya's ear, so
                // the same arm takes it and brings it to the ear (no arm crossing over / twisting).
                Vector3 aim = (t <= 0f && momFixedEarHold) ? head.position + right * 0.085f : thermometer.position;
                mom.ReachTo(Vector3.Lerp(mom.ChestPosition - Vector3.up * 0.25f, meet, u), null, aim);
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
                mom.ReachTo(meet, null, meet + towardNurse * 0.4f + Vector3.up * 0.05f);
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
                // Simple hold: the hand straight out from the ear toward Mom, so the probe goes
                // straight in and her arm comes from her own side.
                Vector3 away = momFixedEarHold
                    ? right
                    : momSimpleEarHold
                    ? (right + toMom * 0.35f).normalized
                    : (right + forward * momHandInFront + toMom * momHandTowardMom).normalized;
                Vector3 grip = ear + away * (tipDistance + (momFixedEarHold ? momFixedGripOut : momGripGap)) - Vector3.up * EarThermometerProp.TipRise;
                Vector3 hand = Vector3.Lerp(meet, grip, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.05f);
                mom.ReachTo(hand, thermometer, ear);
                if (momSupportsHead && !momSimpleEarHold && k > 0.3f)
                    mom.SupportWith(head.position + Vector3.up * 0.10f - right * 0.02f - forward * 0.03f);
                yield return null;
            }
            yield break;
        }

        // The nurse measures (new script: Mom holds Yaya, the nurse takes the temperature).
        // The thermometer appears in the nurse's hand, then goes to the ear, the probe
        // pointing straight into it, and stays there while it measures.
        ItemPresentFx nurseFx = ItemPresentFx.Attach(thermometer, 0.07f * momThermometerSize + 0.03f, ItemPresentFx.Gold);
        for (float t = 0f; t < handOverPresentSeconds && thermometer == movedThermometer; t += Time.deltaTime)
        {
            float u = t / Mathf.Max(0.05f, handOverPresentSeconds);
            thermometer.SetPositionAndRotation(NurseHandPoint(from), Quaternion.Slerp(fromRotation, Quaternion.LookRotation(-right, Vector3.up), u));
            thermometer.localScale = Vector3.one * Mathf.Max(0.01f, EaseOutBack(Mathf.Clamp01(u / 0.5f)));
            yield return null;
        }
        if (thermometer == null)
            yield break;
        thermometer.localScale = Vector3.one;
        if (nurseFx != null)
            nurseFx.Stop();

        Vector3 start = thermometer.position;
        Quaternion startRotation = thermometer.rotation;
        float fly = Mathf.Max(0.2f, handOverFlySeconds + 0.2f);
        for (float t = 0f; t < fly && thermometer == movedThermometer; t += Time.deltaTime)
        {
            Vector3 ear = EarGrip(head, right, tipDistance);
            float k = Mathf.SmoothStep(0f, 1f, t / fly);
            thermometer.SetPositionAndRotation(Vector3.Lerp(start, ear, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.04f),
                Quaternion.Slerp(startRotation, Quaternion.LookRotation(-right, Vector3.up), k));
            yield return null;
        }

        while (thermometer == movedThermometer && readingCard != null && readingCard.activeSelf)
        {
            thermometer.SetPositionAndRotation(EarGrip(head, right, tipDistance), Quaternion.LookRotation(-right, Vector3.up));
            yield return null;
        }
    }

    // Where the thermometer's grip goes so that its probe tip just touches the ear opening.
    private Vector3 EarGrip(Transform head, Vector3 side, float tipDistance)
    {
        Vector3 forward = Vector3.Cross(side, Vector3.up).normalized;
        if (Vector3.Dot(forward, PrefabPerformanceRuntime.GetActorPelvisForward(yayaAnimation.transform)) < 0f)
            forward = -forward;
        Vector3 ear = head.position + side * nurseEarSpot.x + Vector3.up * nurseEarSpot.y + forward * nurseEarSpot.z;
        return ear + side * (tipDistance + nurseProbeGap) - Vector3.up * EarThermometerProp.TipRise;
    }

    // A little below and in front of the player's eyes: where the nurse "holds out" a thing.
    private static Vector3 NurseHandPoint(Vector3 fallback)
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position + cam.transform.forward * 0.42f - cam.transform.up * 0.16f : fallback;
    }

    // Picture card in front of the eyes: pops up, the orange bar counts down, then it closes.
    private IEnumerator ShowEarPullCard(Texture2D texture)
    {
        const float cardW = 900f, cardH = 560f;
        GameObject card = new GameObject("Part6_EarPullCard", typeof(RectTransform));
        Canvas canvas = card.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;
        card.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        RectTransform root = (RectTransform)card.transform;
        root.sizeDelta = new Vector2(cardW, cardH);

        GameObject pictureObject = new GameObject("Picture", typeof(RectTransform));
        pictureObject.transform.SetParent(root, false);
        RawImage picture = pictureObject.AddComponent<RawImage>();
        picture.texture = texture;
        picture.raycastTarget = false;
        ((RectTransform)pictureObject.transform).sizeDelta = new Vector2(cardW, cardH);

        // Animated layers: the "✓ 芽芽" chip pops in a moment later, the orange arrow then keeps
        // tugging the ear up and back (same layout as the picture, 900 x 560).
        RawImage check = NewLayer(root, "Check", earPullCardResource + "_Check", new Vector2(783f, 173f));
        RawImage arrowLayer = NewLayer(root, "Arrow", earPullCardResource + "_Arrow", new Vector2(667f, 285f));
        Vector2 pullDir = new Vector2(751.2f - 667.2f, -(209.2f - 285.2f)).normalized;   // up and back (canvas y up)
        Vector2 arrowHome = arrowLayer != null ? arrowLayer.rectTransform.anchoredPosition : Vector2.zero;
        if (check != null) check.rectTransform.localScale = Vector3.zero;
        if (arrowLayer != null) arrowLayer.color = new Color(1f, 1f, 1f, 0f);

        // Count-down bar where the card has room for it (bottom of the card).
        Image track = NewBar(root, "BarTrack", new Color(0.90f, 0.88f, 0.86f, 1f));
        Image fill = NewBar(root, "BarFill", new Color(1f, 0.67f, 0.24f, 1f));
        RectTransform fillRect = fill.rectTransform;

        float seconds = Mathf.Max(1f, earPullCardSeconds);
        const float popIn = 0.35f, popOut = 0.25f;
        float total = popIn + seconds + popOut;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float pop = t < popIn ? EaseOutBack(Mathf.Clamp01(t / popIn))
                : t > popIn + seconds ? 1f - Mathf.SmoothStep(0f, 1f, (t - popIn - seconds) / popOut)
                : 1f;
            float left = 1f - Mathf.Clamp01((t - popIn) / seconds);
            fillRect.sizeDelta = new Vector2(820f * left, 10f);

            float shown = t - popIn - 0.5f;   // half a second after the card is up
            if (check != null)
                check.rectTransform.localScale = Vector3.one * (shown <= 0f ? 0f : EaseOutBack(Mathf.Clamp01(shown / 0.3f)));
            if (arrowLayer != null)
            {
                arrowLayer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(shown / 0.25f));
                // A tug: slides out along the arrow, holds a moment, snaps back; again and again.
                float phase = Mathf.Repeat(Mathf.Max(0f, shown) * 0.9f, 1f);
                float pull = phase < 0.6f ? Mathf.SmoothStep(0f, 1f, phase / 0.6f) : 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.6f) / 0.4f);
                arrowLayer.rectTransform.anchoredPosition = arrowHome + pullDir * (16f * pull);
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                Transform c = cam.transform;
                Vector3 offset = HudDepth.Flatten(earPullCardOffset, out float depthFactor);
                card.transform.SetPositionAndRotation(c.position + c.right * offset.x + c.up * offset.y + c.forward * offset.z, c.rotation);
                card.transform.localScale = Vector3.one * (earPullCardWidth / cardW) * depthFactor * Mathf.Max(0.01f, pop);
            }
            yield return null;
        }
        Destroy(card);
    }

    // A full-card transparent picture layer, turning / growing around `pivotOnCard`
    // (a point on the 900 x 560 card, measured from its top-left corner).
    private static RawImage NewLayer(RectTransform parent, string name, string resource, Vector2 pivotOnCard)
    {
        Texture2D texture = Resources.Load<Texture2D>(resource);
        if (texture == null)
            return null;
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(900f, 560f);
        rt.pivot = new Vector2(pivotOnCard.x / 900f, 1f - pivotOnCard.y / 560f);
        rt.anchoredPosition = new Vector2((rt.pivot.x - 0.5f) * 900f, (rt.pivot.y - 0.5f) * 560f);
        RawImage image = go.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private static Image NewBar(RectTransform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(-410f, -219f);
        rt.sizeDelta = new Vector2(820f, 10f);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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

        string keywords = waitingForNurseAction == WaitingForNurseAction.PullEarAndExplain ? pullEarKeywords + "|耳朵|耳道"
            : waitingForNurseAction == WaitingForNurseAction.ReportTemperatureAndIntroduceBp ? reportTemperatureKeywords + "|耳溫|發燒" + TemperatureWords()
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
        text = SpeechGateMatcher.FixMishearings(text);
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
