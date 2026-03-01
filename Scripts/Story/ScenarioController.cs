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

    [Header("UI 綁定")]
    public ScenarioUiBinding ui;

    [Header("Quiz Placement (2F)")]
    [SerializeField] private Transform quizUiAnchor;
    [SerializeField] private Transform xrRigRoot;
    [SerializeField] private Transform quizXrSpawn;

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
    QuizUi _activeQuizUi;
    int _quizShownCount;
    bool _waitingForCalm;

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
        if (scenario != null)
            StartScenario();
    }

    public void StartScenario()
    {
        _currentIndex = -1;
        _quizShownCount = 0;
        _activeQuizUi = null;
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
        var gate = _currentStep.emotionGate;
        if (gate == null) return true;
        if (!gate.blockWhenAnxious) return true;

        int stage = emotionManager?.Current?.stage ?? 0;
        if (stage >= gate.anxiousStageThreshold)
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
            ui.subtitleText.text = text;

        if (_subtitleRoutine != null)
            StopCoroutine(_subtitleRoutine);

        if (duration > 0f)
            _subtitleRoutine = StartCoroutine(HideSubtitleLater(duration));
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
    }

    void ShowQuiz(ScenarioQuiz quiz)
    {
        _activeQuiz = quiz;
        if (ui == null) return;

        var quizUi = ResolveQuizUi();
        _activeQuizUi = quizUi;
        if (quizUi == null) return;

        if (quizUi.root)
            quizUi.root.SetActive(true);
        AlignQuizUi(quizUi.root);
        MoveRigToQuizSpawn();
        if (quizUi.questionText)
            quizUi.questionText.text = quiz.question;
        if (quizUi.explanationText)
            quizUi.explanationText.text = string.Empty;

        for (int i = 0; i < quizUi.options.Length; i++)
        {
            var option = quizUi.options[i];
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
        if (ui == null) return;
        var quizUi = _activeQuizUi != null ? _activeQuizUi : ui.quiz;
        if (quizUi == null) return;
        if (quizUi.root)
            quizUi.root.SetActive(false);
        foreach (var opt in quizUi.options)
        {
            if (opt?.button != null)
                opt.button.onClick.RemoveAllListeners();
        }
        _activeQuizUi = null;
    }

    QuizUi ResolveQuizUi()
    {
        if (ui.quizPanels != null && ui.quizPanels.Length > 0)
        {
            var index = Mathf.Clamp(_quizShownCount, 0, ui.quizPanels.Length - 1);
            _quizShownCount++;
            return ui.quizPanels[index];
        }
        return ui.quiz;
    }

    void AlignQuizUi(GameObject root)
    {
        if (root == null) return;
        if (quizUiAnchor == null) return;
        var rootTransform = root.transform;
        rootTransform.position = quizUiAnchor.position;
        rootTransform.rotation = quizUiAnchor.rotation;
        rootTransform.localScale = quizUiAnchor.localScale;
    }

    void MoveRigToQuizSpawn()
    {
        if (xrRigRoot == null || quizXrSpawn == null) return;
        xrRigRoot.position = quizXrSpawn.position;
        var yaw = quizXrSpawn.rotation.eulerAngles.y;
        xrRigRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void OnQuizOptionSelected(int index)
    {
        if (_activeQuiz == null)
            return;

        bool correct = index == _activeQuiz.correctIndex;
        quizAnswered?.Invoke(_activeQuiz, index, correct);
        if (!correct)
        {
            var quizUi = _activeQuizUi != null ? _activeQuizUi : ui?.quiz;
            if (!string.IsNullOrEmpty(_activeQuiz.explanation) && quizUi?.explanationText)
                quizUi.explanationText.text = _activeQuiz.explanation;
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

        if (snapshot.stage <= gate.calmStageRequirement)
        {
            _waitingForCalm = false;
            if (ui?.subtitleText)
                ui.subtitleText.text = string.Empty;
        }
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
        public QuizUi[] quizPanels;
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
