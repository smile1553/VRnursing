using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Part 8 - Explaining to Mom why the blood pressure has to be measured:
//   nurse prompt (she tells Mom: Yaya had a fever and a poor appetite, we worry about
//   dehydration, so measuring the blood pressure is to protect her)  ->  Quiz 8.
//
// Started automatically by Part7 after Quiz 7 is answered.
// All scene references are found by name at runtime; you can also assign them in the Inspector.
public class PediatricVitalSignsPart8Flow : MonoBehaviour
{
    private enum WaitingForNurseAction
    {
        None,
        ExplainToMom
    }

    [Header("Panels")]
    [Tooltip("While the nurse explains to Mom, what she says does not change Yaya's emotion bar.")]
    [SerializeField] private bool holdYayaEmotionWhileTalkingToMom = true;
    [SerializeField] private GameObject nursePromptPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;
    [Tooltip("First name that exists is used.")]
    [SerializeField] private string promptPanelNames = "P8Instruction_Canvas|P7Instruction_Canvas|P6Instruction_Canvas|P5Instruction_Canvas";
    [SerializeField] private string promptTextChildNames = "Prompt_Text|Instruction_Text|NursePromptText|Text (TMP)|Text";
    [SerializeField] private string dialoguePanelChildName = "Dialogue_Panel";
    [SerializeField] private string quizPanelChildName = "Quiz_Panel_8";

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
    [Tooltip("The nurse's sentence passes when it contains any of these (separated by |).")]
    [SerializeField] private string explainToMomKeywords = "發燒|食慾|胃口|脫水|保護|擔心|為了芽芽|為了保護";

    [Header("Dialogue (not used yet in this part; kept so lines can be added like the other parts)")]
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private AudioSource dialogueAudioSource;

    [Header("Texts (edit here)")]
    [Tooltip("Guides the nurse without giving her the words.")]
    [TextArea(2, 4)] [SerializeField] private string promptGuideMom =
        "媽媽擔心芽芽會痛，不想讓她量血壓。請向媽媽說明：為什麼芽芽現在需要量血壓？";

    [Header("Quiz size")]
    [Tooltip("Size of Quiz 8 compared with how it was laid out (1 = unchanged, smaller = smaller).")]
    [SerializeField] private float quizPanelSize = 0.9f;

    [Header("Next part")]
    [SerializeField] private bool startPart9AfterQuiz = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onQuizClosed;

    private Coroutine routine;
    private Coroutine quizCompletionRoutine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private bool skipRequested;
    private Button promptSkipButton;
    private int lastStartFrame = -1;

