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
        LetYayaListenMom,
        TellMomListenYaya
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
    [SerializeField] private int momStickerLineIndex = 6;
    [SerializeField] private int momEncourageLineIndex = 7;
    [SerializeField] private int yayaRefuseLineIndex = 8;
    [SerializeField] private int momDoctorLineIndex = 9;
    [SerializeField] private int momHeartbeatLineIndex = 10;
    [SerializeField] private int yayaEarPainLineIndex = 11;

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
    [SerializeField] private string letMomListenYayaKeywords = "媽媽|芽芽|心跳|聽診器|撲通";
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
    [SerializeField] private AudioClip nurseLetMomListenYayaClip;
    [SerializeField] private bool useAudioLengthForDialogueDelay = true;
    [SerializeField] private float dialogueAdvanceDelay = 4f;
    [SerializeField] private float extraDelayAfterAudio = 0.4f;

    [Header("Quiz")]
    [SerializeField] private bool bindQuizButtonsAutomatically = true;
    [SerializeField] private int expectedQuizButtonCount = 4;
    [SerializeField] private int correctAnswerIndex = 1;
    [SerializeField] private bool hideQuizAfterCorrect = true;
    [SerializeField] private float correctAnswerDelay = 1.2f;
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
    private Coroutine correctRoutine;
    private const string AskPreferencePrompt =
        "護生如何知道芽芽喜歡什麼東西呢？";

    private const string ReassureWithStickerPrompt =
        "請引導護生說：等一下量完，姊姊再給你這個貼紙喔！";

    private const string StickerStrategyPrompt =
        "字幕：以貼紙，鼓勵病童的正向行為。";

    private const string LetYayaListenMomPrompt =
        "請引導芽芽先聽媽媽的心跳。";

    private const string NurseLetYayaListenMomLine =
        "請引導護生說：好！那你先聽媽媽的心跳，有撲通、撲通的聲音喔！";

    private const string NurseLetMomListenYayaLine =
        "請引導護生說：那換媽媽聽聽芽芽的心跳聲音，撲通撲通！";

    private const string MomLikesStickerLine =
        "她喜歡貼紙！";

    private const string MomEncourageLine =
        "哇！好棒喔！有貼紙耶！來！我們讓姊姊聽聽！";

    private const string YayaRefuseLine =
        "我不要！不要！";

    private const string MomDoctorLine =
        "芽芽當醫生聽媽媽的心跳，來，聽診器給你！";

    private const string MomHeartbeatLine =
        "聽到撲通撲通的聲音！";

    private const string YayaEarPainLine =
        "耳朵痛痛！";

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
        Debug.Log("[Part3] StartPart3: starting sticker distraction flow.", this);
        routine = StartCoroutine(Part3Routine());
    }

    public void DebugSkipCurrentNurseCheck()
    {
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

    private IEnumerator Part3Routine()
    {
        ShowNursePrompt(AskPreferencePrompt, WaitingForNurseAction.AskMomPreference);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

        ShowDialogueText("媽媽", MomLikesStickerLine, momStickerClip);
        momAnimation?.PlayTalking();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(momStickerClip);

        visualDemo?.ShowSticker();
        ShowNursePrompt(ReassureWithStickerPrompt, WaitingForNurseAction.ReassureWithSticker);
        PlayAudio(nursePromiseStickerClip);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

        ShowNursePrompt(StickerStrategyPrompt, WaitingForNurseAction.None);
        visualDemo?.HighlightSticker();
        yield return new WaitForSeconds(2f);

        ShowDialogueText("媽媽", MomEncourageLine, momEncourageClip);
        // Mom celebrates with the configured clapping animation, then returns to talking.
        momAnimation?.PlayClapping();
        yield return WaitForDialogue(momEncourageClip);
        momAnimation?.PlayTalking();

        ShowDialogueText("芽芽", YayaRefuseLine, yayaRefuseClip);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaRefuseClip);

        visualDemo?.HighlightStethoscope();
        ShowNursePrompt(NurseLetYayaListenMomLine, WaitingForNurseAction.LetYayaListenMom);
        PlayAudio(nurseLetYayaListenMomClip);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

        ShowDialogueText("媽媽", MomDoctorLine, momDoctorClip);
        momAnimation?.PlayPointing();
        yield return WaitForDialogue(momDoctorClip);

        yayaAnimation?.PlaySittingIdle();
        if (visualDemo != null)
        {
            yield return visualDemo.MoveStethoscopeForYayaListeningMom();
            yield return visualDemo.PlayHeartbeat();
        }
        else
        {
            yield return new WaitForSeconds(2f);
        }

        ShowDialogueText("媽媽", MomHeartbeatLine, momHeartbeatClip);
        momAnimation?.PlayTalking();
        yield return WaitForDialogue(momHeartbeatClip);

        ShowDialogueText("芽芽", YayaEarPainLine, yayaEarPainClip);
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaEarPainClip);

        ShowNursePrompt(NurseLetMomListenYayaLine, WaitingForNurseAction.TellMomListenYaya);
        PlayAudio(nurseLetMomListenYayaClip);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

        momAnimation?.PlayTalking();
        yayaAnimation?.PlaySittingIdle();
        if (visualDemo != null)
            yield return visualDemo.MoveStethoscopeForMomListeningYaya();
        else
            yield return new WaitForSeconds(2f);

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
            case WaitingForNurseAction.TellMomListenYaya:
                return letMomListenYayaKeywords;
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
        {
            nursePromptText.enableWordWrapping = true;
            nursePromptText.enableAutoSizing = true;
            nursePromptText.fontSizeMin = Mathf.Min(nursePromptText.fontSizeMin, 18f);
            nursePromptText.fontSizeMax = Mathf.Max(nursePromptText.fontSize, nursePromptText.fontSizeMax);
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
        }        else
        {
            Debug.LogWarning("[Part3] Dialogue manager is missing.", this);
        }

        PlayAudio(clip);
    }

    private void ShowDialogueText(string speakerName, string content, AudioClip clip)
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
            dialogueManager.ShowText(speakerName, content);
        }
        else
        {
            if (speakerText != null)
            {
                TryAddGlyphs(speakerText, speakerName);
                speakerText.text = speakerName;
            }

            if (dialogueText != null)
            {
                TryAddGlyphs(dialogueText, content);
                dialogueText.text = content;
            }
        }

        PlayAudio(clip);
    }

    private void ShowQuiz()
    {
        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetAllSceneObjectsNamedVisible("SkipVoice_Button", false);
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
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(wrongPopup, true, "Wrong_Popup");
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
        SetPromptSkipButtonsVisible(false);
    }

    private IEnumerator InvokeCorrectAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, correctAnswerDelay));
        correctRoutine = null;
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(quizPanel, false, quizPanelChildName);
        onCorrectAnswer?.Invoke();
    }

    private void StopCorrectRoutine()
    {
        if (correctRoutine == null)
            return;

        StopCoroutine(correctRoutine);
        correctRoutine = null;
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

    private static void SetAllSceneObjectsNamedVisible(string objectName, bool visible)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject candidate in allObjects)
        {
            if (candidate == null || candidate.name != objectName || !candidate.scene.IsValid())
                continue;

            candidate.SetActive(visible);
        }
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
