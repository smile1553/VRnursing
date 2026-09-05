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

    [Header("Options")]
    [SerializeField] private bool autoAdvanceDialogue = true;
    [SerializeField] private float dialogueAdvanceDelay = 4f;
    [SerializeField] private bool hideQuizAfterCorrect = false;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectAnswer;
    [SerializeField] private UnityEvent onWrongAnswer;

    private int dialogueIndex = -1;
    private Coroutine dialogueRoutine;

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

    private void Awake()
    {
        ResolveDialogueManager();
        SetPanelVisible(dialoguePanel, false, "Dialogue_Panel");
        SetPanelVisible(quizPanel, false, "Quiz_Panel_1");
        ClearFeedback();
    }

    public void SkipVoiceAndStartDialogue()
    {
        HideInstructionUI();

        dialogueIndex = 0;
        ShowDialogue();
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
            dialogueRoutine = StartCoroutine(AdvanceDialogueAfterDelay(GetDialogueDelay(clip)));
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
        if (useAudioLengthForDialogueDelay && clip != null)
            return Mathf.Max(0.1f, clip.length + extraDelayAfterAudio);

        return Mathf.Max(0.1f, dialogueAdvanceDelay);
    }

    private void ShowQuiz()
    {
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
            if (hideQuizAfterCorrect)
                SetPanelVisible(quizPanel, false, "Quiz_Panel_1");

            onCorrectAnswer?.Invoke();
        }
        else
        {
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

    private void StopDialogueRoutine()
    {
        if (dialogueRoutine == null)
            return;

        StopCoroutine(dialogueRoutine);
        dialogueRoutine = null;
    }

    private void ResolveDialogueManager()
    {
        if (dialogueManager != null)
            return;

        if (dialoguePanel != null)
            dialogueManager = dialoguePanel.GetComponentInChildren<NewDialogueManager>(true);
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
}