    // ---------------------------------------------------------------------------------
    // Entry
    // ---------------------------------------------------------------------------------
    public static PediatricVitalSignsPart8Flow FindOrCreate()
    {
        PediatricVitalSignsPart8Flow flow = FindObjectOfType<PediatricVitalSignsPart8Flow>(true);
        if (flow != null)
            return flow;

        GameObject host = null;
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate == null || !candidate.scene.IsValid())
                continue;
            string n = candidate.name.Replace("_", string.Empty).Replace(" ", string.Empty);
            if (string.Equals(n, "Part8Flow", StringComparison.OrdinalIgnoreCase))
            {
                host = candidate;
                break;
            }
        }

        if (host == null)
            host = new GameObject("Part8_Flow");

        return host.AddComponent<PediatricVitalSignsPart8Flow>();
    }

    public void StartPart8()
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
        Debug.Log("[Part8] StartPart8: explaining to Mom why the blood pressure is measured.", this);
        routine = StartCoroutine(Part8Routine());
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
    private IEnumerator Part8Routine()
    {
        ResolveReferences();
        yayaAnimation?.PlaySittingIdle();
        momAnimation?.PlayStandingIdle();

        // 1) The nurse explains to Mom why the blood pressure is measured (keywords or Skip).
        // The nurse talks to Mom here: Yaya's emotion does not move while she does.
        KidEmotionPresenter.Hold(holdYayaEmotionWhileTalkingToMom);
        ShowNursePrompt(promptGuideMom, WaitingForNurseAction.ExplainToMom);
        yield return WaitForNurseActionOrSkip();
        KidEmotionPresenter.Hold(false);

        // 2) Quiz 8
        ShowQuiz();
        routine = null;
    }

    // ---------------------------------------------------------------------------------
    // Helpers (same as the other parts)
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
            Debug.LogWarning("[Part8] Nurse prompt text not found.", this);

        waitingForNurseAction = waitingAction;
    }

    private void ShowQuiz()
    {
        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        part3VisualDemo?.TemporarilyHideStickerForQuiz();
        ApplyQuizScale();
        SetPanelVisible(quizPanel, true, quizPanelChildName);
        ApplyQuizScale();

        QuizPanelRuntimeHelper.BeginQuiz(quizPanel, quizPanelChildName);
        if (quizCompletionRoutine != null)
            StopCoroutine(quizCompletionRoutine);
        quizCompletionRoutine = StartCoroutine(WaitForQuizPanelClosed());
    }

    // Quiz 8 is laid out bigger than the other quizzes: show it at a fixed, smaller size
    // (set before it appears, so it does not pop).
    private RectTransform scaledQuizPanel;
    private Vector3 quizPanelOriginalScale;

    private void ApplyQuizScale()
    {
        if (quizPanel == null)
            return;
        RectTransform panel = FindDeepChild(quizPanel.transform, quizPanelChildName) as RectTransform;
        if (panel == null)
            return;
        if (scaledQuizPanel != panel)
        {
            scaledQuizPanel = panel;
            quizPanelOriginalScale = panel.localScale;
        }
        panel.localScale = quizPanelOriginalScale * Mathf.Clamp(quizPanelSize, 0.2f, 2f);
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
        Debug.Log("[Part8] Quiz 8 closed.", this);
        onQuizClosed?.Invoke();

        if (startPart9AfterQuiz)
            PediatricVitalSignsPart9Flow.FindOrCreate().StartPart9();
    }

    private void HandleBackendJson(string json)
    {
        if (string.Equals(json, lastProcessedBackendJson, StringComparison.Ordinal))
            return;

        lastProcessedBackendJson = json;
        string speechText = ExtractBackendSpeechText(json);
        if (string.IsNullOrWhiteSpace(speechText))
            return;

        string keywords = waitingForNurseAction == WaitingForNurseAction.ExplainToMom ? explainToMomKeywords : string.Empty;

        if (ContainsAnyKeyword(speechText, keywords))
        {
            // Yaya is in meltdown (red): the nurse must calm her before the story moves on.
            if (KidEmotionGate.Blocking)
            {
                Debug.Log("[Part8] Keywords matched, but Yaya must be calmed first.", this);
                return;
            }

            Debug.Log($"[Part8] Backend matched {waitingForNurseAction}. text={speechText}", this);
            waitingForNurseAction = WaitingForNurseAction.None;
            return;
        }

        Debug.Log($"[Part8] Waiting for {waitingForNurseAction}, speech did not match. text={speechText}", this);
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
        if (dialogueManager == null && dialoguePanel != null)
            dialogueManager = dialoguePanel.GetComponentInChildren<NewDialogueManager>(true);
        if (dialogueManager == null)
            dialogueManager = FindObjectOfType<NewDialogueManager>(true);
        if (dialogueAudioSource == null)
        {
            dialogueAudioSource = GetComponent<AudioSource>();
            if (dialogueAudioSource == null)
                dialogueAudioSource = gameObject.AddComponent<AudioSource>();
            dialogueAudioSource.playOnAwake = false;
            dialogueAudioSource.spatialBlend = 0f;
        }

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
            Debug.LogWarning("[Part8] Failed to parse backend json: " + e.Message);
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
