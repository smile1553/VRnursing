using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class PediatricVitalSignsPart1Flow : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;

    [Header("Actors")]
    [SerializeField] private MomAnimationPlayer momAnimation;
    [SerializeField] private YayaAnimationPlayer yayaAnimation;

    [Header("Dialogue Manager")]
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private int momOkLineIndex = 0;
    [SerializeField] private int yayaRefuseLineIndex = 1;

    [Header("Backend Voice Gate")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool startDialogueFromBackendJson = true;
    [SerializeField] private string greetingKeywords = "";
    [SerializeField] private string measurementKeywords = "";
    [SerializeField] private float staleBackendIgnoreSeconds = 1.5f;
    [SerializeField] private bool ignoreFirstBackendJson = true;

    [Header("Startup Gate")]
    [SerializeField] private GameObject waitUntilPanelHidden;
    [SerializeField] private string autoFindBlockingPanelNames = "VitalSigns_Intro_Panel|MedicalRecord_Panel 1|Login_Panel";
    [SerializeField] private float delayAfterBlockingPanelHidden = 0.5f;

    [Header("Dialogue Text Fallback")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Dialogue Audio")]
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip momOkClip;
    [SerializeField] private AudioClip yayaNoInjectionClip;
    [SerializeField] private bool useAudioLengthForDialogueDelay = true;
    [SerializeField] private float extraDelayAfterAudio = 0.4f;

    [Header("Quiz Text")]
    [SerializeField] private TMP_Text quizQuestionText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private GameObject correctPopup;
    [SerializeField] private GameObject wrongPopup;

    [Header("Options")]
    [SerializeField] private bool autoAdvanceDialogue = true;
    [SerializeField] private float dialogueAdvanceDelay = 4f;
    [SerializeField] private float maxDialogueAutoAdvanceDelay = 8f;
    [SerializeField] private bool hideQuizAfterCorrect = false;
    [SerializeField] private float correctAnswerDelay = 1.2f;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectAnswer;
    [SerializeField] private UnityEvent onWrongAnswer;

    [Header("Flow Link")]
    [SerializeField] private PediatricVitalSignsPart2Flow nextPartFlow;
    [SerializeField] private bool startNextPartAfterCorrect = true;
    [SerializeField] private bool hidePart1UiWhenStartingNextPart = true;

    private int dialogueIndex = -1;
    private Coroutine dialogueRoutine;
    private Coroutine correctRoutine;
    private bool hasSkippedInitialBackendJson;
    private string initialBackendSpeechToIgnore;
    private float backendListenStartedAt;
    private string lastProcessedBackendJson;
    private bool startupGateReleased;
    private float startupGateReleaseAt;

    private static readonly string[] FallbackSpeakers =
    {
        "\u5abd\u5abd",
        "\u82bd\u82bd"
    };

    private static readonly string[] FallbackDialogues =
    {
        "\u597d\u7684\u9ebb\u7169\u59b3\u4e86",
        "\u6211\u4e0d\u8981\uff01\u6211\u4e0d\u8981\u6253\u91dd\uff01"
    };

    private const string CorrectFeedback = "\u7b54\u5c0d\u4e86\uff01";
    private const string WrongFeedback = "\u518d\u60f3\u4e00\u4e0b\uff0c\u54ea\u4e00\u9805\u6bd4\u8f03\u4e0d\u6703\u52a0\u91cd\u82bd\u82bd\u7684\u5bb3\u6015\uff1f";
    private static readonly string[] Part1GreetingKeywords =
    {
        "\u5abd\u5abd",
        "\u82bd\u82bd",
        "\u4f60\u5011\u597d",
        "\u4f60\u597d"
    };

    private static readonly string[] Part1MeasurementKeywords =
    {
        "\u91cf\u9ad4\u6eab",
        "\u9ad4\u6eab",
        "\u547c\u5438",
        "\u5fc3\u8df3",
        "\u8840\u58d3",
        "\u751f\u547d\u5fb5\u8c61",
        "\u6e2c\u91cf\u9805\u76ee"
    };

    private void Awake()
    {
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (nextPartFlow == null)
            nextPartFlow = FindObjectOfType<PediatricVitalSignsPart2Flow>(true);

        ResolveStartupGatePanel();

        ResolveDialogueManager();
        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_1");
        SetPanelVisible(correctPopup, false, "Correct_Popup");
        SetPanelVisible(wrongPopup, false, "Wrong_Popup");
        ClearFeedback();
    }

    private void OnEnable()
    {
        backendListenStartedAt = Time.time;
        hasSkippedInitialBackendJson = false;
        initialBackendSpeechToIgnore = null;
        lastProcessedBackendJson = null;
        startupGateReleased = false;
        startupGateReleaseAt = 0f;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        ResolveStartupGatePanel();
        TryReleaseStartupGate();
    }

    private void Update()
    {
        if (!startDialogueFromBackendJson || dialogueIndex >= 0)
            return;

        if (!TryReleaseStartupGate())
            return;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (network == null || string.IsNullOrWhiteSpace(network.LastJson))
            return;

        HandleBackendJson(network.LastJson);
    }

    public void EnablePart1VoiceGate()
    {
        startupGateReleased = true;
        startupGateReleaseAt = Time.time + Mathf.Max(0f, delayAfterBlockingPanelHidden);
        MarkCurrentBackendJsonAsSeen("Manual Part1 voice gate release");
    }

    public void SkipVoiceAndStartDialogue()
    {
        HideInstructionUI();

        dialogueIndex = 0;
        ShowDialogue();
    }

    private void HandleBackendJson(string json)
    {
        if (!startDialogueFromBackendJson || dialogueIndex >= 0 || string.IsNullOrWhiteSpace(json))
            return;

        if (string.Equals(json, lastProcessedBackendJson, StringComparison.Ordinal))
            return;

        lastProcessedBackendJson = json;
        string speechText = ExtractBackendSpeechText(json);
        if (string.IsNullOrWhiteSpace(speechText))
            return;

        if (ignoreFirstBackendJson && !hasSkippedInitialBackendJson)
        {
            hasSkippedInitialBackendJson = true;
            initialBackendSpeechToIgnore = speechText;
            Debug.Log("[Part1] First backend speech ignored after startup: " + initialBackendSpeechToIgnore, this);
            return;
        }

        Debug.Log("[Part1] Backend speech text: " + speechText, this);

        if (!hasSkippedInitialBackendJson)
            hasSkippedInitialBackendJson = true;

        bool hasGreeting = ContainsAnyKeyword(speechText, Part1GreetingKeywords) || ContainsAnyKeyword(speechText, greetingKeywords);
        bool hasMeasurement = ContainsAnyKeyword(speechText, Part1MeasurementKeywords) || ContainsAnyKeyword(speechText, measurementKeywords);
        if (hasGreeting && hasMeasurement)
        {
            SkipVoiceAndStartDialogue();
        }
        else
        {
            Debug.Log("[Part1] Speech received, but prompt keywords not matched. text=" + speechText, this);
        }
    }

    public void NextDialogue()
    {
        StopDialogueRoutine();
        dialogueIndex++;

        if (dialogueIndex >= FallbackDialogues.Length)
        {
            ShowQuiz();
            return;
        }

        ShowDialogue();
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

    private void ShowDialogue()
    {
        ResolveDialogueManager();
        SetPanelVisible(dialoguePanel, true, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_1");

        int lineIndex = dialogueIndex == 0 ? momOkLineIndex : yayaRefuseLineIndex;
        Debug.Log($"[Part1] Show dialogue index={dialogueIndex}, line={lineIndex}", this);

        if (dialogueManager != null)
        {
            dialogueManager.StopPlayback();
            dialogueManager.ShowLine(lineIndex);
        }
        else
        {
            ShowFallbackDialogue();
        }

        PlayDialogueAnimation(dialogueIndex);
        AudioClip clip = PlayDialogueAudio(dialogueIndex);
        ClearFeedback();

        if (autoAdvanceDialogue)
        {
            StopDialogueRoutine();
            float delay = GetDialogueDelay(clip);
            Debug.Log($"[Part1] Auto advance in {delay:0.00}s", this);
            dialogueRoutine = StartCoroutine(AdvanceDialogueAfterDelay(delay));
        }
    }

    private void ShowFallbackDialogue()
    {
        if (speakerText != null)
            speakerText.text = FallbackSpeakers[dialogueIndex];

        if (dialogueText != null)
            dialogueText.text = FallbackDialogues[dialogueIndex];
    }

    private void PlayDialogueAnimation(int index)
    {
        if (index == 0)
        {
            momAnimation?.PlayTalking();
            yayaAnimation?.PlaySittingRubbingArm();
            return;
        }

        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
    }

    private AudioClip PlayDialogueAudio(int index)
    {
        if (dialogueAudioSource == null)
            return null;

        AudioClip clip = index == 0 ? momOkClip : yayaNoInjectionClip;
        dialogueAudioSource.Stop();

        if (clip != null)
            dialogueAudioSource.PlayOneShot(clip);

        return clip;
    }

    private float GetDialogueDelay(AudioClip clip)
    {
        float delay = dialogueAdvanceDelay;
        if (useAudioLengthForDialogueDelay && clip != null)
            delay = clip.length + extraDelayAfterAudio;

        if (maxDialogueAutoAdvanceDelay > 0f)
            delay = Mathf.Min(delay, maxDialogueAutoAdvanceDelay);

        return Mathf.Max(0.1f, delay);
    }

    private void ShowQuiz()
    {
        Debug.Log("[Part1] Show quiz 1", this);
        StopDialogueRoutine();
        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        SetPanelVisible(quizPanel, true, "Quiz_Panel_1");

        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
        ClearFeedback();
    }

    private void SelectAnswer(int index)
    {
        bool correct = index == 3;

        if (feedbackText != null)
            feedbackText.text = correct ? CorrectFeedback : WrongFeedback;

        if (correct)
        {
            StopCorrectRoutine();
            SetPanelVisible(wrongPopup, false, "Wrong_Popup");
            SetPanelVisible(correctPopup, true, "Correct_Popup");

            if (hideQuizAfterCorrect)
                SetPanelVisible(quizPanel, false, "Quiz_Panel_1");

            correctRoutine = StartCoroutine(InvokeCorrectAfterDelay());
        }
        else
        {
            SetPanelVisible(correctPopup, false, "Correct_Popup");
            SetPanelVisible(wrongPopup, true, "Wrong_Popup");
            onWrongAnswer?.Invoke();
        }
    }

    private void ClearFeedback()
    {
        if (feedbackText != null)
            feedbackText.text = string.Empty;
    }

    private IEnumerator AdvanceDialogueAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        dialogueRoutine = null;
        NextDialogue();
    }

    private IEnumerator InvokeCorrectAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, correctAnswerDelay));
        correctRoutine = null;
        SetPanelVisible(correctPopup, false, "Correct_Popup");

        if (hidePart1UiWhenStartingNextPart)
        {
            SetPanelVisible(quizPanel, false, "Quiz_Panel_1");
            SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        }

        StartNextPartFlow();
        onCorrectAnswer?.Invoke();
    }

    private void StartNextPartFlow()
    {
        if (!startNextPartAfterCorrect)
            return;

        if (nextPartFlow == null)
            nextPartFlow = FindObjectOfType<PediatricVitalSignsPart2Flow>(true);

        if (nextPartFlow == null)
        {
            Debug.LogWarning("[Part1] Part2 flow was not found. Please add PediatricVitalSignsPart2Flow to the scene.", this);
            return;
        }

        nextPartFlow.StartPart2();
    }

    private void StopDialogueRoutine()
    {
        if (dialogueRoutine == null)
            return;

        StopCoroutine(dialogueRoutine);
        dialogueRoutine = null;
    }

    private void StopCorrectRoutine()
    {
        if (correctRoutine == null)
            return;

        StopCoroutine(correctRoutine);
        correctRoutine = null;
    }

    private void ResolveDialogueManager()
    {
        if (dialogueManager != null)
            return;

        if (dialoguePanel != null)
            dialogueManager = dialoguePanel.GetComponentInChildren<NewDialogueManager>(true);
    }

    private void ResolveStartupGatePanel()
    {
        if (waitUntilPanelHidden != null)
            return;

        string[] names = autoFindBlockingPanelNames.Split('|');
        foreach (string rawName in names)
        {
            string panelName = rawName.Trim();
            if (panelName.Length == 0)
                continue;

            GameObject found = GameObject.Find(panelName);
            if (found != null)
            {
                waitUntilPanelHidden = found;
                Debug.Log("[Part1] Voice gate waits for panel to hide: " + panelName, this);
                return;
            }
        }
    }

    private bool TryReleaseStartupGate()
    {
        if (startupGateReleased)
            return Time.time >= startupGateReleaseAt;

        ResolveStartupGatePanel();
        if (waitUntilPanelHidden != null && waitUntilPanelHidden.activeInHierarchy)
            return false;

        startupGateReleased = true;
        startupGateReleaseAt = Time.time + Mathf.Max(0f, delayAfterBlockingPanelHidden);
        MarkCurrentBackendJsonAsSeen("Startup panel hidden");
        Debug.Log("[Part1] Voice gate released after startup panels.", this);
        return Time.time >= startupGateReleaseAt;
    }

    private void MarkCurrentBackendJsonAsSeen(string reason)
    {
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (network == null || string.IsNullOrWhiteSpace(network.LastJson))
            return;

        lastProcessedBackendJson = network.LastJson;
        initialBackendSpeechToIgnore = ExtractBackendSpeechText(network.LastJson);
        hasSkippedInitialBackendJson = true;
        Debug.Log("[Part1] Cached backend speech ignored on gate release (" + reason + "): " + initialBackendSpeechToIgnore, this);
    }

    private void HideInstructionUI()
    {
        if (instructionPanel == null)
            return;

        if (!instructionPanel.activeSelf)
            return;

        Canvas canvas = instructionPanel.GetComponent<Canvas>();
        if (canvas == null)
        {
            instructionPanel.SetActive(false);
            return;
        }

        SetChildVisible(instructionPanel.transform, "TopHint_Panel", false);
        SetChildVisible(instructionPanel.transform, "SkipVoice_Button", false);
    }

    private static void SetChildVisible(Transform parent, string childName, bool visible)
    {
        Transform child = FindDeepChild(parent, childName);
        if (child != null)
            child.gameObject.SetActive(visible);
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
            Debug.LogWarning("[Part1] Failed to parse backend json: " + e.Message);
        }

        return string.Empty;
    }

    private static bool ContainsAnyKeyword(string text, string[] keywords)
    {
        if (string.IsNullOrEmpty(text) || keywords == null)
            return false;

        foreach (string keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                continue;

            if (text.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
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

            if (text.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
    [Serializable]
    private class BackendSpeechResponse
    {
        public string text;
        public string raw_text;
        public string llm_window_text;
    }
}




