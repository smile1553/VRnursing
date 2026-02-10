using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ScenarioController : MonoBehaviour
{
    [Header("Scenario Data")]
    public ScenarioAsset scenario;
    public EmotionStateManager emotionManager;

    [Header("字幕設定")]
    public float defaultSubtitleDuration = 1.5f;
    public bool repeatSubtitleWhileBlocked = true;
    public float repeatSubtitleInterval = 3f;

    [Header("UI 綁定")]
    public ScenarioUiBinding ui;

    [Header("情緒全域 Gate")]
    public bool suppressGateAfterQuiz = true;
    public float gateSuppressSeconds = 0.8f;

    public bool globalEmotionGate = false;
    public int globalAnxiousStageThreshold = 1;
    public int globalFallbackStepIndex = -1;
    public int globalCalmStageRequirement = 0;
    [TextArea] public string globalBlockedSubtitle = "芽芽太緊張，先安撫後再繼續。";

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
    float _gateSuppressedUntil;
    string _lastSubtitleText;
    float _lastSubtitleShownAt;

    public ScenarioStep CurrentStep => _currentStep;
    public int CurrentStepIndex => _currentIndex;

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
        ClearUiText();
        if (scenario != null)
            StartScenario();
    }

    
    void Update()
    {
        if (!repeatSubtitleWhileBlocked) return;
        if (!_waitingForCalm) return;
        if (string.IsNullOrEmpty(_lastSubtitleText)) return;
        if (repeatSubtitleInterval <= 0f) return;
        if (Time.time - _lastSubtitleShownAt >= repeatSubtitleInterval)
        {
            ShowSubtitle(_lastSubtitleText, defaultSubtitleDuration);
        }
    }

    public void StartScenario()
    {
        _currentIndex = -1;
        ProceedToIndex(0);
    }

    public void Next()
    {
        if (_currentStep == null)
            return;

        if (!CanProgressPastCurrent())
            return;

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
        if (scenario == null || string.IsNullOrEmpty(stepId))
            return;
        int index = scenario.steps.FindIndex(s => s != null && string.Equals(s.id, stepId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            ProceedToIndex(index);
        else
            Debug.LogWarning($"[Scenario] 找不到 id={stepId} 的步驟");
    }

    public void JumpToIndex(int index)
    {
        ProceedToIndex(index);
    }

    bool CanProgressPastCurrent()
    {
        if (globalEmotionGate && Time.time >= _gateSuppressedUntil)
        {
            int gateStage = emotionManager?.Current?.stage ?? 0;
            if (gateStage >= globalAnxiousStageThreshold)
            {
                _waitingForCalm = true;
                if (!string.IsNullOrEmpty(globalBlockedSubtitle))
                    ShowSubtitle(globalBlockedSubtitle, 3f);
                if (globalFallbackStepIndex >= 0)
                    ProceedToIndex(globalFallbackStepIndex);
                return false;
            }
        }

        var gate = _currentStep.emotionGate;
        if (gate == null) return true;
        if (!gate.blockWhenAnxious) return true;

        int stepStage = emotionManager?.Current?.stage ?? 0;
        if (stepStage >= gate.anxiousStageThreshold)
        {
            _waitingForCalm = true;
            if (!string.IsNullOrEmpty(gate.blockedSubtitle))
                ShowSubtitle(gate.blockedSubtitle, 3f);
            if (gate.fallbackStepIndex >= 0)
            {
                ProceedToIndex(gate.fallbackStepIndex);
            }
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
            if (ui.speakerText)
                ui.speakerText.text = step.speaker.ToString();
            if (ui.dialogueText)
                ui.dialogueText.text = step.dialogue;
            if (ui.playerPromptText)
            {
                if (step.playerActionRequired && !string.IsNullOrEmpty(step.playerPrompt))
                {
                    ui.playerPromptText.gameObject.SetActive(true);
                    ui.playerPromptText.text = step.playerPrompt;
                }
                else
                {
                    ui.playerPromptText.gameObject.SetActive(false);
                    ui.playerPromptText.text = string.Empty;
                }
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
        if (ui.subtitleRoot)
            ui.subtitleRoot.SetActive(true);
        if (ui.subtitleText)
        {
            ui.subtitleText.text = text;
            if (!ui.subtitleRoot)
                ui.subtitleText.gameObject.SetActive(true);
        }
        _lastSubtitleText = text;
        _lastSubtitleShownAt = Time.time;

        if (_subtitleRoutine != null)
            StopCoroutine(_subtitleRoutine);

        float hideAfter = duration > 0f ? duration : defaultSubtitleDuration;
        if (hideAfter > 0f)
            _subtitleRoutine = StartCoroutine(HideSubtitleLater(hideAfter));
    }

    IEnumerator HideSubtitleLater(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideSubtitle();
    }

    void HideSubtitle()
    {
        if (ui == null) return;
        if (_subtitleRoutine != null)
        {
            StopCoroutine(_subtitleRoutine);
            _subtitleRoutine = null;
        }
        if (ui.subtitleRoot)
            ui.subtitleRoot.SetActive(false);
        if (ui.subtitleText)
        {
            ui.subtitleText.text = string.Empty;
            if (!ui.subtitleRoot)
                ui.subtitleText.gameObject.SetActive(false);
        }
    }

    void ShowQuiz(ScenarioQuiz quiz)
    {
        _activeQuiz = quiz;
        if (ui == null || ui.quiz == null) return;

        if (ui.quiz.root)
            ui.quiz.root.SetActive(true);
        if (ui.quiz.questionText)
            ui.quiz.questionText.text = quiz.question;
        if (ui.quiz.explanationText)
            ui.quiz.explanationText.text = string.Empty;

        for (int i = 0; i < ui.quiz.options.Length; i++)
        {
            var option = ui.quiz.options[i];
            bool valid = quiz.options != null && i < quiz.options.Length && !string.IsNullOrEmpty(quiz.options[i]);
            if (option != null)
            {
                option.button.gameObject.SetActive(valid);
                option.button.onClick.RemoveAllListeners();
            }
            if (!valid) continue;

            string label = quiz.options[i];
            option.label.text = label;
            int captured = i;
            option.button.onClick.AddListener(() => OnQuizOptionSelected(captured));
        }
    }

    void HideQuiz()
    {
        _activeQuiz = null;
        if (ui == null || ui.quiz == null) return;
        if (ui.quiz.root)
            ui.quiz.root.SetActive(false);
        foreach (var opt in ui.quiz.options)
        {
            if (opt?.button != null)
                opt.button.onClick.RemoveAllListeners();
        }
    }

    void OnQuizOptionSelected(int index)
    {
        if (_activeQuiz == null)
            return;

        bool correct = index == _activeQuiz.correctIndex;
        quizAnswered?.Invoke(_activeQuiz, index, correct);
        if (suppressGateAfterQuiz)
            _gateSuppressedUntil = Time.time + Mathf.Max(0f, gateSuppressSeconds);
        if (!correct)
        {
            if (!string.IsNullOrEmpty(_activeQuiz.explanation) && ui?.quiz?.explanationText)
                ui.quiz.explanationText.text = _activeQuiz.explanation;
            if (_activeQuiz.requireCorrectToProceed)
                return;
        }

        HideQuiz();
        Next();
    }

    void HandleEmotionChanged(EmotionSnapshot snapshot)
    {
        if (!_waitingForCalm || _currentStep == null)
            return;

        var gate = _currentStep.emotionGate;
        if (gate == null)
        {
            _waitingForCalm = false;
            return;
        }

        int calmRequirement = globalEmotionGate ? globalCalmStageRequirement : gate.calmStageRequirement;
        if (snapshot.stage <= calmRequirement)
        {
            _waitingForCalm = false;
            if (ui?.subtitleText)
                ui.subtitleText.text = string.Empty;
        }
    }

    
    void ClearUiText()
    {
        if (ui == null) return;
        if (ui.speakerText) ui.speakerText.text = string.Empty;
        if (ui.dialogueText) ui.dialogueText.text = string.Empty;
        if (ui.playerPromptText) { ui.playerPromptText.text = string.Empty; ui.playerPromptText.gameObject.SetActive(false); }
        if (ui.subtitleText) ui.subtitleText.text = string.Empty;
        if (ui.subtitleRoot) ui.subtitleRoot.SetActive(false);
    }

    [System.Serializable]
    public class ScenarioUiBinding
    {
        public Text speakerText;
        public Text dialogueText;
        public Text playerPromptText;
        public GameObject subtitleRoot;
        public Text subtitleText;
        public QuizUi quiz;
    }

    [System.Serializable]
    public class QuizUi
    {
        public GameObject root;
        public Text questionText;
        public Text explanationText;
        public QuizOption[] options;
    }

    [System.Serializable]
    public class QuizOption
    {
        public Button button;
        public Text label;
    }

    [System.Serializable]
    public class StringEvent : UnityEvent<string> { }

    [System.Serializable]
    public class QuizAnswerEvent : UnityEvent<ScenarioQuiz, int, bool> { }
}
