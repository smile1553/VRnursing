using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PediatricVitalSignsPart4Flow : MonoBehaviour
{
    private enum WaitingForNurseAction
    {
        None,
        ReassureBeforeApexPulse,
        GiveStickerBeforeEarTemp,
        ExplainEarTemperature,
        ExplainFearToMom
    }

    [Header("Panels")]
    [SerializeField] private GameObject nursePromptPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private string promptPanelName = "P4Instruction_Canvas";
    [SerializeField] private string promptTextChildNames = "Prompt_Text|Instruction_Text|NursePromptText|Text (TMP)|Text";
    [SerializeField] private string dialoguePanelChildName = "Dialogue_Panel";
    [SerializeField] private string quizPanelChildName = "Quiz_Panel_4";

    [Header("Dialogue")]
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private int momEncourageLineIndex = 12;
    [SerializeField] private int yayaRefuseLineIndex = 13;
    [SerializeField] private int momScoldLineIndex = 14;
    [SerializeField] private int momComfortLineIndex = 15;

    [Header("Actors")]
    [SerializeField] private MomAnimationPlayer momAnimation;
    [SerializeField] private YayaAnimationPlayer yayaAnimation;

    [Header("Reward")]
    [SerializeField] private StickerRewardAnimator stickerRewardAnimator;
    [SerializeField] private Part3VisualDemoController part3VisualDemo;
    [SerializeField] private bool playStickerRewardAnimation = true;

    [Header("Prompt Text")]
    [SerializeField] private TMP_Text nursePromptText;

    [Header("Backend")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool advanceFromBackendJson = true;
    [SerializeField] private string reassureBeforeApexPulseKeywords = "不痛|好乖|媽媽聽完|姊姊聽聽|一下子";
    [SerializeField] private string giveStickerBeforeEarTempKeywords = "貼紙|下一個|生命徵象|耳溫|觀察";
    [SerializeField] private string explainEarTemperatureKeywords = "耳溫|耳溫槍|不會痛|一下子|耳朵";
    [SerializeField] private string explainFearToMomKeywords = "害怕|緊張|不熟悉|示範|貼紙";
    [SerializeField] private bool ignoreFirstBackendJson = true;

    [Header("Audio")]
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip momEncourageClip;
    [SerializeField] private AudioClip yayaRefuseClip;
    [SerializeField] private AudioClip momScoldClip;
    [SerializeField] private AudioClip momComfortClip;
    [SerializeField] private bool useAudioLengthForDialogueDelay = true;
    [SerializeField] private float dialogueAdvanceDelay = 4f;
    [SerializeField] private float extraDelayAfterAudio = 0.4f;

    [Header("Options")]
    [SerializeField] private float measurementActionDelay = 3f;
    [SerializeField] private float correctAnswerDelay = 1.2f;
    [SerializeField] private float wrongAnswerDelay = 1.2f;

    [Header("Feedback")]
    [SerializeField] private GameObject correctPopup;
    [SerializeField] private GameObject wrongPopup;

    [Header("Heartbeat Effect")]
    [SerializeField] private GameObject heartbeatEffect;
    [SerializeField] private string heartbeatEffectObjectNames = "HeartbeatEffect|heartbeat_Effect|heartbeat|heartbeat.prefab";
    [HideInInspector] [SerializeField] private Transform heartbeatEffectAnchor;
    [HideInInspector] [SerializeField] private string heartbeatEffectAnchorNames = "Yaya_Chest_StethoscopeAnchor|Yaya_Chest_Anchor|Yaya_StickerAnchor|StethoscopeAnchor|ChestAnchor";
    [HideInInspector] [SerializeField] private Vector3 heartbeatEffectAnchorOffset = new Vector3(0f, 0.16f, -0.05f);
    [Tooltip("Camera-relative position. X = right, Y = up, Z = forward. Use (0, 0, 0.85) for center view.")]
    [SerializeField] private Vector3 heartbeatEffectCameraOffset = new Vector3(0f, -0.16f, 0.95f);
    [Min(0.01f)] [SerializeField] private float heartbeatEffectVisibleScale = 0.10f;
    [Min(0.1f)] [SerializeField] private float heartbeatEffectDuration = 3.2f;
    [SerializeField] private AudioClip heartbeatEffectClip;
    [Min(0f)] [SerializeField] private float heartbeatEffectVolume = 1.5f;
    private GameObject heartbeatEffectInstance;
    private Coroutine heartbeatRuntimeRoutine;
    [SerializeField] private float yayaHugToyDelay = 2f;

    [Header("Quiz")]
    [SerializeField] private bool bindQuizButtonsAutomatically = true;
    [SerializeField] private int expectedQuizButtonCount = 4;
    [SerializeField] private int correctAnswerIndex = 1;
    [SerializeField] private bool hideQuizAfterCorrect = false;
    [SerializeField] private TMP_Text quizQuestionText;
    [SerializeField] private TMP_Text[] quizOptionTexts;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectAnswer;
    [SerializeField] private UnityEvent onWrongAnswer;

    [Header("Flow Link")]
    [SerializeField] private PediatricVitalSignsPart5Flow nextPartFlow;
    [SerializeField] private bool startNextPartAfterCorrect = true;

    private Coroutine routine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private string initialBackendSpeechToIgnore;
    private int lastStartFrame = -1;
    private bool quizButtonsBound;
    private Coroutine correctRoutine;
    private Coroutine wrongRoutine;
    private Coroutine quizCompletionRoutine;
    private Button promptSkipButton;
    private const string NurseReassureBeforeApexPulse =
        "請先安撫芽芽，降低她對心尖脈測量的緊張。";

    private const string ApexPulseAction =
        "請觀察心尖脈測量，並記錄心跳次數。";

    private const string NurseGiveStickerBeforeEarTemp =
        "請用貼紙鼓勵芽芽，並提醒她接下來要觀察下一個生命徵象。";

    private const string NurseExplainEarTemperature =
        "請用孩子聽得懂的方式介紹耳溫測量，讓芽芽知道過程很快、不會痛，也可以先示範。";

    private const string NurseExplainFearToMom =
        "請向媽媽說明芽芽害怕器材的原因，並提出示範、觸摸與貼紙鼓勵等做法。";

    private const string QuizQuestion =
        "考題4：護生使用了什麼方法來降低芽芽的害怕？";

    private static readonly string[] QuizOptions =
    {
        "A. 直接要求芽芽配合",
        "B. 先示範並用貼紙鼓勵",
        "C. 忽略芽芽的害怕",
        "D. 告訴芽芽不配合就不能出院"
    };

    private const string CorrectFeedback = "答對了！";
    private const string WrongFeedback = "再想一下，哪一種方法最能降低芽芽的害怕？";
    private void OnValidate()
    {
        expectedQuizButtonCount = 4;
        correctAnswerIndex = 1;
        startNextPartAfterCorrect = true;
        NormalizeHeartbeatInspectorDefaults();
    }

    private void Awake()
    {
        expectedQuizButtonCount = 4;
        correctAnswerIndex = 1;
        startNextPartAfterCorrect = true;
        NormalizeHeartbeatInspectorDefaults();
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(wrongPopup, false, "Wrong_Popup");
        SetPanelVisible(heartbeatEffect, false, null);
    }

    private void OnEnable()
    {
        ResetBackendGate();
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

    public void StartPart4()
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
        part3VisualDemo?.RestoreStickerAfterQuiz();
        Debug.Log("[Part4] StartPart4: starting ear temperature preparation flow.", this);
        routine = StartCoroutine(Part4Routine());
    }

    public void DebugSkipCurrentNurseCheck()
    {
        if (waitingForNurseAction != WaitingForNurseAction.None)
            waitingForNurseAction = WaitingForNurseAction.None;
    }

    public void SkipVoiceAndStartDialogue()
    {
        DebugSkipCurrentNurseCheck();
    }

    public void SelectA()
    {
        SelectAnswer(0);
    }

    public void SelectB()
    {
        SelectAnswer(1);
    }

    public void SelectC()
    {
        SelectAnswer(2);
    }

    public void SelectD()
    {
        SelectAnswer(3);
    }

    private IEnumerator Part4Routine()
    {
        ShowNursePrompt(NurseReassureBeforeApexPulse, WaitingForNurseAction.ReassureBeforeApexPulse);
        yield return WaitForNurseActionToComplete(
            () => momAnimation?.PlayStandingIdle(),
            () => yayaAnimation?.PlaySittingDisbelief());

        ShowNursePrompt(ApexPulseAction, WaitingForNurseAction.None);
        ShowHeartbeatEffect(true);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingIdle();
        yield return new WaitForSeconds(Mathf.Max(0.1f, heartbeatEffectDuration));
        ShowHeartbeatEffect(false);

        ShowNursePrompt(NurseGiveStickerBeforeEarTemp, WaitingForNurseAction.GiveStickerBeforeEarTemp);
        yield return WaitForNurseActionToComplete(
            () => momAnimation?.PlayStandingIdle(),
            () => yayaAnimation?.PlaySittingDisbelief());
        yield return PlayStickerRewardIfNeeded();

        ShowDialogueLine(momEncourageLineIndex, momEncourageClip);
        momAnimation?.PlayClapping();
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForDialogue(momEncourageClip);
        momAnimation?.PlayStandingIdle();

        ShowNursePrompt(NurseExplainEarTemperature, WaitingForNurseAction.ExplainEarTemperature);
        yield return WaitForNurseActionToComplete(
            () => momAnimation?.PlayStandingIdle(),
            () => yayaAnimation?.PlaySittingIdle());

        ShowDialogueLine(yayaRefuseLineIndex, yayaRefuseClip);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaRefuseClip);

        ShowDialogueLine(momScoldLineIndex, momScoldClip);
        momAnimation?.PlayAngry();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(momScoldClip);

        ShowNursePrompt(NurseExplainFearToMom, WaitingForNurseAction.ExplainFearToMom);
        yield return WaitForNurseActionToComplete(
            () => momAnimation?.PlayAngry(),
            () => yayaAnimation?.PlaySittingDisbelief());

        ShowDialogueLine(momComfortLineIndex, momComfortClip);
        momAnimation?.PlayPointing();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(momComfortClip);

        ShowQuiz();
        routine = null;
    }

    private IEnumerator PlayStickerRewardIfNeeded()
    {
        if (!playStickerRewardAnimation)
            yield break;

        if (stickerRewardAnimator == null)
            stickerRewardAnimator = FindObjectOfType<StickerRewardAnimator>(true);
        if (part3VisualDemo == null)
            part3VisualDemo = FindObjectOfType<Part3VisualDemoController>(true);

        if (stickerRewardAnimator == null)
            yield break;

        yield return stickerRewardAnimator.PlayAndWait();
    }


    private IEnumerator WaitForNurseActionToComplete(Action momLoop, Action yayaLoop)
    {
        momLoop?.Invoke();
        yayaLoop?.Invoke();
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);
    }
    private void HandleBackendJson(string json)
    {
        if (string.Equals(json, lastProcessedBackendJson, StringComparison.Ordinal))
            return;

        lastProcessedBackendJson = json;
        string speechText = ExtractBackendSpeechText(json);
        if (string.IsNullOrWhiteSpace(speechText))
            return;

        string keywords = GetKeywords(waitingForNurseAction);
        if (ContainsAnyKeyword(speechText, keywords))
        {
            Debug.Log($"[Part4] Backend matched {waitingForNurseAction}. text={speechText}", this);
            waitingForNurseAction = WaitingForNurseAction.None;
            return;
        }

        Debug.Log($"[Part4] Waiting for {waitingForNurseAction}, speech did not match. text={speechText}", this);
    }

    private string GetKeywords(WaitingForNurseAction action)
    {
        switch (action)
        {
            case WaitingForNurseAction.ReassureBeforeApexPulse:
                return reassureBeforeApexPulseKeywords;
            case WaitingForNurseAction.GiveStickerBeforeEarTemp:
                return giveStickerBeforeEarTempKeywords;
            case WaitingForNurseAction.ExplainEarTemperature:
                return explainEarTemperatureKeywords;
            case WaitingForNurseAction.ExplainFearToMom:
                return explainFearToMomKeywords;
            default:
                return string.Empty;
        }
    }

    private void ShowNursePrompt(string prompt, WaitingForNurseAction waitingAction)
    {
        ResolveReferences();

        if (dialogueManager != null)
            dialogueManager.StopPlayback();

        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);
        SetPanelVisible(nursePromptPanel, true, "TopHint_Panel");
        SetPromptSkipButtonsVisible(waitingAction != WaitingForNurseAction.None);

        if (nursePromptText != null)
            nursePromptText.text = prompt;
        else
            Debug.LogWarning("[Part4] Nurse prompt text is missing.", this);

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        waitingForNurseAction = waitingAction;
    }

    private void ShowDialogueLine(int lineIndex, AudioClip clip)
    {
        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, true, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);

        if (dialogueManager != null)
        {
            dialogueManager.gameObject.SetActive(true);
            dialogueManager.StopPlayback();
            dialogueManager.ShowLine(lineIndex);
        }
        else
        {
            Debug.LogWarning("[Part4] Dialogue manager is missing.", this);
        }

        PlayAudio(clip);
    }

    private void ShowQuiz()
    {
        ResolveReferences();
        part3VisualDemo?.TemporarilyHideStickerForQuiz();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        SetPanelVisible(quizPanel, true, quizPanelChildName);
        QuizPanelRuntimeHelper.BeginQuiz(quizPanel, quizPanelChildName);

        StopQuizCompletionRoutine();
        quizCompletionRoutine = StartCoroutine(WaitForQuizPanelClosedThenStartNextPart());
    }

    private IEnumerator WaitForQuizPanelClosedThenStartNextPart()
    {
        GameObject watchedPanel = GetQuizContentObject();
        if (watchedPanel == null)
            watchedPanel = quizPanel;

        yield return null;

        while (watchedPanel != null && watchedPanel.activeInHierarchy)
            yield return null;

        quizCompletionRoutine = null;
        QuizPanelRuntimeHelper.EndQuiz();
        part3VisualDemo?.RestoreStickerAfterQuiz();
        StartNextPartFlow();
    }


    private Transform GetQuizContentRoot()
    {
        if (quizPanel == null)
            return null;

        return FindDeepChild(quizPanel.transform, quizPanelChildName) ?? quizPanel.transform;
    }
    private GameObject GetQuizContentObject()
    {
        if (quizPanel == null)
            return null;

        Transform child = FindDeepChild(quizPanel.transform, quizPanelChildName);
        return child != null ? child.gameObject : quizPanel;
    }

    private void StopQuizCompletionRoutine()
    {
        if (quizCompletionRoutine == null)
            return;

        StopCoroutine(quizCompletionRoutine);
        quizCompletionRoutine = null;
    }

    private void SelectAnswer(int index)
    {
        bool correct = index == correctAnswerIndex;
        Debug.Log(correct ? CorrectFeedback : WrongFeedback, this);

        if (correct)
        {
            StopCorrectRoutine();
            StopWrongRoutine();
            SetPanelVisible(wrongPopup, false, "Wrong_Popup");
            SetPanelVisible(correctPopup, true, "Correct_Popup");
            correctRoutine = StartCoroutine(InvokeCorrectAfterDelay());
            return;
        }

        StopWrongRoutine();
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(wrongPopup, true, "Wrong_Popup");
        onWrongAnswer?.Invoke();
        wrongRoutine = StartCoroutine(HideWrongAfterDelay());
    }

    private IEnumerator InvokeCorrectAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, correctAnswerDelay));
        correctRoutine = null;
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        if (hideQuizAfterCorrect)
        {
            SetPanelVisible(quizPanel, false, quizPanelChildName);
            QuizPanelRuntimeHelper.EndQuiz();
        }
        part3VisualDemo?.RestoreStickerAfterQuiz();
        onCorrectAnswer?.Invoke();
        StartNextPartFlow();
    }

    private IEnumerator HideWrongAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, wrongAnswerDelay));
        SetPanelVisible(wrongPopup, false, "Wrong_Popup");
        wrongRoutine = null;
    }

    private void StopCorrectRoutine()
    {
        if (correctRoutine == null)
            return;

        StopCoroutine(correctRoutine);
        correctRoutine = null;
    }

    private void StopWrongRoutine()
    {
        if (wrongRoutine == null)
            return;

        StopCoroutine(wrongRoutine);
        wrongRoutine = null;
    }

    private IEnumerator WaitForDialogue(AudioClip clip)
    {
        yield return new WaitForSeconds(GetDialogueDelay(clip));
    }

    private void PlayAudio(AudioClip clip)
    {
        if (dialogueAudioSource == null)
            return;

        dialogueAudioSource.Stop();

        if (clip != null)
            dialogueAudioSource.PlayOneShot(clip);
    }

    private float GetDialogueDelay(AudioClip clip)
    {
        if (useAudioLengthForDialogueDelay && clip != null)
            return Mathf.Max(0.1f, clip.length + extraDelayAfterAudio);

        return Mathf.Max(0.1f, dialogueAdvanceDelay);
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        if (dialogueManager != null)
            dialogueManager.StopPlayback();

        StopCorrectRoutine();
        StopQuizCompletionRoutine();
        SetPromptSkipButtonsVisible(false);
        ShowHeartbeatEffect(false);
    }

    private void StartNextPartFlow()
    {
        if (!startNextPartAfterCorrect)
        {
            Debug.LogWarning("[Part4] Correct answer received, but Start Next Part After Correct is disabled.", this);
            return;
        }

        if (nextPartFlow == null)
            nextPartFlow = FindObjectOfType<PediatricVitalSignsPart5Flow>(true);

        if (nextPartFlow == null)
        {
            Debug.LogWarning("[Part4] Part5 flow was not found. Please add PediatricVitalSignsPart5Flow to the scene.", this);
            return;
        }

        Debug.Log("[Part4] Correct answer: starting Part5.", this);
        nextPartFlow.StartPart5();
    }

    private void BindQuizButtonsIfNeeded()
    {
        if (!bindQuizButtonsAutomatically || quizButtonsBound || quizPanel == null)
            return;

        Transform root = FindDeepChild(quizPanel.transform, quizPanelChildName) ?? quizPanel.transform;
        Button[] foundButtons = root.GetComponentsInChildren<Button>(true);
        List<Button> buttons = new List<Button>();

        foreach (Button button in foundButtons)
        {
            if (button == null || ShouldIgnoreQuizButton(button.gameObject.name))
                continue;

            buttons.Add(button);
        }

        buttons.Sort(CompareButtonsByScreenOrder);
        int bindCount = expectedQuizButtonCount > 0 ? Mathf.Min(expectedQuizButtonCount, buttons.Count) : buttons.Count;

        for (int i = 0; i < bindCount; i++)
        {
            Button button = buttons[i];
            int capturedIndex = GetAnswerIndexForButton(button, i);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SelectAnswer(capturedIndex));
        }

        quizButtonsBound = bindCount > 0;
        Debug.Log($"[Part4] Auto-bound quiz buttons: {bindCount}/{buttons.Count}", this);
    }

    private void ResolveReferences()
    {
        if (nursePromptPanel == null)
            nursePromptPanel = FindSceneObjectByName(promptPanelName);

        if (dialoguePanel == null)
            dialoguePanel = FindSceneObjectByName("Newnewnew Dialogue_Canvas") ?? FindSceneObjectByName("Dialogue_Canvas");

        if (quizPanel == null)
            quizPanel = FindSceneObjectByName("NewQuizCanvas") ?? FindSceneObjectByName("QuizCanvas");

        if (dialogueManager == null && dialoguePanel != null)
            dialogueManager = dialoguePanel.GetComponentInChildren<NewDialogueManager>(true);

        if (dialogueAudioSource == null)
            dialogueAudioSource = GetComponent<AudioSource>();

        if (heartbeatEffect == null)
            heartbeatEffect = FindFirstNamedObjectOrAsset(heartbeatEffectObjectNames);

        if (correctPopup == null)
            correctPopup = FindSceneObjectByName("Correct_Popup") ?? FindSceneObjectByName("CorrectPopup") ?? FindSceneObjectByName("Correct Panel");

        if (wrongPopup == null)
            wrongPopup = FindSceneObjectByName("Wrong_Popup") ?? FindSceneObjectByName("WrongPopup") ?? FindSceneObjectByName("Wrong Panel");

        ResolvePromptText();
    }

    private void ResolvePromptText()
    {
        if (nursePromptPanel == null)
            return;

        if (nursePromptText != null && nursePromptText.transform.IsChildOf(nursePromptPanel.transform))
            return;

        string[] names = promptTextChildNames.Split('|');
        foreach (string rawName in names)
        {
            string textName = rawName.Trim();
            if (textName.Length == 0)
                continue;

            Transform child = FindDeepChild(nursePromptPanel.transform, textName);
            if (child == null)
                continue;

            TMP_Text text = child.GetComponent<TMP_Text>();
            if (text != null)
            {
                nursePromptText = text;
                return;
            }
        }

        TMP_Text[] texts = nursePromptPanel.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            if (text.name.IndexOf("Skip", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            nursePromptText = text;
            return;
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
            nursePromptPanel = FindSceneObjectByName(promptPanelName);

        if (nursePromptPanel == null)
            return;

        if (promptSkipButton != null && promptSkipButton.transform.IsChildOf(nursePromptPanel.transform))
        {
            BindPromptSkipButton(promptSkipButton);
            return;
        }

        Transform existing = FindDeepChild(nursePromptPanel.transform, "SkipVoice_Button");
        if (existing != null)
        {
            promptSkipButton = existing.GetComponent<Button>();
            if (promptSkipButton == null)
                promptSkipButton = existing.gameObject.AddComponent<Button>();
        }

        if (promptSkipButton == null)
            promptSkipButton = CreatePromptSkipButton();

        BindPromptSkipButton(promptSkipButton);
    }

    private Button CreatePromptSkipButton()
    {
        GameObject buttonObject = new GameObject("SkipVoice_Button");
        buttonObject.transform.SetParent(nursePromptPanel.transform, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = new Vector2(0f, 14f);
        rectTransform.sizeDelta = new Vector2(160f, 34f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRectTransform = labelObject.AddComponent<RectTransform>();
        labelRectTransform.anchorMin = Vector2.zero;
        labelRectTransform.anchorMax = Vector2.one;
        labelRectTransform.offsetMin = Vector2.zero;
        labelRectTransform.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.text = "Skip Voice";
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        buttonObject.SetActive(false);
        return button;
    }

    private void BindPromptSkipButton(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(DebugSkipCurrentNurseCheck);
        button.onClick.AddListener(DebugSkipCurrentNurseCheck);
    }

    private void ResetBackendGate()
    {
        lastProcessedBackendJson = null;
        initialBackendSpeechToIgnore = null;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (network != null && ignoreFirstBackendJson && !string.IsNullOrWhiteSpace(network.LastJson))
        {
            lastProcessedBackendJson = network.LastJson;
            initialBackendSpeechToIgnore = ExtractBackendSpeechText(network.LastJson);
            Debug.Log("[Part4] Cached backend speech will be ignored: " + initialBackendSpeechToIgnore, this);
        }
    }

    private static bool ContainsAnyKeyword(string text, string keywords)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keywords))
            return false;

        string[] parts = keywords.Split('|');
        foreach (string part in parts)
        {
            string keyword = part.Trim();
            if (keyword.Length == 0)
                continue;

            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
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
            {
                string combinedText = string.Empty;

                if (!string.IsNullOrWhiteSpace(response.llm_window_text))
                    combinedText += response.llm_window_text + " ";

                if (!string.IsNullOrWhiteSpace(response.text))
                    combinedText += response.text + " ";

                if (!string.IsNullOrWhiteSpace(response.raw_text))
                    combinedText += response.raw_text;

                return combinedText.Trim();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Part4] Failed to parse backend json: " + e.Message);
        }

        return string.Empty;
    }


    private void NormalizeHeartbeatInspectorDefaults()
    {
        if (heartbeatEffectDuration <= 0f)
            heartbeatEffectDuration = 3.2f;

        if (Vector3.Distance(heartbeatEffectCameraOffset, new Vector3(-0.22f, -0.18f, 0.9f)) < 0.001f || Vector3.Distance(heartbeatEffectCameraOffset, new Vector3(0f, 0f, 0.85f)) < 0.001f)
            heartbeatEffectCameraOffset = new Vector3(0f, -0.16f, 0.95f);

        if (heartbeatEffectVisibleScale <= 0f || Mathf.Abs(heartbeatEffectVisibleScale - 0.22f) < 0.001f || Mathf.Abs(heartbeatEffectVisibleScale - 0.35f) < 0.001f || Mathf.Abs(heartbeatEffectVisibleScale - 0.16f) < 0.001f)
            heartbeatEffectVisibleScale = 0.10f;
    }
    private void ShowHeartbeatEffect(bool visible)
    {
        GameObject effect = heartbeatEffectInstance != null ? heartbeatEffectInstance : heartbeatEffect;
        if (effect == null)
            effect = FindFirstNamedObjectOrAsset(heartbeatEffectObjectNames);

        if (effect == null)
        {
            if (visible)
                Debug.LogWarning("[Part4] Heartbeat effect not found. Assign Heartbeat Effect or name the prefab/object heartbeat.", this);
            return;
        }

        if (!effect.scene.IsValid())
        {
            heartbeatEffectInstance = Instantiate(effect);
            heartbeatEffectInstance.name = effect.name + "_Runtime";
            effect = heartbeatEffectInstance;
        }
        else if (heartbeatEffectInstance == null)
        {
            heartbeatEffectInstance = effect;
        }

        if (visible)
        {
            PlaceHeartbeatEffect(effect);
            EnsureHeartbeatEffectVisible(effect);
        }

        effect.SetActive(visible);
        if (!visible)
            StopHeartbeatRuntimeEffect(effect);
        Debug.Log("[Part4] Heartbeat effect visible=" + visible + ", object=" + effect.name + ", position=" + effect.transform.position, this);
        if (visible)
        {
            Animator animator = effect.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Play(0, 0, 0f);
                animator.Update(0f);
            }

            StartHeartbeatRuntimeEffect(effect);
        }
    }



    private void StartHeartbeatRuntimeEffect(GameObject effect)
    {
        if (effect == null)
            return;

        StopHeartbeatRuntimeEffect(effect);
        heartbeatRuntimeRoutine = StartCoroutine(HeartbeatRuntimeRoutine(effect));
    }

    private void StopHeartbeatRuntimeEffect(GameObject effect)
    {
        if (heartbeatRuntimeRoutine != null)
        {
            StopCoroutine(heartbeatRuntimeRoutine);
            heartbeatRuntimeRoutine = null;
        }

        if (effect != null)
        {
            AudioSource source = effect.GetComponentInChildren<AudioSource>(true);
            if (source != null)
                source.Stop();
        }
    }

    private IEnumerator HeartbeatRuntimeRoutine(GameObject effect)
    {
        EnsureHeartbeatAudio(effect);
        LineRenderer[] rings = EnsureHeartbeatRings(effect);
        Vector3 baseScale = Vector3.one * heartbeatEffectVisibleScale;
        AudioSource source = effect.GetComponentInChildren<AudioSource>(true);
        if (source != null && source.clip != null)
        {
            source.Stop();
            source.loop = true;
            source.Play();
        }

        float elapsed = 0f;
        while (effect != null && effect.activeInHierarchy && elapsed < heartbeatEffectDuration)
        {
            elapsed += Time.deltaTime;
            float cycle = Mathf.Repeat(elapsed, 1f);
            float beat = Mathf.Exp(-Mathf.Pow((cycle - 0.08f) / 0.045f, 2f)) + 0.72f * Mathf.Exp(-Mathf.Pow((cycle - 0.28f) / 0.055f, 2f));
            effect.transform.localScale = baseScale * (1f + beat * 0.18f);

            for (int i = 0; i < rings.Length; i++)
                UpdateHeartbeatRing(rings[i], Mathf.Repeat(cycle - i * 0.23f + 1f, 1f));

            yield return null;
        }

        if (source != null)
            source.Stop();
        heartbeatRuntimeRoutine = null;
    }

    private void EnsureHeartbeatAudio(GameObject effect)
    {
        AudioSource source = effect.GetComponentInChildren<AudioSource>(true);
        if (source == null)
            source = effect.AddComponent<AudioSource>();

        if (heartbeatEffectClip == null)
            heartbeatEffectClip = FindAudioClipByName("heartbeat") ?? FindAudioClipByName("Heartbeat") ?? FindAudioClipByName("heartbeat-sound") ?? FindAudioClipByName("Heartbeat-sound");

        if (source.clip == null)
            source.clip = heartbeatEffectClip;

        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = heartbeatEffectVolume;
    }

    private LineRenderer[] EnsureHeartbeatRings(GameObject effect)
    {
        Transform root = effect.transform.Find("Heartbeat_Runtime_Rings");
        if (root == null)
        {
            GameObject rootObject = new GameObject("Heartbeat_Runtime_Rings");
            rootObject.transform.SetParent(effect.transform, false);
            root = rootObject.transform;
        }

        LineRenderer[] rings = root.GetComponentsInChildren<LineRenderer>(true);
        if (rings.Length >= 3)
            return rings;

        Material material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        material.color = new Color(1f, 0.45f, 0.78f, 0.65f);

        List<LineRenderer> result = new List<LineRenderer>(rings);
        for (int i = result.Count; i < 3; i++)
        {
            GameObject ringObject = new GameObject("Heartbeat_Ripple_" + (i + 1));
            ringObject.transform.SetParent(root, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 72;
            ring.material = material;
            ring.numCapVertices = 4;
            result.Add(ring);
        }

        return result.ToArray();
    }

    private static void UpdateHeartbeatRing(LineRenderer ring, float progress)
    {
        if (ring == null)
            return;

        float alpha = Mathf.Clamp01(1f - progress);
        float radius = Mathf.Lerp(0.65f, 1.65f, progress);
        float width = Mathf.Lerp(0.018f, 0.004f, progress);
        Color color = new Color(1f, 0.45f, 0.78f, alpha * 0.55f);
        ring.startColor = color;
        ring.endColor = color;
        ring.startWidth = width;
        ring.endWidth = width;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.02f));
        }
    }

    private static AudioClip FindAudioClipByName(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return null;

        AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (AudioClip clip in clips)
        {
            if (clip != null && clip.name.IndexOf(clipName, StringComparison.OrdinalIgnoreCase) >= 0)
                return clip;
        }

        return null;
    }
    private void EnsureHeartbeatEffectVisible(GameObject effect)
    {
        if (effect == null)
            return;

        if (effect.transform.localScale.sqrMagnitude < 0.0001f)
            effect.transform.localScale = Vector3.one * heartbeatEffectVisibleScale;

        Renderer[] renderers = effect.GetComponentsInChildren<Renderer>(true);
        Material fallbackMaterial = null;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            renderer.enabled = true;
            if ((renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0 || renderer.sharedMaterial == null) && fallbackMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                fallbackMaterial = new Material(shader);
                fallbackMaterial.color = new Color(1f, 0.12f, 0.28f, 1f);
            }

            if (fallbackMaterial != null && (renderer.sharedMaterial == null || renderer.sharedMaterials.Length == 0))
                renderer.material = fallbackMaterial;
        }

        if (renderers.Length == 0)
            CreateFallbackHeartbeatVisual(effect.transform);
    }

    private void CreateFallbackHeartbeatVisual(Transform parent)
    {
        GameObject heart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        heart.name = "Heartbeat_Fallback_Heart";
        heart.transform.SetParent(parent, false);
        heart.transform.localPosition = Vector3.zero;
        heart.transform.localScale = Vector3.one * heartbeatEffectVisibleScale;

        Renderer renderer = heart.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = new Color(1f, 0.12f, 0.28f, 1f);
            renderer.material = material;
        }
    }
    private void PlaceHeartbeatEffect(GameObject effect)
    {
        if (effect == null)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Transform cameraTransform = camera.transform;
        effect.transform.SetParent(null, true);
        effect.transform.position = cameraTransform.position
            + cameraTransform.right * heartbeatEffectCameraOffset.x
            + cameraTransform.up * heartbeatEffectCameraOffset.y
            + cameraTransform.forward * heartbeatEffectCameraOffset.z;
        effect.transform.rotation = Quaternion.LookRotation(effect.transform.position - cameraTransform.position, cameraTransform.up);
        effect.transform.localScale = Vector3.one * heartbeatEffectVisibleScale;
    }

    private Transform FindFirstNamedTransform(string objectNames)
    {
        GameObject found = FindFirstNamedObjectOrAsset(objectNames);
        return found != null ? found.transform : null;
    }
    private static int GetAnswerIndexForButton(Button button, int fallbackIndex)
    {
        if (button == null)
            return fallbackIndex;

        string value = button.gameObject.name;
        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
            value += " " + tmpText.text;

        Text legacyText = button.GetComponentInChildren<Text>(true);
        if (legacyText != null)
            value += " " + legacyText.text;

        if (ContainsAnswerToken(value, "A")) return 0;
        if (ContainsAnswerToken(value, "B")) return 1;
        if (ContainsAnswerToken(value, "C")) return 2;
        if (ContainsAnswerToken(value, "D")) return 3;
        return fallbackIndex;
    }

    private static bool ContainsAnswerToken(string value, string token)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.IndexOf("(" + token + ")", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf(token + ".", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("Option_" + token, StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("Answer_" + token, StringComparison.OrdinalIgnoreCase) >= 0
            || value.Equals(token, StringComparison.OrdinalIgnoreCase);
    }

    private GameObject FindFirstNamedObjectOrAsset(string names)
    {
        if (string.IsNullOrWhiteSpace(names))
            return null;

        string[] splitNames = names.Split('|');
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (string rawName in splitNames)
        {
            string objectName = rawName.Trim();
            if (objectName.Length == 0)
                continue;

            GameObject active = GameObject.Find(objectName);
            if (active != null)
                return active;

            foreach (GameObject candidate in allObjects)
            {
                if (candidate == null || candidate.name != objectName)
                    continue;

                return candidate;
            }
        }

        return null;
    }
    private static int CompareButtonsByScreenOrder(Button left, Button right)
    {
        Vector3 leftPosition = left.transform.position;
        Vector3 rightPosition = right.transform.position;

        int yCompare = rightPosition.y.CompareTo(leftPosition.y);
        if (yCompare != 0)
            return yCompare;

        return leftPosition.x.CompareTo(rightPosition.x);
    }

    private static int CompareTextsByScreenOrder(TMP_Text left, TMP_Text right)
    {
        Vector3 leftPosition = left.transform.position;
        Vector3 rightPosition = right.transform.position;

        int yCompare = rightPosition.y.CompareTo(leftPosition.y);
        if (yCompare != 0)
            return yCompare;

        return leftPosition.x.CompareTo(rightPosition.x);
    }

    private static bool ShouldIgnoreQuizButton(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
            return false;

        return buttonName.IndexOf("skip", StringComparison.OrdinalIgnoreCase) >= 0
            || buttonName.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0
            || buttonName.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0
            || buttonName.IndexOf("ok", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetPanelVisible(GameObject target, bool visible, string preferredChildName)
    {
        if (target == null)
            return;

        if (visible && !target.activeSelf)
            target.SetActive(true);

        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas != null)
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

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject candidate in allObjects)
        {
            if (candidate == null || candidate.name != objectName)
                continue;

            if (!candidate.scene.IsValid())
                continue;

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
