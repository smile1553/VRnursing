using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PediatricVitalSignsPart5Flow : MonoBehaviour
{
    private enum WaitingForNurseAction
    {
        None,
        StartToyRolePlay,
        AskYayaComfortToy,
        EncourageYayaTemperature,
        AskWhichEar
    }

    [Header("Panels")]
    [SerializeField] private GameObject nursePromptPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private string promptPanelName = "P5Instruction_Canvas";
    [SerializeField] private string promptTextChildNames = "Prompt_Text|Instruction_Text|NursePromptText|Text (TMP)|Text";
    [SerializeField] private string dialoguePanelChildName = "Dialogue_Panel";
    [SerializeField] private string quizPanelChildName = "Quiz_Panel_5";

    [Header("Dialogue")]
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private int yayaRefuseToyLineIndex = 17;
    [SerializeField] private int yayaRefuseAgainLineIndex = 18;
    [SerializeField] private int yayaComfortToyLineIndex = 19;
    [SerializeField] private int yayaComfortToyFeelingLineIndex = 20;
    [SerializeField] private int yayaMomHugLineIndex = 21;
    [SerializeField] private int momEncourageEarLineIndex = 22;
    [SerializeField] private int momEncourageEarSecondLineIndex = 23;
    [SerializeField] private int momAskWhichEarLineIndex = 24;
    [SerializeField] private int yayaChooseEarLineIndex = 25;

    [Header("Actors")]
    [SerializeField] private MomAnimationPlayer momAnimation;
    [SerializeField] private YayaAnimationPlayer yayaAnimation;
    [SerializeField] private Part3VisualDemoController part3VisualDemo;

    [Header("Prompt Text")]
    [SerializeField] private TMP_Text nursePromptText;

    [Header("Backend")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool advanceFromBackendJson = true;
    [SerializeField] private bool ignoreFirstBackendJson = true;
    [SerializeField] private string startToyRolePlayKeywords = "我們來幫熊熊量體溫|熊熊量體溫|幫熊熊量|玩偶|量體溫";
    [SerializeField] private string askYayaComfortToyKeywords = "小熊|很怕|怎麼辦|會不會痛|好怕|不會痛|冰冰";
    [SerializeField] private string encourageYayaTemperatureKeywords = "勇敢|一樣勇敢|嗶|不會痛|幫你量";
    [SerializeField] private string askWhichEarKeywords = "哪一隻耳朵|哪隻耳朵|右耳|左耳|這個";

    [Header("Audio")]
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip yayaRefuseToyClip;
    [SerializeField] private AudioClip yayaRefuseAgainClip;
    [SerializeField] private AudioClip yayaComfortToyClip;
    [SerializeField] private AudioClip yayaComfortToyFeelingClip;
    [SerializeField] private AudioClip yayaMomHugClip;
    [SerializeField] private AudioClip momEncourageEarClip;
    [SerializeField] private AudioClip momEncourageEarSecondClip;
    [SerializeField] private AudioClip momAskWhichEarClip;
    [SerializeField] private AudioClip yayaChooseEarClip;
    [SerializeField] private bool useAudioLengthForDialogueDelay = true;
    [SerializeField] private float dialogueAdvanceDelay = 4f;
    [SerializeField] private float extraDelayAfterAudio = 0.4f;

    [Header("Options")]
    [SerializeField] private float subtitleDelay = 3f;
    [SerializeField] private float actionDelay = 2.5f;

    [Header("Next Part")]
    [SerializeField] private bool startPart6AfterQuiz = true;

    [Header("Quiz")]
    [SerializeField] private bool bindQuizButtonsAutomatically = true;
    [SerializeField] private int expectedQuizButtonCount = 4;
    [SerializeField] private int correctAnswerIndex = 0;
    [SerializeField] private bool hideQuizAfterCorrect = false;
    [SerializeField] private TMP_Text quizQuestionText;
    [SerializeField] private TMP_Text[] quizOptionTexts;

    [Header("Events")]
    [SerializeField] private UnityEvent onToyTemperatureMeasure;
    [SerializeField] private UnityEvent onYayaTemperatureMeasure;
    [SerializeField] private UnityEvent onCorrectAnswer;
    [SerializeField] private UnityEvent onWrongAnswer;

    private Coroutine routine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private string initialBackendSpeechToIgnore;
    private int lastStartFrame = -1;
    private bool quizButtonsBound;
    private bool skipRequested;
    private Button promptSkipButton;
    private Coroutine quizCompletionRoutine;
    private const string NurseStartToyRolePlay =
        "請用角色扮演的方式邀請芽芽一起幫熊熊量體溫。";

    private const string TherapeuticPlaySubtitle =
        "讓病童和喜愛的玩偶以角色扮演方式進行治療性遊戲，讓病童配合測量。";

    private const string NurseAskYayaComfortToy =
        "請用孩子聽得懂的方式引導芽芽安撫小熊，讓芽芽用自己的話告訴小熊耳溫測量的感覺。";

    private const string NurseEncourageYayaTemperature =
        "請用小熊完成測量後的勇敢經驗鼓勵芽芽，邀請芽芽也試著量耳溫，並提醒她過程很快、嗶一聲就好。";

    private const string NurseAskWhichEar =
        "請詢問芽芽想先量哪一隻耳朵，讓她用指出耳朵的方式做選擇。";

    private const string QuizQuestion =
        "考題5：芽芽兩歲半，請問量耳溫時，該如何讓耳溫槍進入耳道？";

    private static readonly string[] QuizOptions =
    {
        "A. 耳朵要向下向後拉",
        "B. 耳朵要向上向後拉",
        "C. 耳朵要向下向前拉",
        "D. 耳朵要向上向前拉"
    };

    private const string CorrectFeedback = "答對了！";
    private const string WrongFeedback = "再想一下，兩歲半幼兒量耳溫時耳朵方向要怎麼拉？";
    private void Awake()
    {
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();
        if (part3VisualDemo == null)
            part3VisualDemo = FindObjectOfType<Part3VisualDemoController>(true);

        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);
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
        if (part3VisualDemo == null)
            part3VisualDemo = FindObjectOfType<Part3VisualDemoController>(true);

        if (network == null || string.IsNullOrWhiteSpace(network.LastJson))
            return;

        HandleBackendJson(network.LastJson);
    }

    public void StartPart5()
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
        part3VisualDemo?.RestoreStickerAfterQuiz();
        NurseryRhymeMusicController.Stop();
        Debug.Log("[Part5] StartPart5: starting ear temperature role-play flow.", this);
        routine = StartCoroutine(Part5Routine());
    }

    public void DebugSkipCurrentNurseCheck()
    {
        skipRequested = true;
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

    private IEnumerator Part5Routine()
    {
        yayaAnimation?.PlaySittingIdle();
        momAnimation?.PlayStandingIdle();

        ShowNursePrompt(TherapeuticPlaySubtitle, WaitingForNurseAction.StartToyRolePlay);
        yield return WaitForNurseActionOrSkip();

        ShowDialogueLine(yayaRefuseToyLineIndex, yayaRefuseToyClip);
        yayaAnimation?.PlaySittingDisbelief();
        momAnimation?.PlayStandingIdle();
        yield return WaitForDialogue(yayaRefuseToyClip);

        ShowDialogueLine(yayaRefuseAgainLineIndex, yayaRefuseAgainClip);
        yayaAnimation?.PlaySittingDisbelief();
        momAnimation?.PlayStandingIdle();
        yield return WaitForDialogue(yayaRefuseAgainClip);

        ShowNursePrompt(NurseAskYayaComfortToy, WaitingForNurseAction.AskYayaComfortToy);
        yayaAnimation?.PlaySittingIdle();
        momAnimation?.PlayStandingIdle();
        yield return WaitForNurseActionOrSkip();

        ShowDialogueLine(yayaComfortToyLineIndex, yayaComfortToyClip);
        yayaAnimation?.PlayHugBearWithThermometer();
        momAnimation?.PlayStandingIdle();
        yield return WaitForDialogue(yayaComfortToyClip);

        ShowDialogueLine(yayaComfortToyFeelingLineIndex, yayaComfortToyFeelingClip);
        yayaAnimation?.PlayHugBearWithThermometer();
        momAnimation?.PlayStandingIdle();
        yield return WaitForDialogue(yayaComfortToyFeelingClip);

        yayaAnimation?.PlayKidCombine();
        onToyTemperatureMeasure?.Invoke();
        // Short beat after the comfort-toy line so the next prompt follows quickly.
        yield return new WaitForSeconds(Mathf.Clamp(actionDelay, 0.1f, 0.6f));

        ShowNursePrompt(NurseEncourageYayaTemperature, WaitingForNurseAction.EncourageYayaTemperature);
        momAnimation?.PlayStandingIdle();
        yield return WaitForNurseActionOrSkip();

        ShowDialogueLine(yayaMomHugLineIndex, yayaMomHugClip);
        yayaAnimation?.PlaySittingDisbelief();
        momAnimation?.PlayComfortToward(yayaAnimation != null ? yayaAnimation.transform : null);
        yield return WaitForDialogue(yayaMomHugClip);

        ShowDialogueLine(momEncourageEarLineIndex, momEncourageEarClip);
        momAnimation?.PlayClapping();
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForDialogue(momEncourageEarClip);

        ShowDialogueLine(momEncourageEarSecondLineIndex, momEncourageEarSecondClip);
        momAnimation?.PlayClapping();
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForDialogue(momEncourageEarSecondClip);
        momAnimation?.PlayStandingIdle();

        ShowNursePrompt(NurseAskWhichEar, WaitingForNurseAction.AskWhichEar);
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForNurseActionOrSkip();

        ShowDialogueLine(momAskWhichEarLineIndex, momAskWhichEarClip);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForDialogue(momAskWhichEarClip);

        ShowDialogueLine(yayaChooseEarLineIndex, yayaChooseEarClip);
        float pointEarDelay = GetDialogueDelay(yayaChooseEarClip);
        // Yaya stays as she is and just lifts a hand to her ear (no separate performance model).
        yayaAnimation?.PlayKidPointEar();
        onYayaTemperatureMeasure?.Invoke();
        yield return new WaitForSeconds(pointEarDelay);

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
            // Yaya is in meltdown (red): the nurse must calm her before the story moves on.
            if (KidEmotionGate.Blocking)
            {
                Debug.Log("[Part5] Keywords matched, but Yaya must be calmed first.", this);
                return;
            }

            Debug.Log($"[Part5] Backend matched {waitingForNurseAction}. text={speechText}", this);
            waitingForNurseAction = WaitingForNurseAction.None;
            return;
        }

        Debug.Log($"[Part5] Waiting for {waitingForNurseAction}, speech did not match. text={speechText}", this);
    }

    private string GetKeywords(WaitingForNurseAction action)
    {
        switch (action)
        {
            case WaitingForNurseAction.StartToyRolePlay:
                return startToyRolePlayKeywords;
            case WaitingForNurseAction.AskYayaComfortToy:
                return askYayaComfortToyKeywords;
            case WaitingForNurseAction.EncourageYayaTemperature:
                return encourageYayaTemperatureKeywords;
            case WaitingForNurseAction.AskWhichEar:
                return askWhichEarKeywords;
            default:
                return string.Empty;
        }
    }

    private IEnumerator WaitForNurseActionOrSkip()
    {
        skipRequested = false;
        // The sticker can only be given when a prompt asks for it; this one does not.
        bool allowStickerSelection = false;
        if (allowStickerSelection && part3VisualDemo != null)
        {
            part3VisualDemo.ResetHudStickerSelection();
            part3VisualDemo.ShowSticker();
            part3VisualDemo.HighlightSticker();
        }

        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None
            || skipRequested
            || (allowStickerSelection && part3VisualDemo != null && part3VisualDemo.HudStickerWasSelected));

        waitingForNurseAction = WaitingForNurseAction.None;
        skipRequested = false;
    }
    private IEnumerator WaitForSecondsOrSkip(float seconds)
    {
        skipRequested = false;
        float endTime = Time.time + Mathf.Max(0.1f, seconds);
        while (Time.time < endTime && !skipRequested)
            yield return null;

        skipRequested = false;
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
            Debug.LogWarning("[Part5] Nurse prompt text is missing. Add P5Instruction_Canvas later and assign its prompt text.", this);

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
            Debug.LogWarning("[Part5] Dialogue manager is missing.", this);
        }

        PlayAudio(clip);
    }


    private void ShowDialogueText(string speaker, string content, AudioClip clip)
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
            dialogueManager.ShowText(speaker, content);
        }
        else
        {
            if (speakerText != null)
                speakerText.text = speaker;
            if (dialogueText != null)
                dialogueText.text = content;
        }

        PlayAudio(clip);
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
        StopQuizCompletionRoutine();
        quizCompletionRoutine = StartCoroutine(WaitForQuizPanelClosedThenRestoreSticker());
    }


    private IEnumerator WaitForQuizPanelClosedThenRestoreSticker()
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

        if (startPart6AfterQuiz)
        {
            Debug.Log("[Part5] Quiz 5 closed: starting Part6.", this);
            PediatricVitalSignsPart6Flow.FindOrCreate().StartPart6();
        }
    }

    private void StopQuizCompletionRoutine()
    {
        if (quizCompletionRoutine == null)
            return;

        StopCoroutine(quizCompletionRoutine);
        quizCompletionRoutine = null;
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

    private void SelectAnswer(int index)
    {
        bool correct = index == correctAnswerIndex;
        Debug.Log(correct ? CorrectFeedback : WrongFeedback, this);

        if (correct)
        {
            if (hideQuizAfterCorrect)
            {
                SetPanelVisible(quizPanel, false, quizPanelChildName);
                QuizPanelRuntimeHelper.EndQuiz();
            }

            part3VisualDemo?.RestoreStickerAfterQuiz();
            onCorrectAnswer?.Invoke();
            return;
        }

        onWrongAnswer?.Invoke();
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
        // Use the audible part of the clip (trailing silence trimmed) so lines follow each other
        // right after the voice ends instead of pausing.
        if (useAudioLengthForDialogueDelay && clip != null)
            return Mathf.Max(0.1f, AudioClipTrim.GetAudibleLength(clip) + Mathf.Min(extraDelayAfterAudio, 0.2f));

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

        SetPromptSkipButtonsVisible(false);
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
            int capturedIndex = i;
            buttons[i].onClick.AddListener(() => SelectAnswer(capturedIndex));
        }

        quizButtonsBound = bindCount > 0;
        Debug.Log($"[Part5] Auto-bound quiz buttons: {bindCount}/{buttons.Count}", this);
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
        if (part3VisualDemo == null)
            part3VisualDemo = FindObjectOfType<Part3VisualDemoController>(true);

        if (network != null && ignoreFirstBackendJson && !string.IsNullOrWhiteSpace(network.LastJson))
        {
            lastProcessedBackendJson = network.LastJson;
            initialBackendSpeechToIgnore = ExtractBackendSpeechText(network.LastJson);
            Debug.Log("[Part5] Cached backend speech will be ignored: " + initialBackendSpeechToIgnore, this);
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
            Debug.LogWarning("[Part5] Failed to parse backend json: " + e.Message);
        }

        return string.Empty;
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
