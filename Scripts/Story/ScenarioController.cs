using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI; // 保留給 Button 使用
using TMPro; // 必須引用，處理 TMP 元件

public class ScenarioController : MonoBehaviour
{
    [Header("Scenario Data")]
    public ScenarioAsset scenario;
    public EmotionStateManager emotionManager;

    [Header("UI 綁定")]
    public ScenarioUiBinding ui;

    [Header("事件")]
    public StringEvent cursorTargetChanged;
    public StringEvent stepStarted;
    public StringEvent stepCompleted;
    public UnityEvent onScenarioCompleted;
    public QuizAnswerEvent quizAnswered;
    public event Action<ScenarioStep> StepChanged;

    int _currentIndex = -1;
    ScenarioStep _currentStep;
    Coroutine _subtitleRoutine;
    ScenarioQuiz _activeQuiz;
    bool _waitingForCalm;

    public ScenarioStep CurrentStep => _currentStep;
    public int CurrentStepIndex => _currentIndex;
    public bool IsQuizActive => _activeQuiz != null;

    void Awake()
    {
        if (!emotionManager)
            emotionManager = FindObjectOfType<EmotionStateManager>();
    }

    void OnEnable()
    {
        if (emotionManager != null)
            emotionManager.OnEmotionChanged += HandleEmotionChanged;
    }

    void OnDisable()
    {
        if (emotionManager != null)
            emotionManager.OnEmotionChanged -= HandleEmotionChanged;
    }

    void Start()
    {
        if (scenario != null)
            StartScenario();
    }

    public void StartScenario()
    {
        _currentIndex = -1;
        ProceedToIndex(0);
    }

    public void Next()
    {
        if (_currentStep == null) return;
        if (!CanProgressPastCurrent()) return;

        if (!string.IsNullOrEmpty(_currentStep.id))
            stepCompleted?.Invoke(_currentStep.id);

        int target = _currentStep.explicitNextIndex >= 0 ? _currentStep.explicitNextIndex : _currentIndex + 1;
        ProceedToIndex(target);
    }

    public void Previous()
    {
        int target = Mathf.Max(0, _currentIndex - 1);
        ProceedToIndex(target);
    }

