using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PerformanceScoreManager : MonoBehaviour
{
    public ScenarioController controller;
    public EmotionStateManager emotionState;

    [Header("Score")]
    public int expectedQuizCount = 8;
    public int maxQuizScore = 80;
    public int maxEmotionScore = 20;
    public int wrongQuizPenalty = 10;

    [Header("Report")]
    public bool autoSaveOnComplete = true;
    public bool logScoreChanges = true;
    public string fileNamePrefix = "performance_report";

    readonly HashSet<string> _processedUtterances = new HashSet<string>();
    readonly HashSet<string> _processedQuizKeys = new HashSet<string>();
    readonly List<PerformanceScoreEvent> _events = new List<PerformanceScoreEvent>();

    float _startTime;
    int _quizScore;
    int _emotionScore;
    int _quizCorrectCount;
    int _quizWrongCount;
    int _emotionPenaltyTotal;

    public int QuizScore => _quizScore;
    public int EmotionScore => _emotionScore;
    public int TotalScore => _quizScore + _emotionScore;
    public int QuizCorrectCount => _quizCorrectCount;
    public int QuizWrongCount => _quizWrongCount;
    public int QuizAnsweredCount => _quizCorrectCount + _quizWrongCount;
    public int EmotionPenaltyTotal => _emotionPenaltyTotal;

    public event Action<PerformanceScoreSnapshot> ScoreChanged;

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
        if (!emotionState)
            emotionState = FindObjectOfType<EmotionStateManager>();
    }

    void OnEnable()
    {
        ResetScores();

        if (controller != null)
        {
            controller.quizAnswered.AddListener(OnQuizAnswered);
            controller.onScenarioCompleted.AddListener(OnScenarioCompleted);
        }

        if (emotionState != null)
            emotionState.OnEmotionChanged += OnEmotionChanged;
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.quizAnswered.RemoveListener(OnQuizAnswered);
            controller.onScenarioCompleted.RemoveListener(OnScenarioCompleted);
        }

        if (emotionState != null)
            emotionState.OnEmotionChanged -= OnEmotionChanged;
    }

    public void ResetScores()
    {
        _startTime = Time.time;
        _quizScore = maxQuizScore;
        _emotionScore = maxEmotionScore;
        _quizCorrectCount = 0;
        _quizWrongCount = 0;
        _emotionPenaltyTotal = 0;
        _processedUtterances.Clear();
        _processedQuizKeys.Clear();
        _events.Clear();
        EmitScoreChanged();
    }

    void OnQuizAnswered(ScenarioQuiz quiz, int selectedIndex, bool correct)
    {
        string key = BuildQuizKey(quiz);
        if (!_processedQuizKeys.Add(key))
            return;

        int penalty = correct ? 0 : wrongQuizPenalty;
        if (correct)
            _quizCorrectCount++;
        else
            _quizWrongCount++;

        _quizScore = Mathf.Max(0, _quizScore - penalty);
        AddEvent("quiz", key, penalty, correct ? "correct" : "wrong", quiz?.question);
        EmitScoreChanged();
    }

    void OnEmotionChanged(EmotionSnapshot snapshot)
    {
        if (snapshot == null) return;
        if (!string.Equals(snapshot.source, "student_speech", StringComparison.OrdinalIgnoreCase)) return;

        string key = BuildUtteranceKey(snapshot);
        if (string.IsNullOrEmpty(key) || !_processedUtterances.Add(key))
            return;

        int previousSeverity = GetEmotionSeverity(snapshot.previousKidEmotionState);
        int currentSeverity = GetEmotionSeverity(snapshot.kidEmotionState);
        if (previousSeverity < 0 || currentSeverity < 0)
            return;

        int penalty = Mathf.Max(0, currentSeverity - previousSeverity);
        if (penalty <= 0)
            return;

        _emotionPenaltyTotal += penalty;
        _emotionScore = Mathf.Max(0, _emotionScore - penalty);
        AddEvent("emotion", key, penalty, $"{snapshot.previousKidEmotionState}->{snapshot.kidEmotionState}", snapshot.text);
        EmitScoreChanged();
    }

    void OnScenarioCompleted()
    {
        AddEvent("scenario_completed", string.Empty, 0, "completed", null);
        if (autoSaveOnComplete)
            SaveReport();
    }

    string BuildQuizKey(ScenarioQuiz quiz)
    {
        if (controller != null && controller.CurrentStep != null && !string.IsNullOrEmpty(controller.CurrentStep.id))
            return controller.CurrentStep.id;
        if (controller != null && controller.CurrentStepIndex >= 0)
            return "step_" + controller.CurrentStepIndex;
        return quiz != null && !string.IsNullOrEmpty(quiz.question) ? quiz.question : "quiz_" + _processedQuizKeys.Count;
    }

    static string BuildUtteranceKey(EmotionSnapshot snapshot)
    {
        if (!string.IsNullOrEmpty(snapshot.utteranceId))
            return snapshot.utteranceId;
        if (!string.IsNullOrEmpty(snapshot.rawJson))
            return snapshot.rawJson;
        return snapshot.text;
    }

    static int GetEmotionSeverity(string state)
    {
        if (string.IsNullOrWhiteSpace(state)) return -1;
        if (string.Equals(state, "Calm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Normal", StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(state, "Uneasy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Fear", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(state, "Crying", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Cry", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(state, "Meltdown", StringComparison.OrdinalIgnoreCase)) return 3;
        return -1;
    }

    void AddEvent(string type, string key, int penalty, string reason, string text)
    {
        var evt = new PerformanceScoreEvent
        {
            type = type,
            key = key,
            time = Time.time - _startTime,
            penalty = penalty,
            reason = reason,
            text = text,
            quizScore = _quizScore,
            emotionScore = _emotionScore,
            totalScore = TotalScore
        };
        _events.Add(evt);

        if (logScoreChanges)
            Debug.Log($"[PerformanceScore] {type} penalty={penalty} total={TotalScore} quiz={_quizScore} emotion={_emotionScore} reason={reason}");
    }

    void EmitScoreChanged()
    {
        ScoreChanged?.Invoke(CreateSnapshot());
    }

    PerformanceScoreSnapshot CreateSnapshot()
    {
        return new PerformanceScoreSnapshot
        {
            quizScore = _quizScore,
            emotionScore = _emotionScore,
            totalScore = TotalScore,
            quizCorrectCount = _quizCorrectCount,
            quizWrongCount = _quizWrongCount,
            quizAnsweredCount = QuizAnsweredCount,
            expectedQuizCount = expectedQuizCount,
            emotionPenaltyTotal = _emotionPenaltyTotal
        };
    }

    public void SaveReport()
    {
        string folder = Path.Combine(Application.persistentDataPath, "ScenarioLogs");
        Directory.CreateDirectory(folder);

        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = string.IsNullOrEmpty(fileNamePrefix) ? "performance_report" : fileNamePrefix;
        string path = Path.Combine(folder, fileName + "_" + timeStamp + ".json");

        var report = new PerformanceScoreReport
        {
            generatedAt = DateTime.UtcNow.ToString("o"),
            expectedQuizCount = expectedQuizCount,
            quizMaxScore = maxQuizScore,
            emotionMaxScore = maxEmotionScore,
            quizScore = _quizScore,
            emotionScore = _emotionScore,
            totalScore = TotalScore,
            quizCorrectCount = _quizCorrectCount,
            quizWrongCount = _quizWrongCount,
            quizAnsweredCount = QuizAnsweredCount,
            emotionPenaltyTotal = _emotionPenaltyTotal,
            events = _events.ToArray()
        };

        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log("[PerformanceScore] saved -> " + path);
    }
}

[Serializable]
public class PerformanceScoreSnapshot
{
    public int quizScore;
    public int emotionScore;
    public int totalScore;
    public int quizCorrectCount;
    public int quizWrongCount;
    public int quizAnsweredCount;
    public int expectedQuizCount;
    public int emotionPenaltyTotal;
}

[Serializable]
public class PerformanceScoreEvent
{
    public string type;
    public string key;
    public float time;
    public int penalty;
    public string reason;
    public string text;
    public int quizScore;
    public int emotionScore;
    public int totalScore;
}

[Serializable]
public class PerformanceScoreReport
{
    public string generatedAt;
    public int expectedQuizCount;
    public int quizMaxScore;
    public int emotionMaxScore;
    public int quizScore;
    public int emotionScore;
    public int totalScore;
    public int quizCorrectCount;
    public int quizWrongCount;
    public int quizAnsweredCount;
    public int emotionPenaltyTotal;
    public PerformanceScoreEvent[] events;
}
