using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PediatricVitalSignsPart2Flow : MonoBehaviour
{
    private enum WaitingForNurseAction
    {
        None,
        RespirationExplanation,
        HeartbeatExplanation
    }

    [Header("Panels")]
    [SerializeField] private GameObject nursePromptPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private bool clonePromptPanelWhenMissing = true;
    [SerializeField] private string promptTemplatePanelNames = "Part2_NursePrompt_Panel|Instruction_Canvas|TopHint_Panel";
    [SerializeField] private string promptTextChildNames = "Prompt_Text|Instruction_Text|NursePromptText|Text (TMP)|Text";

    [Header("Dialogue")]
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private int momApologyLineIndex = 2;
    [SerializeField] private int momLayDownLineIndex = 3;
    [SerializeField] private int momBesideLineIndex = 4;
    [SerializeField] private int yayaRefuseLineIndex = 5;

    [Header("Actors")]
    [SerializeField] private MomAnimationPlayer momAnimation;
    [SerializeField] private YayaAnimationPlayer yayaAnimation;

    [Header("Nurse Prompt Text")]
    [SerializeField] private TMP_Text nursePromptText;

    [Header("Backend")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool advanceFromBackendJson = true;
    [SerializeField] private string respirationKeywords = "\u89c0\u5bdf\u8d77\u4f0f|\u8a08\u7b97\u547c\u5438\u6b21\u6578|\u89c0\u5bdf|\u8d77\u4f0f|\u547c\u5438\u6b21\u6578";
    [SerializeField] private string heartbeatKeywords = "\u5fc3\u8df3|\u807d\u8a3a\u5668|\u4e0d\u6703\u75db|\u6478\u6478|\u807d\u807d";
    [SerializeField] private bool ignoreFirstBackendJson = true;

    [Header("Audio")]
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip momApologyClip;
    [SerializeField] private AudioClip momLayDownClip;
    [SerializeField] private AudioClip momBesideClip;
    [SerializeField] private AudioClip yayaRefuseClip;
    [SerializeField] private bool useAudioLengthForDialogueDelay = true;
    [SerializeField] private float dialogueAdvanceDelay = 4f;
    [SerializeField] private float extraDelayAfterAudio = 0.4f;

    [Header("Options")]
    [SerializeField] private float observationDelay = 60f;
    [SerializeField] private bool hideQuizAfterCorrect = false;
    [SerializeField] private float correctAnswerDelay = 1.2f;
    [SerializeField] private bool bindQuizButtonsAutomatically = true;
    [SerializeField] private int expectedQuizButtonCount = 3;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectAnswer;
    [SerializeField] private UnityEvent onWrongAnswer;

    [Header("Flow Link")]
    [SerializeField] private PediatricVitalSignsPart3Flow nextPartFlow;
    [SerializeField] private bool startNextPartAfterCorrect = true;

    [Header("Hidden Future Demo Objects")]
    [SerializeField] private string hiddenCombinedKidObjectNames = "Kid_Combined|Kid_Combined 1|Sitting Idle|Sitting Idle test|kidtakingbeartemp";
    [SerializeField] private bool hideCombinedKidDuringPart2 = true;

    private Coroutine routine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private string initialBackendSpeechToIgnore;
    private int lastStartFrame = -1;
    private bool quizButtonsBound;
    private bool skipRequested;
    private Coroutine correctRoutine;

    private const string RespirationPrompt =
        "\u8acb\u89c0\u5bdf\u82bd\u82bd\u80f8\u8179\u8d77\u4f0f\uff0c\u4e26\u8aaa\u660e\u8981\u8a08\u7b97\u547c\u5438\u6b21\u6578\u3002";

    private const string ObservationPrompt =
        "\u958b\u59cb\u6e2c\u91cf\u547c\u5438\u4e00\u5206\u9418\u3002";

    private const string HeartbeatPrompt =
        "請安撫芽芽，並說明接下來只是聽心跳、不會痛；可以引導芽芽摸摸聽診器，讓她知道一下子就好。";

    private const string CorrectFeedback =
        "\u7b54\u5c0d\u4e86\uff01";

    private const string WrongFeedback =
        "\u518d\u60f3\u4e00\u4e0b\uff0c\u54ea\u4e00\u7a2e\u65b9\u6cd5\u6700\u80fd\u964d\u4f4e\u82bd\u82bd\u7684\u5bb3\u6015\uff1f";

    private void Awake()
    {
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        ResolveDialogueManager();
        ResolveNursePromptPanel();
        SetCombinedKidVisible(false);
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_2");
    }

    private void OnEnable()
    {
        SetCombinedKidVisible(false);
        ResetBackendGate();
    }

    private void Update()
    {
        if (hideCombinedKidDuringPart2)
            SetCombinedKidVisible(false);

        if (!advanceFromBackendJson || waitingForNurseAction == WaitingForNurseAction.None)
            return;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (network == null || string.IsNullOrWhiteSpace(network.LastJson))
            return;

        HandleBackendJson(network.LastJson);
    }

    public void StartPart2()
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
        SetCombinedKidVisible(false);
        Debug.Log("[Part2] StartPart2: starting from mom apology.", this);
        routine = StartCoroutine(Part2Routine());
    }

    public void CompleteRespirationExplanation()
    {
        if (waitingForNurseAction == WaitingForNurseAction.RespirationExplanation)
            waitingForNurseAction = WaitingForNurseAction.None;
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

    private void HandleBackendJson(string json)
    {
        if (!advanceFromBackendJson || waitingForNurseAction == WaitingForNurseAction.None || string.IsNullOrWhiteSpace(json))
            return;

        if (string.Equals(json, lastProcessedBackendJson, StringComparison.Ordinal))
            return;

        lastProcessedBackendJson = json;
        string speechText = ExtractBackendSpeechText(json);
        if (string.IsNullOrWhiteSpace(speechText))
            return;

        string keywords = waitingForNurseAction == WaitingForNurseAction.HeartbeatExplanation
            ? heartbeatKeywords
            : respirationKeywords;

        if (ContainsAnyKeyword(speechText, keywords))
        {
            Debug.Log($"[Part2] Backend matched {waitingForNurseAction}. text={speechText}", this);
            waitingForNurseAction = WaitingForNurseAction.None;
            return;
        }

        Debug.Log($"[Part2] Waiting for {waitingForNurseAction}, speech did not match. text={speechText}", this);
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

    private void SelectByButtonIndex(int index)
    {
        Debug.Log($"[Part2] Quiz button clicked index={index}", this);
        SelectAnswer(index);
    }

    private IEnumerator Part2Routine()
    {
        ShowDialogueLine(momApologyLineIndex, momApologyClip);
        momAnimation?.PlayQuickBow();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(momApologyClip);

        ShowNursePrompt(RespirationPrompt, WaitingForNurseAction.RespirationExplanation);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);
        skipRequested = false;

        ShowDialogueLine(momLayDownLineIndex, momLayDownClip);
        momAnimation?.PlayPointing();
        yayaAnimation?.PlayLayingDown();
        yield return WaitForDialogue(momLayDownClip);

        ShowDialogueLine(momBesideLineIndex, momBesideClip);
        momAnimation?.PlayPointing();
        yayaAnimation?.PlayLayingSleeping();
        yield return WaitForDialogue(momBesideClip);

        ShowNursePrompt(ObservationPrompt, WaitingForNurseAction.None);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlayLayingSleeping();
        yield return WaitForSecondsOrSkip(Mathf.Max(60f, observationDelay));

        ShowNursePrompt(HeartbeatPrompt, WaitingForNurseAction.HeartbeatExplanation);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);
        skipRequested = false;

        ShowDialogueLine(yayaRefuseLineIndex, yayaRefuseClip);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlayKickingOut();
        yield return WaitForDialogue(yayaRefuseClip);

        ShowQuiz();
        routine = null;
    }

    private void ShowDialogueLine(int lineIndex, AudioClip clip)
    {
        ResolveDialogueManager();
        ResolveNursePromptPanel();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, true, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_2");

        if (dialogueManager != null)
        {
            dialogueManager.gameObject.SetActive(true);
            dialogueManager.StopPlayback();
            dialogueManager.ShowLine(lineIndex);
        }
        else
        {
            Debug.LogWarning("[Part2] Dialogue manager is missing.", this);
        }

        PlayAudio(clip);
    }

    private void ShowNursePrompt(string prompt, WaitingForNurseAction waitingAction)
    {
        ResolveNursePromptPanel();

        if (dialogueManager != null)
            dialogueManager.StopPlayback();

        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_2");
        SetPanelVisible(nursePromptPanel, true, "TopHint_Panel");
        SetPromptSkipButtonsVisible(true);

        if (nursePromptText != null)
            nursePromptText.text = prompt;
        else
            Debug.LogWarning("[Part2] Nurse prompt text is missing.", this);

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        waitingForNurseAction = waitingAction;
    }

    private IEnumerator WaitForDialogue(AudioClip clip)
    {
        yield return new WaitForSeconds(GetDialogueDelay(clip));
    }

    private IEnumerator WaitForSecondsOrSkip(float seconds)
    {
        skipRequested = false;
        float endTime = Time.time + Mathf.Max(0.1f, seconds);
        while (Time.time < endTime && !skipRequested)
            yield return null;

        skipRequested = false;
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

    private void ShowQuiz()
    {
        ResolveNursePromptPanel();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        WorldSpaceUiPlacer.PlaceCanvasInFrontOfCamera(quizPanel);
        WorldSpaceUiPlacer.MatchQuizPanelToQuizOne(quizPanel, "Quiz_Panel_2");
        SetPanelVisible(quizPanel, true, "Quiz_Panel_2");
        BindQuizButtonsIfNeeded();
        momAnimation?.PlayStandingIdle();
    }

    private void SelectAnswer(int index)
    {
        bool correct = index == 0;
        Debug.Log(correct ? CorrectFeedback : WrongFeedback, this);

        if (correct)
        {
            if (hideQuizAfterCorrect)
                SetPanelVisible(quizPanel, false, "Quiz_Panel_2");

            StopCorrectRoutine();
            correctRoutine = StartCoroutine(InvokeCorrectAfterDelay());
            return;
        }

        onWrongAnswer?.Invoke();
    }

    private void StartNextPartFlow()
    {
        if (!startNextPartAfterCorrect)
            return;

        if (nextPartFlow == null)
            nextPartFlow = FindObjectOfType<PediatricVitalSignsPart3Flow>(true);

        if (nextPartFlow == null)
        {
            Debug.LogWarning("[Part2] Part3 flow was not found. Please add PediatricVitalSignsPart3Flow to the scene.", this);
            return;
        }

        nextPartFlow.StartPart3();
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
    }

    private IEnumerator InvokeCorrectAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, correctAnswerDelay));
        correctRoutine = null;
        StartNextPartFlow();
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

        Transform root = FindDeepChild(quizPanel.transform, "Quiz_Panel_2") ?? quizPanel.transform;
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
            int capturedIndex = GetAnswerIndexFromButton(button, i);
            button.onClick.AddListener(() => SelectByButtonIndex(capturedIndex));
        }

        quizButtonsBound = bindCount > 0;
        Debug.Log($"[Part2] Auto-bound quiz buttons: {bindCount}/{buttons.Count}", this);
    }
    private static int GetAnswerIndexFromButton(Button button, int fallbackIndex)
    {
        if (button == null)
            return fallbackIndex;

        string buttonName = button.gameObject.name.ToUpperInvariant();
        if (buttonName.Contains("BTN_A") || buttonName.EndsWith("_A") || buttonName == "A")
            return 0;
        if (buttonName.Contains("BTN_B") || buttonName.EndsWith("_B") || buttonName == "B")
            return 1;
        if (buttonName.Contains("BTN_C") || buttonName.EndsWith("_C") || buttonName == "C")
            return 2;
        if (buttonName.Contains("BTN_D") || buttonName.EndsWith("_D") || buttonName == "D")
            return 3;

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text))
                continue;

            string value = text.text.Trim().ToUpperInvariant();
            if (value.Contains("(A)") || value.Contains("（A）") || value.StartsWith("A.") || value.StartsWith("A ") || value.StartsWith("A、"))
                return 0;
            if (value.Contains("(B)") || value.Contains("（B）") || value.StartsWith("B.") || value.StartsWith("B ") || value.StartsWith("B、"))
                return 1;
            if (value.Contains("(C)") || value.Contains("（C）") || value.StartsWith("C.") || value.StartsWith("C ") || value.StartsWith("C、"))
                return 2;
            if (value.Contains("(D)") || value.Contains("（D）") || value.StartsWith("D.") || value.StartsWith("D ") || value.StartsWith("D、"))
                return 3;
        }

        return fallbackIndex;
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
            Debug.Log("[Part2] Cached backend speech will be ignored: " + initialBackendSpeechToIgnore, this);
        }
    }

    private void ResolveDialogueManager()
    {
        if (dialogueManager != null)
            return;

        if (dialoguePanel != null)
            dialogueManager = dialoguePanel.GetComponentInChildren<NewDialogueManager>(true);
    }

    private void ResolveNursePromptPanel()
    {
        if (nursePromptPanel == null)
            nursePromptPanel = FindSceneObjectByName("Part2_NursePrompt_Panel");

        if (nursePromptPanel == null && clonePromptPanelWhenMissing)
        {
            GameObject template = FindPromptTemplate();
            if (template != null)
            {
                nursePromptPanel = Instantiate(template, template.transform.parent);
                nursePromptPanel.name = "Part2_NursePrompt_Panel";
                nursePromptPanel.SetActive(false);
                Debug.Log("[Part2] Created private nurse prompt panel from template: " + template.name, this);
            }
        }

        ResolveNursePromptText();
    }

    private GameObject FindPromptTemplate()
    {
        string[] names = promptTemplatePanelNames.Split('|');
        foreach (string rawName in names)
        {
            string panelName = rawName.Trim();
            if (panelName.Length == 0 || panelName == "Part2_NursePrompt_Panel")
                continue;

            GameObject found = FindSceneObjectByName(panelName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void ResolveNursePromptText()
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
            Debug.LogWarning("[Part2] Failed to parse backend json: " + e.Message);
        }

        return string.Empty;
    }

    private void SetPromptSkipButtonsVisible(bool visible)
    {
        if (nursePromptPanel == null)
            return;

        SetNamedChildrenVisible(nursePromptPanel.transform, "SkipVoice_Button", visible);
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

    [Serializable]
    private class BackendSpeechResponse
    {
        public string text;
        public string raw_text;
        public string llm_window_text;
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

    private void SetCombinedKidVisible(bool visible)
    {
        if (string.IsNullOrWhiteSpace(hiddenCombinedKidObjectNames))
            return;

        string[] objectNames = hiddenCombinedKidObjectNames.Split('|');
        foreach (string rawName in objectNames)
        {
            string objectName = rawName.Trim();
            if (string.IsNullOrEmpty(objectName))
                continue;

            GameObject objectToHide = FindSceneObjectByName(objectName);
            if (objectToHide != null && objectToHide.activeSelf != visible)
                objectToHide.SetActive(visible);
        }
    }
}
