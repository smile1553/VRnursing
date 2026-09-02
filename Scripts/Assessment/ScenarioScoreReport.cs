using System;

[Serializable]
public class ScenarioScoreReport
{
    public float total;
    public float knowledge;
    public float process;
    public float communication;
    public float emotionCare;
    public int completedRequiredSteps;
    public int requiredSteps;
    public int matchedCommunicationSteps;
    public int communicationSteps;
    public int matchedEmotionCareSteps;
    public int emotionCareSteps;
    public ScenarioQuizScore[] quizzes;
    public string[] feedback;
}

[Serializable]
public class ScenarioQuizScore
{
    public string stepId;
    public string question;
    public int attempts;
    public bool correct;
    public float multiplier;
    public string[] options;
    public int correctIndex;
    public System.Collections.Generic.List<ScenarioQuizAttempt> answerHistory = new System.Collections.Generic.List<ScenarioQuizAttempt>();
}

[Serializable]
public class ScenarioQuizAttempt
{
    public int attemptNumber;
    public int selectedIndex;
    public string selectedAnswer;
    public string correctAnswer;
    public bool correct;
}
