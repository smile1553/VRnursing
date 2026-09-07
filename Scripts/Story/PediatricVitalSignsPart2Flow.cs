using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

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
    [SerializeField] private string respirationKeywords = "\u5148\u4e0d\u78b0|\u8eba\u4e0b|\u89c0\u5bdf|\u547c\u5438";
    [SerializeField] private string heartbeatKeywords = "\u5fc3\u8df3|\u807d\u8a3a\u5668|\u4e0d\u6703\u75db|\u6478";

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
    [SerializeField] private float observationDelay = 3f;
    [SerializeField] private bool hideQuizAfterCorrect = false;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectAnswer;
    [SerializeField] private UnityEvent onWrongAnswer;

    private Coroutine routine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;

    private const string RespirationPrompt =
        "\u8acb\u5411\u5abd\u5abd\u8aaa\u660e\uff1a\u5148\u4e0d\u78b0\u82bd\u82bd\u7684\u8eab\u9ad4\uff0c\u8acb\u5abd\u5abd\u8b93\u82bd\u82bd\u8eba\u4e0b\uff0c\u4e26\u89c0\u5bdf\u80f8\u8179\u8d77\u4f0f\u4f86\u8a08\u7b97\u547c\u5438\u6b21\u6578\u3002";

    private const string ObservationPrompt =
        "\u8acb\u89c0\u5bdf\u82bd\u82bd\u7684\u80f8\u8179\u8d77\u4f0f\uff0c\u6e2c\u91cf\u547c\u5438\u4e00\u5206\u9418\u3002";

    private const string HeartbeatPrompt =
        "\u8acb\u5b89\u64ab\u82bd\u82bd\uff0c\u4e26\u8aaa\u660e\u8981\u807d\u5fc3\u8df3\uff0c\u8b93\u5979\u6478\u6478\u807d\u8a3a\u5668\uff0c\u544a\u8a34\u5979\u4e0d\u6703\u75db\u3002";

    private const string CorrectFeedback =
        "\u7b54\u5c0d\u4e86\uff01";

    private const string WrongFeedback =
        "\u518d\u60f3\u4e00\u4e0b\uff0c\u54ea\u4e00\u7a2e\u65b9\u6cd5\u6700\u80fd\u964d\u4f4e\u82bd\u82bd\u7684\u5bb3\u6015\uff1f";

    private void Awake()
    {
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        ResolveDialogueManager();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_2");
    }

    private void OnEnable()
    {
        if (network != null)
            network.EmotionJsonReceived += HandleBackendJson;
    }

    private void OnDisable()
    {
        if (network != null)
            network.EmotionJsonReceived -= HandleBackendJson;
    }

    public void StartPart2()
    {
        StopRoutine();
        waitingForNurseAction = WaitingForNurseAction.None;
        routine = StartCoroutine(Part2Routine());
    }

    public void CompleteRespirationExplanation()
    {
        if (waitingForNurseAction == WaitingForNurseAction.RespirationExplanation)
            waitingForNurseAction = WaitingForNurseAction.None;
    }

    public void CompleteHeartbeatExplanation()
    {
        if (waitingForNurseAction == WaitingForNurseAction.HeartbeatExplanation)
            waitingForNurseAction = WaitingForNurseAction.None;
    }

    public void DebugSkipCurrentNurseCheck()
    {
        if (waitingForNurseAction != WaitingForNurseAction.None)
            waitingForNurseAction = WaitingForNurseAction.None;
    }

    private void HandleBackendJson(string json)
    {
        if (!advanceFromBackendJson || waitingForNurseAction == WaitingForNurseAction.None)
            return;

        string keywords = waitingForNurseAction == WaitingForNurseAction.RespirationExplanation
            ? respirationKeywords
            : heartbeatKeywords;

        if (ContainsAnyKeyword(json, keywords))
        {
            Debug.Log($"[Part2] Backend matched {waitingForNurseAction}. Continue flow.", this);
            waitingForNurseAction = WaitingForNurseAction.None;
        }
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

    private IEnumerator Part2Routine()
    {
        ShowDialogueLine(momApologyLineIndex, momApologyClip);
        momAnimation?.PlayTalking();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(momApologyClip);

        ShowNursePrompt(RespirationPrompt, WaitingForNurseAction.RespirationExplanation);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

        ShowDialogueLine(momLayDownLineIndex, momLayDownClip);
        momAnimation?.PlayTalking();
        yayaAnimation?.PlayLayingDown();
        yield return WaitForDialogue(momLayDownClip);

        ShowDialogueLine(momBesideLineIndex, momBesideClip);
        momAnimation?.PlayTalking();
        yayaAnimation?.PlayLayingSleeping();
        yield return WaitForDialogue(momBesideClip);

        ShowNursePrompt(ObservationPrompt, WaitingForNurseAction.None);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlayLayingSleeping();
        yield return new WaitForSeconds(Mathf.Max(0.1f, observationDelay));

        ShowNursePrompt(HeartbeatPrompt, WaitingForNurseAction.HeartbeatExplanation);
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None);

        ShowDialogueLine(yayaRefuseLineIndex, yayaRefuseClip);
        momAnimation?.PlayStandingIdle();
        yayaAnimation?.PlaySittingDisbelief();
        yield return WaitForDialogue(yayaRefuseClip);

        ShowQuiz();
        routine = null;
    }

    private void ShowDialogueLine(int lineIndex, AudioClip clip)
    {
        ResolveDialogueManager();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPanelVisible(dialoguePanel, true, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_2");

        if (dialogueManager != null)
        {
            dialogueManager.StopPlayback();
            dialogueManager.ShowLine(lineIndex);
        }

        PlayAudio(clip);
    }

    private void ShowNursePrompt(string prompt, WaitingForNurseAction waitingAction)
    {
        if (dialogueManager != null)
            dialogueManager.StopPlayback();

        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_2");
        SetPanelVisible(nursePromptPanel, true, "TopHint_Panel");

        if (nursePromptText != null)
            nursePromptText.text = prompt;

        if (dialogueAudioSource != null)
            dialogueAudioSource.Stop();

        waitingForNurseAction = waitingAction;
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

    private void ShowQuiz()
    {
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        SetPanelVisible(quizPanel, true, "Quiz_Panel_2");
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

            onCorrectAnswer?.Invoke();
            return;
        }

        onWrongAnswer?.Invoke();
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

    private void ResolveDialogueManager()
    {
        if (dialogueManager != null)
            return;

        if (dialoguePanel != null)
            dialogueManager = dialoguePanel.GetComponentInChildren<NewDialogueManager>(true);
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
}