    public void JumpToStepId(string stepId)
    {
        if (scenario == null || string.IsNullOrEmpty(stepId)) return;
        int index = scenario.steps.FindIndex(s => s != null && string.Equals(s.id, stepId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) ProceedToIndex(index);
    }

    bool CanProgressPastCurrent()
    {
        var gate = _currentStep.emotionGate;
        if (gate == null) return true;
        if (!gate.blockWhenAnxious) return true;

        int stage = emotionManager?.Current?.stage ?? 0;
        if (stage >= gate.anxiousStageThreshold)
        {
            _waitingForCalm = true;
            if (!string.IsNullOrEmpty(gate.blockedSubtitle))
                ShowSubtitle(gate.blockedSubtitle, 3f);
            if (gate.fallbackStepIndex >= 0) ProceedToIndex(gate.fallbackStepIndex);
            return false;
        }
        return true;
    }

    void ProceedToIndex(int index)
    {
        if (scenario == null) return;
        if (index < 0 || index >= scenario.steps.Count)
        {
            _currentStep = null;
            onScenarioCompleted?.Invoke();
            return;
        }

        _currentIndex = index;
        _currentStep = scenario.steps[index];
        if (!string.IsNullOrEmpty(_currentStep.id))
            stepStarted?.Invoke(_currentStep.id);
        StepChanged?.Invoke(_currentStep);
        ApplyStep(_currentStep);
    }

    void ApplyStep(ScenarioStep step)
    {
        _waitingForCalm = false;
        HideQuiz();

        if (ui != null)
        {
            if (ui.speakerText) ui.speakerText.text = step.speaker.ToString();
            if (ui.dialogueText) ui.dialogueText.text = step.dialogue;
            if (ui.playerPromptText)
            {
                ui.playerPromptText.gameObject.SetActive(step.playerActionRequired);
                ui.playerPromptText.text = step.playerPrompt;
            }
        }

        cursorTargetChanged?.Invoke(step.cursorTargetId);

        if (step.subtitle != null && !string.IsNullOrEmpty(step.subtitle.text))
            ShowSubtitle(step.subtitle.text, step.subtitle.duration);
        else
            HideSubtitle();

        if (step.quiz != null && !string.IsNullOrEmpty(step.quiz.question))
        {
            ShowQuiz(step.quiz);
            return;
        }

        if (!step.waitForClick)
        {
            if (step.autoAdvanceDelay > 0f)
                StartCoroutine(AutoAdvance(step.autoAdvanceDelay));
            else
                Next();
        }
    }

    IEnumerator AutoAdvance(float delay)
    {
        yield return new WaitForSeconds(delay);
        Next();
    }

    void ShowSubtitle(string text, float duration)
    {
        if (ui == null) return;
        if (ui.subtitleRoot) ui.subtitleRoot.SetActive(true);
        if (ui.subtitleText) ui.subtitleText.text = text;

        if (_subtitleRoutine != null) StopCoroutine(_subtitleRoutine);
        if (duration > 0f) _subtitleRoutine = StartCoroutine(HideSubtitleLater(duration));
    }

    IEnumerator HideSubtitleLater(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideSubtitle();
    }

    void HideSubtitle()
    {
        if (ui == null) return;
        if (_subtitleRoutine != null) { StopCoroutine(_subtitleRoutine); _subtitleRoutine = null; }
        if (ui.subtitleRoot) ui.subtitleRoot.SetActive(false);
    }

    void ShowQuiz(ScenarioQuiz quiz)
    {
        _activeQuiz = quiz;
        if (ui == null || ui.quiz == null) return;

        if (ui.quiz.root) ui.quiz.root.SetActive(true);
        if (ui.quiz.questionText) ui.quiz.questionText.text = quiz.question;
        if (ui.quiz.explanationText) ui.quiz.explanationText.text = string.Empty;

        for (int i = 0; i < ui.quiz.options.Length; i++)
        {
            var option = ui.quiz.options[i];
            bool valid = quiz.options != null && i < quiz.options.Length && !string.IsNullOrEmpty(quiz.options[i]);

            if (option != null && option.button != null)
            {
                option.button.gameObject.SetActive(valid);
                option.button.onClick.RemoveAllListeners();
                if (valid)
                {
                    option.label.text = quiz.options[i];
                    int captured = i;
                    option.button.onClick.AddListener(() => OnQuizOptionSelected(captured));
                }
            }
        }
    }

    void HideQuiz()
    {
        _activeQuiz = null;
        if (ui == null || ui.quiz == null) return;
        if (ui.quiz.root) ui.quiz.root.SetActive(false);
    }

    void OnQuizOptionSelected(int index)
    {
        SelectChoice(index);
    }

    public void SelectChoice(int index)
    {
        if (_activeQuiz == null) return;

        bool correct = index == _activeQuiz.correctIndex;
        quizAnswered?.Invoke(_activeQuiz, index, correct);
        if (!correct)
        {
            if (!string.IsNullOrEmpty(_activeQuiz.explanation) && ui?.quiz?.explanationText)
                ui.quiz.explanationText.text = _activeQuiz.explanation;
            if (_activeQuiz.requireCorrectToProceed) return;
        }

        HideQuiz();
        Next();
    }

    void HandleEmotionChanged(EmotionSnapshot snapshot)
    {
        if (!_waitingForCalm || _currentStep == null) return;

        var gate = _currentStep.emotionGate;
        if (gate != null && snapshot.stage <= gate.calmStageRequirement)
        {
            _waitingForCalm = false;
        }
    }

    // --- 資料結構定義 ---

    [Serializable]
    public class ScenarioUiBinding
    {
        public TMP_Text speakerText;    // 改為 TMP_Text
        public TMP_Text dialogueText;   // 改為 TMP_Text
        public TMP_Text playerPromptText; // 改為 TMP_Text
        public GameObject subtitleRoot;
        public TMP_Text subtitleText;   // 改為 TMP_Text
        public QuizUi quiz;
    }

    [Serializable]
    public class QuizUi
    {
        public GameObject root;
        public TMP_Text questionText;    // 改為 TMP_Text
        public TMP_Text explanationText; // 改為 TMP_Text
        public QuizOption[] options;
    }

    [Serializable]
    public class QuizOption
    {
        public UnityEngine.UI.Button button; // 強制指定路徑
        public TMP_Text label;               // 改為 TMP_Text
    }

    [Serializable] public class StringEvent : UnityEvent<string> { }
    [Serializable] public class QuizAnswerEvent : UnityEvent<ScenarioQuiz, int, bool> { }
}
