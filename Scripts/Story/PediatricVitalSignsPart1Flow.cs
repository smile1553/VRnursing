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

    [Header("Dialogue Text")]
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

    private readonly string[] speakers =
    {
        "媽媽",
        "芽芽"
    };

    private readonly string[] dialogues =
    {
        "好！",
        "我不要打針！我不要！"
    };

    private const string Question =
        "考題1：請問護生要先測量哪一樣生命徵象，比較不會加重芽芽的害怕？\n\n" +
        "A. 測量血壓\n" +
        "B. 聽診心尖脈\n" +
        "C. 測量耳溫\n" +
        "D. 觀察呼吸次數";

    private void Awake()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (quizPanel != null)
            quizPanel.SetActive(false);

        ClearFeedback();
    }

    public void SkipVoiceAndStartDialogue()
    {
        if (instructionPanel != null)
            instructionPanel.SetActive(false);

        dialogueIndex = 0;
        ShowDialogue();
    }

    public void NextDialogue()
    {
        StopDialogueRoutine();
        dialogueIndex++;

        if (dialogueIndex >= dialogues.Length)
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
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (speakerText != null)
            speakerText.text = speakers[dialogueIndex];

        if (dialogueText != null)
            dialogueText.text = dialogues[dialogueIndex];

        PlayDialogueAnimation(dialogueIndex);
        AudioClip clip = PlayDialogueAudio(dialogueIndex);
        ClearFeedback();

        if (autoAdvanceDialogue)
        {
            StopDialogueRoutine();
            dialogueRoutine = StartCoroutine(AdvanceDialogueAfterDelay(GetDialogueDelay(clip)));
        }
    }

    private void PlayDialogueAnimation(int index)
    {
        if (index == 0)
        {
            momAnimation?.PlayTalking();
            yayaAnimation?.PlaySittingRubbingArm();
            return;
        }

        if (index == 1)
        {
            momAnimation?.PlayStandingIdle();
            yayaAnimation?.PlaySittingDisbelief();
        }
    }

    private AudioClip PlayDialogueAudio(int index)
    {
        if (dialogueAudioSource == null)
            return null;

        AudioClip clip = index switch
        {
            0 => momOkClip,
            1 => yayaNoInjectionClip,
            _ => null
        };

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

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (quizPanel != null)
            quizPanel.SetActive(true);

        if (quizQuestionText != null)
            quizQuestionText.text = Question;

        momAnimation?.PlayStandingIdle();
        ClearFeedback();
    }

    private void SelectAnswer(int index)
    {
        bool correct = index == 3;

        if (feedbackText != null)
            feedbackText.text = correct
                ? "答對了！先觀察呼吸次數，比較不會加重芽芽的害怕。"
                : "再想想看。現在芽芽很害怕，先不要碰觸身體會比較合適。";

        if (correct)
        {
            if (hideQuizAfterCorrect && quizPanel != null)
                quizPanel.SetActive(false);

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
}
