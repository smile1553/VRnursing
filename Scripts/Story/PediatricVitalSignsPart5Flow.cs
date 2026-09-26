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
        CheckToyFever,
        LetYayaMeasureToy,
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
    [SerializeField] private int yayaRefuseToyLineIndex = 16;
    [SerializeField] private int yayaRefuseAgainLineIndex = 17;
    [SerializeField] private int yayaComfortToyLineIndex = 18;
    [SerializeField] private int yayaMomHugLineIndex = 19;
    [SerializeField] private int momEncourageEarLineIndex = 20;
    [SerializeField] private int yayaChooseEarLineIndex = 21;

    [Header("Actors")]
    [SerializeField] private MomAnimationPlayer momAnimation;
    [SerializeField] private YayaAnimationPlayer yayaAnimation;

    [Header("Prompt Text")]
    [SerializeField] private TMP_Text nursePromptText;

    [Header("Backend")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool advanceFromBackendJson = true;
    [SerializeField] private bool ignoreFirstBackendJson = true;
    [SerializeField] private string startToyRolePlayKeywords = "佩佩豬|玩偶|量體溫";
    [SerializeField] private string checkToyFeverKeywords = "發燒|出院|回家|體溫幾度|佩佩豬";
    [SerializeField] private string letYayaMeasureToyKeywords = "芽芽幫佩佩豬|幫佩佩豬量|幫忙佩佩豬|量體溫";
    [SerializeField] private string askYayaComfortToyKeywords = "很怕|怎麼辦|會不會痛|好怕|佩佩豬";
    [SerializeField] private string encourageYayaTemperatureKeywords = "勇敢|一樣勇敢|嗶|不會痛|幫你量";
    [SerializeField] private string askWhichEarKeywords = "哪一隻耳朵|哪隻耳朵|右耳|左耳|這個";

    [Header("Audio")]
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip yayaRefuseToyClip;
    [SerializeField] private AudioClip yayaRefuseAgainClip;
    [SerializeField] private AudioClip yayaComfortToyClip;
    [SerializeField] private AudioClip yayaMomHugClip;
    [SerializeField] private AudioClip momEncourageEarClip;
    [SerializeField] private AudioClip yayaChooseEarClip;
    [SerializeField] private bool useAudioLengthForDialogueDelay = true;
    [SerializeField] private float dialogueAdvanceDelay = 4f;
    [SerializeField] private float extraDelayAfterAudio = 0.4f;

    [Header("Options")]
    [SerializeField] private float subtitleDelay = 3f;
    [SerializeField] private float actionDelay = 2.5f;

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
    private const string NurseStartToyRolePlay =
        "護生：芽芽！我們來幫佩佩豬量體溫！";

    private const string TherapeuticPlaySubtitle =
        "讓病童和喜愛的玩偶以角色扮演方式進行治療性遊戲，讓病童配合測量。";

    private const string NurseCheckToyFever =
        "護生：我們看看佩佩豬有沒有發燒？沒有發燒才可以出院回家喔！來！我們看看佩佩豬體溫幾度？";

    private const string NurseLetYayaMeasureToy =
        "護生：好，芽芽不要量！來！芽芽幫佩佩豬量體溫，芽芽好棒喔！會幫忙佩佩豬量體溫喔！";

    private const string YayaMeasureToyAction =
        "芽芽拿著耳溫槍，對著佩佩豬做測量動作。";

    private const string NurseAskYayaComfortToy =
        "護生：芽芽，佩佩豬很怕量體溫耶，怎麼辦？你要跟她說甚麼？芽芽我好怕量體溫喔！量體溫會不會痛？";

    private const string NurseToyBraveLine =
        "護生：芽芽！我很勇敢，我讓你量體溫，你要輕輕的量喔！";

    private const string YayaFinishMeasureAction =
        "芽芽完成幫佩佩豬量體溫。";

    private const string NurseEncourageYayaTemperature =
        "護生：芽芽，你看！佩佩豬很勇敢耶，我覺得你和佩佩豬一樣勇敢耶，來，姐姐幫你量一下體溫，佩佩豬會說你很勇敢喔！對啊！芽芽很勇敢，你看我都不會痛耶！嗶一聲就好了！";

    private const string NurseAskWhichEar =
        "護生：芽芽要量哪一隻耳朵呢？";

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

        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
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
        NurseryRhymeMusicController.Play();
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

        ShowNursePrompt(NurseStartToyRolePlay, WaitingForNurseAction.StartToyRolePlay);
        yield return WaitForNurseActionOrSkip();

        ShowNursePrompt(TherapeuticPlaySubtitle, WaitingForNurseAction.None);
        yield return WaitForSecondsOrSkip(subtitleDelay);

        ShowDialogueLine(yayaRefuseToyLineIndex, yayaRefuseToyClip);
        yayaAnimation?.PlaySittingDisbelief();
        momAnimation?.PlayStandingIdle();
        yield return WaitForDialogue(yayaRefuseToyClip);

        ShowNursePrompt(NurseCheckToyFever, WaitingForNurseAction.CheckToyFever);
        yield return WaitForNurseActionOrSkip();

        ShowDialogueLine(yayaRefuseAgainLineIndex, yayaRefuseAgainClip);
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaRefuseAgainClip);

        ShowNursePrompt(NurseLetYayaMeasureToy, WaitingForNurseAction.LetYayaMeasureToy);
        yield return WaitForNurseActionOrSkip();

        ShowNursePrompt(YayaMeasureToyAction, WaitingForNurseAction.None);
        yayaAnimation?.PlaySittingRubbingArm();
        onToyTemperatureMeasure?.Invoke();
        yield return WaitForSecondsOrSkip(actionDelay);

        ShowNursePrompt(NurseAskYayaComfortToy, WaitingForNurseAction.AskYayaComfortToy);
        yield return WaitForNurseActionOrSkip();

        ShowDialogueLine(yayaComfortToyLineIndex, yayaComfortToyClip);
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForDialogue(yayaComfortToyClip);

        ShowNursePrompt(NurseToyBraveLine, WaitingForNurseAction.None);
        yield return WaitForSecondsOrSkip(dialogueAdvanceDelay);

        ShowNursePrompt(YayaFinishMeasureAction, WaitingForNurseAction.None);
        onToyTemperatureMeasure?.Invoke();
        yield return WaitForSecondsOrSkip(actionDelay);

        ShowNursePrompt(NurseEncourageYayaTemperature, WaitingForNurseAction.EncourageYayaTemperature);
        yield return WaitForNurseActionOrSkip();

        ShowDialogueLine(yayaMomHugLineIndex, yayaMomHugClip);
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaMomHugClip);

        ShowDialogueLine(momEncourageEarLineIndex, momEncourageEarClip);
        momAnimation?.PlayClapping();
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForDialogue(momEncourageEarClip);

        ShowNursePrompt(NurseAskWhichEar, WaitingForNurseAction.AskWhichEar);
        yield return WaitForNurseActionOrSkip();

        ShowDialogueLine(yayaChooseEarLineIndex, yayaChooseEarClip);
        yayaAnimation?.PlaySittingIdle();
        onYayaTemperatureMeasure?.Invoke();
        yield return WaitForDialogue(yayaChooseEarClip);

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
            case WaitingForNurseAction.CheckToyFever:
                return checkToyFeverKeywords;
            case WaitingForNurseAction.LetYayaMeasureToy:
                return letYayaMeasureToyKeywords;
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
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None || skipRequested);
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
        SetPanelVisible(dialoguePanel, true, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);

        if (dialogueManager != null)
        {
            dialogueManager.gameObject.SetActive(true);
            dialogueManager.StopPlayback();
            dialogueManager.ShowLine(lineIndex);
        }        else
        {
            Debug.LogWarning("[Part5] Dialogue manager is missing.", this);
        }

        PlayAudio(clip);
    }

    private void ShowQuiz()
    {
        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        WorldSpaceUiPlacer.PlaceCanvasInFrontOfCamera(quizPanel);
        WorldSpaceUiPlacer.MatchQuizPanelToQuizOne(quizPanel, quizPanelChildName);
        SetPanelVisible(quizPanel, true, quizPanelChildName);
        ApplyQuizText();
        BindQuizButtonsIfNeeded();
    }

    private void ApplyQuizText()
    {
        if (quizQuestionText != null)
            quizQuestionText.text = QuizQuestion;

        if (quizOptionTexts != null)
        {
            for (int i = 0; i < quizOptionTexts.Length && i < QuizOptions.Length; i++)
            {
                if (quizOptionTexts[i] != null)
                    quizOptionTexts[i].text = QuizOptions[i];
            }
        }

        if (quizQuestionText != null && quizOptionTexts != null && quizOptionTexts.Length >= QuizOptions.Length)
            return;

        Transform root = quizPanel != null ? FindDeepChild(quizPanel.transform, quizPanelChildName) ?? quizPanel.transform : null;
        if (root == null)
            return;

        TMP_Text[] foundTexts = root.GetComponentsInChildren<TMP_Text>(true);
        List<TMP_Text> visibleTexts = new List<TMP_Text>();

        foreach (TMP_Text text in foundTexts)
        {
            if (text == null || ShouldIgnoreQuizButton(text.gameObject.name))
                continue;

            visibleTexts.Add(text);
        }

        visibleTexts.Sort(CompareTextsByScreenOrder);
        if (visibleTexts.Count > 0 && quizQuestionText == null)
            visibleTexts[0].text = QuizQuestion;

        for (int i = 0; i < QuizOptions.Length; i++)
        {
            if (quizOptionTexts != null && i < quizOptionTexts.Length && quizOptionTexts[i] != null)
                continue;

            int textIndex = i + 1;
            if (textIndex < visibleTexts.Count)
                visibleTexts[textIndex].text = QuizOptions[i];
        }
    }

    private void SelectAnswer(int index)
    {
        bool correct = index == correctAnswerIndex;
        Debug.Log(correct ? CorrectFeedback : WrongFeedback, this);

        if (correct)
        {
            if (hideQuizAfterCorrect)
                SetPanelVisible(quizPanel, false, quizPanelChildName);

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
