using System;
using System.Collections.Generic;
using UnityEngine;

public class ScenarioScoreManager : MonoBehaviour
{
    [Header("References")]
    public ScenarioController controller;
    public ScenarioKeywordAdvancer keywordAdvancer;
    public ScenarioScoreProfile profile;

    [Header("Options")]
    public bool logCompletedReport = true;

    public ScenarioScoreReport CurrentReport { get; private set; }
    public event Action<ScenarioScoreReport> ScoreUpdated;
    public event Action<ScenarioScoreReport> ScoreCompleted;

    readonly HashSet<string> completedRequiredSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> matchedCommunicationSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, ScenarioQuizScore> quizScores = new Dictionary<string, ScenarioQuizScore>(StringComparer.Ordinal);

    void Awake()
    {
        if (!controller) controller = FindObjectOfType<ScenarioController>();
        if (!keywordAdvancer) keywordAdvancer = FindObjectOfType<ScenarioKeywordAdvancer>();
    }

    void OnEnable()
    {
        if (controller != null)
        {
            controller.stepStarted.AddListener(HandleStepStarted);
            controller.stepCompleted.AddListener(HandleStepCompleted);
            controller.quizAnswered.AddListener(HandleQuizAnswered);
            controller.onScenarioCompleted.AddListener(HandleScenarioCompleted);
        }
        if (keywordAdvancer != null)
            keywordAdvancer.MatchAccepted += HandleKeywordMatch;
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.stepStarted.RemoveListener(HandleStepStarted);
            controller.stepCompleted.RemoveListener(HandleStepCompleted);
            controller.quizAnswered.RemoveListener(HandleQuizAnswered);
            controller.onScenarioCompleted.RemoveListener(HandleScenarioCompleted);
        }
        if (keywordAdvancer != null)
            keywordAdvancer.MatchAccepted -= HandleKeywordMatch;
    }

    void HandleStepStarted(string stepId)
    {
        if (controller != null && controller.CurrentStepIndex == 0)
            ResetScore();
    }

    void HandleStepCompleted(string stepId)
    {
        ScenarioStep step = FindStep(stepId);
        if (step != null && step.playerActionRequired)
            completedRequiredSteps.Add(stepId);
        PublishProgress();
    }

    void HandleQuizAnswered(ScenarioQuiz quiz, int optionIndex, bool correct)
    {
        if (quiz == null || string.IsNullOrEmpty(quiz.question)) return;

        string stepId = controller?.CurrentStep?.id;
        string quizKey = string.IsNullOrEmpty(stepId) ? quiz.question : stepId;
        if (!quizScores.TryGetValue(quizKey, out ScenarioQuizScore score))
        {
            score = new ScenarioQuizScore
            {
                stepId = stepId,
                question = quiz.question,
                options = quiz.options,
                correctIndex = quiz.correctIndex
            };
            quizScores.Add(quizKey, score);
        }

        score.attempts++;
        score.answerHistory.Add(new ScenarioQuizAttempt
        {
            attemptNumber = score.attempts,
            selectedIndex = optionIndex,
            selectedAnswer = GetOption(quiz.options, optionIndex),
            correctAnswer = GetOption(quiz.options, quiz.correctIndex),
            correct = correct
        });
        if (correct && !score.correct)
        {
            score.correct = true;
            score.multiplier = GetAttemptMultiplier(score.attempts);
        }
        PublishProgress();
    }

    void HandleKeywordMatch(ScenarioKeywordMatch match)
    {
        if (match == null || string.IsNullOrEmpty(match.stepId)) return;
        matchedCommunicationSteps.Add(match.stepId);
        PublishProgress();
    }

    void HandleScenarioCompleted()
    {
        CurrentReport = BuildReport();
        ScoreUpdated?.Invoke(CurrentReport);
        ScoreCompleted?.Invoke(CurrentReport);
        if (logCompletedReport)
            Debug.Log($"[ScenarioScore] total={CurrentReport.total:0.0} knowledge={CurrentReport.knowledge:0.0} process={CurrentReport.process:0.0} communication={CurrentReport.communication:0.0} emotionCare={CurrentReport.emotionCare:0.0}");
    }

    public void ResetScore()
    {
        completedRequiredSteps.Clear();
        matchedCommunicationSteps.Clear();
        quizScores.Clear();
        CurrentReport = BuildReport();
        ScoreUpdated?.Invoke(CurrentReport);
    }

    public ScenarioScoreReport BuildReport()
    {
        ScenarioScoreProfile rules = profile;
        float knowledgeWeight = rules ? rules.knowledgeWeight : 40f;
        float processWeight = rules ? rules.processWeight : 25f;
        float communicationWeight = rules ? rules.communicationWeight : 25f;
        float emotionCareWeight = rules ? rules.emotionCareWeight : 10f;

        int quizCount = 0;
        int requiredCount = 0;
        int communicationCount = 0;
        int emotionCareCount = 0;
        float quizProgress = 0f;
        var quizDetails = new List<ScenarioQuizScore>();

        if (controller?.scenario?.steps != null)
        {
            foreach (ScenarioStep step in controller.scenario.steps)
            {
                if (step == null) continue;
                if (step.quiz != null && !string.IsNullOrEmpty(step.quiz.question))
                {
                    quizCount++;
                    string quizKey = GetQuizKey(step);
                    if (quizScores.TryGetValue(quizKey, out ScenarioQuizScore quizScore))
                    {
                        quizProgress += quizScore.multiplier;
                        quizDetails.Add(quizScore);
                    }
                    else
                    {
                        quizDetails.Add(new ScenarioQuizScore
                        {
                            stepId = step.id,
                            question = step.quiz.question,
                            options = step.quiz.options,
                            correctIndex = step.quiz.correctIndex
                        });
                    }
                }

                if (!step.playerActionRequired) continue;
                requiredCount++;
                if (HasCommunicationCriteria(step)) communicationCount++;
                if (IsEmotionCareStep(step, rules)) emotionCareCount++;
            }
        }

        int completedCount = CountMatches(completedRequiredSteps, step => step.playerActionRequired);
        int communicationMatchedCount = CountMatches(matchedCommunicationSteps, HasCommunicationCriteria);
        int emotionCareMatchedCount = CountMatches(matchedCommunicationSteps, step => IsEmotionCareStep(step, rules));

        var report = new ScenarioScoreReport
        {
            knowledge = Score(quizProgress, quizCount, knowledgeWeight),
            process = Score(completedCount, requiredCount, processWeight),
            communication = Score(communicationMatchedCount, communicationCount, communicationWeight),
            emotionCare = Score(emotionCareMatchedCount, emotionCareCount, emotionCareWeight),
            completedRequiredSteps = completedCount,
            requiredSteps = requiredCount,
            matchedCommunicationSteps = communicationMatchedCount,
            communicationSteps = communicationCount,
            matchedEmotionCareSteps = emotionCareMatchedCount,
            emotionCareSteps = emotionCareCount,
            quizzes = quizDetails.ToArray()
        };
        report.total = report.knowledge + report.process + report.communication + report.emotionCare;
        report.feedback = BuildFeedback(report, knowledgeWeight);
        return report;
    }

    void PublishProgress()
    {
        CurrentReport = BuildReport();
        ScoreUpdated?.Invoke(CurrentReport);
    }

    ScenarioStep FindStep(string stepId)
    {
        if (controller?.scenario?.steps == null || string.IsNullOrEmpty(stepId)) return null;
        return controller.scenario.steps.Find(step => step != null && string.Equals(step.id, stepId, StringComparison.OrdinalIgnoreCase));
    }

    static string GetQuizKey(ScenarioStep step)
    {
        return !string.IsNullOrEmpty(step?.id) ? step.id : step?.quiz?.question;
    }

    static string GetOption(string[] options, int index)
    {
        return options != null && index >= 0 && index < options.Length ? options[index] : string.Empty;
    }

    int CountMatches(HashSet<string> stepIds, Func<ScenarioStep, bool> predicate)
    {
        int count = 0;
        foreach (string stepId in stepIds)
        {
            ScenarioStep step = FindStep(stepId);
            if (step != null && predicate(step)) count++;
        }
        return count;
    }

    float GetAttemptMultiplier(int attempts)
    {
        if (attempts <= 1) return 1f;
        if (attempts == 2) return profile ? profile.secondAttemptMultiplier : 0.6f;
        return profile ? profile.laterAttemptMultiplier : 0.3f;
    }

    static bool HasCommunicationCriteria(ScenarioStep step)
    {
        return step != null && ((step.expectedKeywords != null && step.expectedKeywords.Length > 0) ||
            (step.expectedIntents != null && step.expectedIntents.Length > 0));
    }

    static bool IsEmotionCareStep(ScenarioStep step, ScenarioScoreProfile rules)
    {
        if (!HasCommunicationCriteria(step)) return false;
        if (step.emotionGate != null || ContainsIntent(step.expectedIntents, "reassure")) return true;
        if (rules?.emotionCareIntents == null) return false;
        foreach (string intent in rules.emotionCareIntents)
            if (ContainsIntent(step.expectedIntents, intent)) return true;
        return false;
    }

    static bool ContainsIntent(string[] intents, string target)
    {
        if (intents == null || string.IsNullOrEmpty(target)) return false;
        foreach (string intent in intents)
            if (string.Equals(intent, target, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static float Score(float achieved, int total, float weight)
    {
        return total <= 0 ? weight : Mathf.Clamp01(achieved / total) * Mathf.Max(0f, weight);
    }

    static string[] BuildFeedback(ScenarioScoreReport report, float knowledgeWeight)
    {
        var feedback = new List<string>();
        if (report.knowledge < 0.7f * knowledgeWeight)
            feedback.Add("建議複習生命徵象測量與兒科照護知識題。");
        if (report.communicationSteps > 0 && report.matchedCommunicationSteps < report.communicationSteps)
            feedback.Add("部分溝通步驟未以關鍵語句或正確意圖完成，可多練習說明與安撫。");
        if (report.emotionCareSteps > 0 && report.matchedEmotionCareSteps < report.emotionCareSteps)
            feedback.Add("遇到兒童緊張時，可先使用安撫、轉移注意力或角色扮演策略。");
        if (feedback.Count == 0)
            feedback.Add("表現完整：知識、流程與安撫溝通皆有達成。");
        return feedback.ToArray();
    }
}
