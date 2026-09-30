using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PediatricVitalSignsPart3Flow : MonoBehaviour
{
    private enum WaitingForNurseAction
    {
        None,
        AskMomPreference,
        ReassureWithSticker,
        LetYayaListenMom
    }

    [Header("Panels")]
    [SerializeField] private GameObject nursePromptPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private string promptPanelName = "P3Instruction_Canvas";
    [SerializeField] private string promptTextChildNames = "Prompt_Text|Instruction_Text|NursePromptText|Text (TMP)|Text";
    [SerializeField] private string dialoguePanelChildName = "Dialogue_Panel";
    [SerializeField] private string quizPanelChildName = "Quiz_Panel_3";
    [SerializeField] private string hiddenCombinedKidObjectNames = "Kid_Combined|Kid_Combined 1|Sitting Idle|Sitting Idle test|kidtakingbeartemp";
    [SerializeField] private bool hideCombinedKidDuringPart3 = true;

    [Header("Dialogue")]
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private int momLikesStickerLineIndex = 5;
    [SerializeField] private int momEncourageLineIndex = 5;
    [SerializeField] private int yayaRefuseLineIndex = 6;
    [SerializeField] private int momDoctorLineIndex = 7;
    [SerializeField] private int momHeartbeatLineIndex = 8;
    [SerializeField] private int yayaEarPainLineIndex = 9;

    [Header("Actors")]
    [SerializeField] private MomAnimationPlayer momAnimation;
    [SerializeField] private YayaAnimationPlayer yayaAnimation;

    [Header("Visual Demo")]
    [SerializeField] private Part3VisualDemoController visualDemo;

    [Header("Prompt Text")]
    [SerializeField] private TMP_Text nursePromptText;

    [Header("Backend")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool advanceFromBackendJson = true;
    [SerializeField] private string askMomPreferenceKeywords = "喜歡|貼紙|玩具|故事|卡通";
    [SerializeField] private string reassureWithStickerKeywords = "貼紙|不打針|不會痛|一下子";
    [SerializeField] private string letYayaListenMomKeywords = "媽媽|心跳|聽診器|聽聽";
    [SerializeField] private bool ignoreFirstBackendJson = true;

    [Header("Audio")]
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip momStickerClip;
    [SerializeField] private AudioClip momEncourageClip;
    [SerializeField] private AudioClip yayaRefuseClip;
    [SerializeField] private AudioClip momDoctorClip;
    [SerializeField] private AudioClip momHeartbeatClip;
    [SerializeField] private AudioClip yayaEarPainClip;
    [SerializeField] private AudioClip nurseShowStickerClip;
    [SerializeField] private AudioClip nursePromiseStickerClip;
    [SerializeField] private AudioClip nurseLetYayaListenMomClip;
    [SerializeField] private bool useAudioLengthForDialogueDelay = true;
    [SerializeField] private float dialogueAdvanceDelay = 4f;
    [SerializeField] private float extraDelayAfterAudio = 0.4f;

    [Header("Quiz")]
    [SerializeField] private bool bindQuizButtonsAutomatically = true;
    [SerializeField] private int expectedQuizButtonCount = 4;
    [SerializeField] private int correctAnswerIndex = 1;
    [SerializeField] private bool hideQuizAfterCorrect = true;
    [SerializeField] private float correctAnswerDelay = 1.2f;
    [SerializeField] private float wrongAnswerDelay = 1.2f;
    [SerializeField] private TMP_Text quizQuestionText;
    [SerializeField] private TMP_Text[] quizOptionTexts;
    [SerializeField] private GameObject correctPopup;
    [SerializeField] private GameObject wrongPopup;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectAnswer;
    [SerializeField] private UnityEvent onWrongAnswer;

    private Coroutine routine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private int lastStartFrame = -1;
    private bool quizButtonsBound;
    private bool promptSkipRequested;
    private Coroutine correctRoutine;
    private Coroutine wrongRoutine;
    private const string AskPreferencePrompt =
        "護生如何知道芽芽喜歡什麼東西呢？";

    private const string ReassureWithStickerPrompt =
        "以貼紙鼓勵芽芽，並告訴芽芽下一個要觀察的生命徵象。";

    private const string StickerStrategyPrompt =
        "以貼紙，鼓勵病童的正向行為。";

    private const string LetYayaListenMomPrompt =
        "請用溫柔的方式安撫芽芽，讓她知道聽診不會痛、一下子就好，並安排媽媽先示範。";
    private const string QuizQuestion =
        "考題3：請問護生使用了什麼方法以減少芽芽的害怕？";

    private static readonly string[] QuizOptions =
    {
        "A. 芽芽和護生角色扮演",
        "B. 芽芽和媽媽角色扮演",
        "C. 護生和媽媽角色扮演"
    };

    private const string CorrectFeedback = "答對了！";
    private const string WrongFeedback = "再想一下，哪一種角色扮演最能減少芽芽害怕？";
    private void Awake()
    {
        expectedQuizButtonCount = Mathf.Max(expectedQuizButtonCount, 4);
        correctAnswerIndex = 1;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        ResolveReferences();
        SetCombinedKidVisible(false);
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(wrongPopup, false, "Wrong_Popup");
    }

    private void OnEnable()
    {
        ResetBackendGate();
    }

    private void Update()
    {
        if (hideCombinedKidDuringPart3)
            SetCombinedKidVisible(false);

        if (!advanceFromBackendJson || waitingForNurseAction == WaitingForNurseAction.None)
            return;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (network == null || string.IsNullOrWhiteSpace(network.LastJson))
            return;

        HandleBackendJson(network.LastJson);
    }

    public void StartPart3()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        enabled = true;

        if (lastStartFrame == Time.frameCount && routine != null)
            return;

        lastStartFrame = Time.frameCount;
        StopRoutine();
        ResetBackendGate();
        SetCombinedKidVisible(false);
        waitingForNurseAction = WaitingForNurseAction.None;
        promptSkipRequested = false;
        Debug.Log("[Part3] StartPart3: starting sticker distraction flow.", this);
        routine = StartCoroutine(Part3Routine());
    }

    public void DebugSkipCurrentNurseCheck()
    {
        promptSkipRequested = true;

        if (waitingForNurseAction != WaitingForNurseAction.None)
            waitingForNurseAction = WaitingForNurseAction.None;
    }

    public void SkipVoiceAndStartDialogue()
    {
        SetPromptSkipButtonsVisible(false);
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

    private IEnumerator Part3Routine()
    {
        visualDemo?.HideSticker();
        ShowNursePrompt(AskPreferencePrompt, WaitingForNurseAction.AskMomPreference);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForNurseActionToComplete(
            () => momAnimation?.PlayStandingIdle(),
            () => yayaAnimation?.PlaySittingDisbelief());

        ShowDialogueLine(momLikesStickerLineIndex, momStickerClip);
        momAnimation?.PlayTalking();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(momStickerClip);

        visualDemo?.ResetHudStickerSelection();
        visualDemo?.ShowSticker();
        ShowNursePrompt(ReassureWithStickerPrompt, WaitingForNurseAction.ReassureWithSticker);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
        PlayAudio(nursePromiseStickerClip);
        yield return WaitForNurseActionToComplete(
            () => momAnimation?.PlayStandingIdle(),
            () => yayaAnimation?.PlaySittingDisbelief());
        yield return WaitForStickerSelectionOrSkip();
        visualDemo?.HighlightSticker();

        ShowDialogueLine(momEncourageLineIndex, momEncourageClip);
        yield return HoldMomClapping(GetDialogueDelay(momEncourageClip));
        momAnimation?.PlayStandingIdle();

        ShowDialogueLine(yayaRefuseLineIndex, yayaRefuseClip);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaRefuseClip);

        visualDemo?.HighlightStethoscope();
        ShowNursePrompt(LetYayaListenMomPrompt, WaitingForNurseAction.LetYayaListenMom);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingIdle();
        PlayAudio(nurseLetYayaListenMomClip);
        yield return WaitForNurseActionToComplete(
            () => momAnimation?.PlayStandingIdle(),
            () => yayaAnimation?.PlaySittingIdle());

        ShowDialogueLine(momDoctorLineIndex, momDoctorClip);
        Debug.Log($"[Part3] Starting MomBend/KidListen. mom={(momAnimation != null ? momAnimation.name : "null")}, yaya={(yayaAnimation != null ? yayaAnimation.name : "null")}", this);
        yield return HoldMomBendKidListen(GetDialogueDelay(momDoctorClip));

        momAnimation?.PlayBend();
        yayaAnimation?.PlayKidListen();
        if (visualDemo != null)
        {
            yield return visualDemo.MoveStethoscopeForYayaListeningMom();
            yield return visualDemo.PlayHeartbeat();
        }
        else
        {
            yield return new WaitForSeconds(2f);
        }

        ShowDialogueLine(momHeartbeatLineIndex, momHeartbeatClip);
        momAnimation?.PlayBend();
        yayaAnimation?.PlayKidListen();
        yield return WaitForDialogue(momHeartbeatClip);

        ShowDialogueLine(yayaEarPainLineIndex, yayaEarPainClip);
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaEarPainClip);

        ShowQuiz();
        routine = null;
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
            Debug.Log($"[Part3] Backend matched {waitingForNurseAction}. text={speechText}", this);
            waitingForNurseAction = WaitingForNurseAction.None;
            return;
        }

        Debug.Log($"[Part3] Waiting for {waitingForNurseAction}, speech did not match. text={speechText}", this);
    }

    private string GetKeywords(WaitingForNurseAction action)
    {
        switch (action)
        {
            case WaitingForNurseAction.AskMomPreference:
                return askMomPreferenceKeywords;
            case WaitingForNurseAction.ReassureWithSticker:
                return reassureWithStickerKeywords;
            case WaitingForNurseAction.LetYayaListenMom:
                return letYayaListenMomKeywords;
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

        if (waitingAction == WaitingForNurseAction.ReassureWithSticker)
            visualDemo?.ShowSticker();

        if (nursePromptText != null)
        {
            nursePromptText.enableWordWrapping = true;
            nursePromptText.enableAutoSizing = false;
            TryAddGlyphs(nursePromptText, prompt);
            nursePromptText.text = prompt;
        }
        else
            Debug.LogWarning("[Part3] Nurse prompt text is missing.", this);

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
            Debug.LogWarning("[Part3] Dialogue manager is missing.", this);
        }

        PlayAudio(clip);
    }

    private void ShowQuiz()
    {
        ResolveReferences();
        visualDemo?.TemporarilyHideStickerForQuiz();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        WorldSpaceUiPlacer.PlaceCanvasInFrontOfCamera(quizPanel);
        WorldSpaceUiPlacer.MatchQuizPanelToQuizOne(quizPanel, quizPanelChildName);
        SetPanelVisible(quizPanel, true, quizPanelChildName);
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(wrongPopup, false, "Wrong_Popup");
        BindQuizButtonsIfNeeded();
    }

    private void ApplyQuizText()
    {
        if (quizQuestionText != null)
            quizQuestionText.text = QuizQuestion;

        if (quizOptionTexts == null)
            return;

        for (int i = 0; i < quizOptionTexts.Length && i < QuizOptions.Length; i++)
        {
            if (quizOptionTexts[i] != null)
                quizOptionTexts[i].text = QuizOptions[i];
        }
    }

    private void SelectAnswer(int index)
    {
        bool correct = index == correctAnswerIndex;
        Debug.Log(correct ? CorrectFeedback : WrongFeedback, this);

        if (correct)
        {
            StopWrongRoutine();
            SetPanelVisible(wrongPopup, false, "Wrong_Popup");
            SetPanelVisible(correctPopup, true, "Correct_Popup");

            if (hideQuizAfterCorrect)
                SetPanelVisible(quizPanel, false, quizPanelChildName);

            yayaAnimation?.PlaySittingIdle();
            StopCorrectRoutine();
            correctRoutine = StartCoroutine(InvokeCorrectAfterDelay());
            return;
        }

        StopCorrectRoutine();
        StopWrongRoutine();
        SetPanelVisible(quizPanel, true, quizPanelChildName);
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(wrongPopup, true, "Wrong_Popup");
        wrongRoutine = StartCoroutine(HideWrongAfterDelay());
    }

    private IEnumerator WaitForDialogue(AudioClip clip)
    {
        yield return new WaitForSeconds(GetDialogueDelay(clip));
    }




    private IEnumerator HoldMomClapping(float duration)
    {
        float endAt = Time.time + Mathf.Max(0.1f, duration);
        float nextReplayAt = 0f;
        while (Time.time < endAt)
        {
            if (Time.time >= nextReplayAt)
            {
                momAnimation?.PlayClapping();
                nextReplayAt = Time.time + 0.45f;
            }

            yield return null;
        }
    }

    private IEnumerator HoldMomBendKidListen(float duration)
    {
        momAnimation?.PlayBend();
        yayaAnimation?.PlayKidListen();
        yield return new WaitForSeconds(Mathf.Max(0.1f, duration));
    }

    private IEnumerator WaitForNurseActionToComplete(Action momLoop, Action yayaLoop)
    {
        float nextReplayAt = 0f;
        while (waitingForNurseAction != WaitingForNurseAction.None)
        {
            if (Time.time >= nextReplayAt)
            {
                momLoop?.Invoke();
                yayaLoop?.Invoke();
                nextReplayAt = Time.time + 2f;
            }

            yield return null;
        }
    }
    private IEnumerator WaitForStickerSelectionOrSkip()
    {
        if (visualDemo == null)
            yield break;

        visualDemo.ShowSticker();
        while (!promptSkipRequested && !visualDemo.HudStickerWasSelected)
            yield return null;

        promptSkipRequested = false;
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
        StopWrongRoutine();
        SetPromptSkipButtonsVisible(false);
    }

    private IEnumerator InvokeCorrectAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, correctAnswerDelay));
        correctRoutine = null;
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(quizPanel, false, quizPanelChildName);
        visualDemo?.RestoreStickerAfterQuiz();
        onCorrectAnswer?.Invoke();
    }

    private void StopCorrectRoutine()
    {
        if (correctRoutine == null)
            return;

        StopCoroutine(correctRoutine);
        correctRoutine = null;
    }

    private IEnumerator HideWrongAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, wrongAnswerDelay));
        wrongRoutine = null;
        SetPanelVisible(wrongPopup, false, "Wrong_Popup");
    }

    private void StopWrongRoutine()
    {
        if (wrongRoutine == null)
            return;

        StopCoroutine(wrongRoutine);
        wrongRoutine = null;
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
            buttons[i].onClick.AddListener(() => SelectAnswer(capturedIndex));
        }

        quizButtonsBound = bindCount > 0;
        Debug.Log($"[Part3] Auto-bound quiz buttons: {bindCount}/{buttons.Count}", this);
    }

    private void ResolveReferences()
    {
        if (nursePromptPanel == null)
            nursePromptPanel = FindSceneObjectByName(promptPanelName);

        if (dialoguePanel == null)
            dialoguePanel = FindSceneObjectByName("Newnewnew Dialogue_Canvas") ?? FindSceneObjectByName("Dialogue_Canvas");

        if (quizPanel == null)
            quizPanel = FindSceneObjectByName("NewQuizCanvas") ?? FindSceneObjectByName("QuizCanvas");

        if (correctPopup == null)
            correctPopup = FindSceneObjectByName("Correct_Popup");

        if (wrongPopup == null)
            wrongPopup = FindSceneObjectByName("Wrong_Popup");

        if (dialogueManager == null && dialoguePanel != null)
            dialogueManager = dialoguePanel.GetComponentInChildren<NewDialogueManager>(true);

        if (dialogueAudioSource == null)
            dialogueAudioSource = GetComponent<AudioSource>();

        if (momAnimation == null)
            momAnimation = FindObjectOfType<MomAnimationPlayer>(true);

        if (yayaAnimation == null)
            yayaAnimation = FindObjectOfType<YayaAnimationPlayer>(true);

        if (visualDemo == null)
            visualDemo = FindObjectOfType<Part3VisualDemoController>(true);

        if (visualDemo == null)
        {
            GameObject visualDemoObject = new GameObject("Part3_VisualDemoController");
            visualDemo = visualDemoObject.AddComponent<Part3VisualDemoController>();
        }

        ResolvePromptText();
    }

    private void SetPromptSkipButtonsVisible(bool visible)
    {
        if (nursePromptPanel == null)
            return;

        SetNamedChildrenVisible(nursePromptPanel.transform, "SkipVoice_Button", visible);
    }

    private void SetCombinedKidVisible(bool visible)
    {
        if (string.IsNullOrWhiteSpace(hiddenCombinedKidObjectNames))
            return;

        string[] names = hiddenCombinedKidObjectNames.Split('|');
        foreach (string rawName in names)
        {
            string objectName = rawName.Trim();
            if (objectName.Length == 0)
                continue;

            GameObject objectToHide = FindSceneObjectByName(objectName);
            if (objectToHide != null)
                objectToHide.SetActive(visible);
        }
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
            Transform child = FindDeepChild(nursePromptPanel.transform, rawName.Trim());
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
        if (texts.Length > 0)
            nursePromptText = texts[0];
    }

    private void TryAddGlyphs(TMP_Text text, string value)
    {
        if (text == null || text.font == null || string.IsNullOrEmpty(value))
            return;

        text.font.TryAddCharacters(value, out string missingCharacters);
        if (!string.IsNullOrEmpty(missingCharacters))
            Debug.LogWarning("[Part3] TMP font is missing glyphs: " + missingCharacters, this);
    }

    private void ResetBackendGate()
    {
        lastProcessedBackendJson = null;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (network != null && ignoreFirstBackendJson && !string.IsNullOrWhiteSpace(network.LastJson))
            lastProcessedBackendJson = network.LastJson;
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

    private static bool ShouldIgnoreQuizButton(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
            return false;

        return buttonName.IndexOf("skip", StringComparison.OrdinalIgnoreCase) >= 0
            || buttonName.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0
            || buttonName.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0
            || buttonName.IndexOf("ok", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int GetAnswerIndexForButton(Button button, int fallbackIndex)
    {
        if (button == null)
            return fallbackIndex;

        string name = button.gameObject.name;
        if (string.IsNullOrWhiteSpace(name))
            return fallbackIndex;

        if (ContainsAnswerToken(name, "A"))
            return 0;

        if (ContainsAnswerToken(name, "B"))
            return 1;

        if (ContainsAnswerToken(name, "C"))
            return 2;

        if (ContainsAnswerToken(name, "D"))
            return 3;

        return fallbackIndex;
    }

    private static bool ContainsAnswerToken(string value, string token)
    {
        return value.Equals(token, StringComparison.OrdinalIgnoreCase)
            || value.IndexOf("Btn_" + token, StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("Button_" + token, StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("Option_" + token, StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("Answer_" + token, StringComparison.OrdinalIgnoreCase) >= 0;
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
            Debug.LogWarning("[Part3] Failed to parse backend json: " + e.Message);
        }

        return string.Empty;
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

    private static void SetNamedChildrenVisible(Transform parent, string childName, bool visible)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                child.gameObject.SetActive(visible);

            SetNamedChildrenVisible(child, childName, visible);
        }
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






