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
    [SerializeField] private bool playStickerRewardAnimation = true;

    [Header("Prompt Text")]
    [SerializeField] private TMP_Text nursePromptText;

    [Header("Backend")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool advanceFromBackendJson = true;
    [SerializeField] private string reassureBeforeApexPulseKeywords = "銝?|憟賭?|慦賢直?賢?|憪??罵銝銝?";
    [SerializeField] private string giveStickerBeforeEarTempKeywords = "憟賣?|鞎潛?|?單澈|?單澈";
    [SerializeField] private string explainEarTemperatureKeywords = "??皞咽撟曉漲|?芯??餉單|?單";
    [SerializeField] private string explainFearToMomKeywords = "瘝?靽??|銝??敹?|皞Ⅱ";
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
    [SerializeField] private float yayaHugToyDelay = 2f;

    [Header("Quiz")]
    [SerializeField] private bool bindQuizButtonsAutomatically = true;
    [SerializeField] private int expectedQuizButtonCount = 3;
    [SerializeField] private int correctAnswerIndex = 1;
    [SerializeField] private bool hideQuizAfterCorrect = false;
    [SerializeField] private TMP_Text quizQuestionText;
    [SerializeField] private TMP_Text[] quizOptionTexts;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectAnswer;
    [SerializeField] private UnityEvent onWrongAnswer;

    [Header("Flow Link")]
    [SerializeField] private PediatricVitalSignsPart5Flow nextPartFlow;
    [SerializeField] private bool startNextPartAfterCorrect = false;

    private Coroutine routine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private string initialBackendSpeechToIgnore;
    private int lastStartFrame = -1;
    private bool quizButtonsBound;
    private const string NurseReassureBeforeApexPulse =
        "請引導護生說：芽芽不痛喔！芽芽好乖喔！媽媽聽完換姊姊聽聽喔！一下子就好！";

    private const string ApexPulseAction =
        "媽媽扶著聽診器，由護生聽心尖脈並計算心跳次數。";

    private const string NurseGiveStickerBeforeEarTemp =
        "請引導護生說：芽芽好棒喔！先給你一張貼紙！再來要量耳溫喔！";

    private const string NurseExplainEarTemperature =
        "請引導護生說：來！姊姊幫你量體溫，等一下給你看幾度喔！你要讓姊姊量哪一隻耳朵呢？";

    private const string NurseExplainFearToMom =
        "請引導護生說：媽媽沒關係！芽芽生病不舒服，會比較怕我們，我再跟他玩一下，讓他心情好一點，量出來的體溫、心跳、呼吸、血壓也會比較準確喔！";

    private const string QuizQuestion =
        "考題4：請問護生要使用甚麼方法以減少芽芽的害怕？";

    private static readonly string[] QuizOptions =
    {
        "A. 芽芽和護生角色扮演",
        "B. 芽芽和玩偶角色扮演",
        "C. 護生和媽媽角色扮演"
    };

    private const string CorrectFeedback = "答對了！";
    private const string WrongFeedback = "再想一下，哪一種方法最能減少芽芽的害怕？";
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

    private IEnumerator Part4Routine()
    {
        ShowNursePrompt(NurseReassureBeforeApexPulse, WaitingForNurseAction.ReassureBeforeApexPulse);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

        ShowNursePrompt(ApexPulseAction, WaitingForNurseAction.None);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingIdle();
        yield return new WaitForSeconds(Mathf.Max(0.1f, measurementActionDelay));

        ShowNursePrompt(NurseGiveStickerBeforeEarTemp, WaitingForNurseAction.GiveStickerBeforeEarTemp);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);
        yield return PlayStickerRewardIfNeeded();

        ShowDialogueLine(momEncourageLineIndex, momEncourageClip);
        momAnimation?.PlayClapping();
        yayaAnimation?.PlaySittingIdle();
        yield return WaitForDialogue(momEncourageClip);

        ShowNursePrompt(NurseExplainEarTemperature, WaitingForNurseAction.ExplainEarTemperature);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

        ShowDialogueLine(yayaRefuseLineIndex, yayaRefuseClip);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaRefuseClip);

        ShowDialogueLine(momScoldLineIndex, momScoldClip);
        momAnimation?.PlayAngry();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(momScoldClip);

        ShowNursePrompt(NurseExplainFearToMom, WaitingForNurseAction.ExplainFearToMom);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

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

        if (stickerRewardAnimator == null)
            yield break;

        yield return stickerRewardAnimator.PlayAndWait();
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
        SetPanelVisible(dialoguePanel, true, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);

        if (dialogueManager != null)
        {
            dialogueManager.gameObject.SetActive(true);
            dialogueManager.StopPlayback();
            dialogueManager.ShowLine(lineIndex);
        }        else
        {
            Debug.LogWarning("[Part4] Dialogue manager is missing.", this);
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

            StartNextPartFlow();
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

    private void StartNextPartFlow()
    {
        if (!startNextPartAfterCorrect)
            return;

        if (nextPartFlow == null)
            nextPartFlow = FindObjectOfType<PediatricVitalSignsPart5Flow>(true);

        if (nextPartFlow == null)
        {
            Debug.LogWarning("[Part4] Part5 flow was not found. Please add PediatricVitalSignsPart5Flow to the scene.", this);
            return;
        }

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
            int capturedIndex = i;
            buttons[i].onClick.AddListener(() => SelectAnswer(capturedIndex));
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
