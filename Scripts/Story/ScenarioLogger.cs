using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ScenarioLogger : MonoBehaviour
{
    public ScenarioController controller;
    public EmotionStateManager emotionState;
    public bool logToConsole = true;
    public bool autoSaveOnComplete = true;
    public string fileNamePrefix = "scenario_log";

    readonly List<ScenarioLogEvent> _events = new();
    float _startTime;

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
        if (!emotionState)
            emotionState = FindObjectOfType<EmotionStateManager>();
    }

    void OnEnable()
    {
        _startTime = Time.time;
        if (controller != null)
        {
            controller.stepStarted.AddListener(OnStepStarted);
            controller.stepCompleted.AddListener(OnStepCompleted);
            controller.quizAnswered.AddListener(OnQuizAnswered);
            controller.onScenarioCompleted.AddListener(OnScenarioCompleted);
        }
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.stepStarted.RemoveListener(OnStepStarted);
            controller.stepCompleted.RemoveListener(OnStepCompleted);
            controller.quizAnswered.RemoveListener(OnQuizAnswered);
            controller.onScenarioCompleted.RemoveListener(OnScenarioCompleted);
        }
    }

    void OnStepStarted(string stepId)
    {
        Append("step_started", stepId, null, null);
    }

    void OnStepCompleted(string stepId)
    {
        Append("step_completed", stepId, null, null);
    }

    void OnQuizAnswered(ScenarioQuiz quiz, int optionIndex, bool correct)
    {
        var data = new ScenarioLogQuiz
        {
            question = quiz?.question,
            selectedIndex = optionIndex,
            correctIndex = quiz != null ? quiz.correctIndex : -1,
            correct = correct
        };
        Append("quiz_answered", quiz?.question, data, null);
    }

    void OnScenarioCompleted()
    {
        Append("scenario_completed", null, null, null);
        if (autoSaveOnComplete)
            SaveToFile();
    }

    void Append(string type, string subject, ScenarioLogQuiz quiz, string notes)
    {
        var snapshot = emotionState?.Current;
        var evt = new ScenarioLogEvent
        {
            type = type,
            subject = subject,
            time = Time.time - _startTime,
            notes = notes,
            quiz = quiz,
            emotion = snapshot != null ? SerializableEmotionSnapshot.From(snapshot) : null
        };
        _events.Add(evt);
        if (logToConsole)
            Debug.Log($"[ScenarioLog] {type} {subject} @ {evt.time:0.00}s stage={evt.emotion?.stage}");
    }

    public void SaveToFile()
    {
        string folder = Path.Combine(Application.persistentDataPath, "ScenarioLogs");
        Directory.CreateDirectory(folder);
        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = string.IsNullOrEmpty(fileNamePrefix) ? "scenario_log" : fileNamePrefix;
        string path = Path.Combine(folder, fileName + "_" + timeStamp + ".json");
        var payload = new ScenarioLogFile
        {
            generatedAt = DateTime.UtcNow.ToString("o"),
            events = _events.ToArray()
        };
        var json = JsonUtility.ToJson(payload, true);
        File.WriteAllText(path, json);
        Debug.Log("[ScenarioLog] saved -> " + path);
    }
}

[Serializable]
public class ScenarioLogEvent
{
    public string type;
    public string subject;
    public float time;
    public string notes;
    public SerializableEmotionSnapshot emotion;
    public ScenarioLogQuiz quiz;
}

[Serializable]
public class ScenarioLogQuiz
{
    public string question;
    public int selectedIndex;
    public int correctIndex;
    public bool correct;
}

[Serializable]
public class ScenarioLogFile
{
    public string generatedAt;
    public ScenarioLogEvent[] events;
}

[Serializable]
public class SerializableEmotionSnapshot
{
    public float tension;
    public int stage;
    public string emotion;
    public string intent;
    public string sentiment;
    public float toxicity;
    public float coercion;
    public float confidence;

    public static SerializableEmotionSnapshot From(EmotionSnapshot snapshot)
    {
        if (snapshot == null) return null;
        return new SerializableEmotionSnapshot
        {
            tension = snapshot.tension,
            stage = snapshot.stage,
            emotion = snapshot.emotion,
            intent = snapshot.llm?.intent,
            sentiment = snapshot.llm?.sentiment,
            toxicity = snapshot.llm?.toxicity ?? 0f,
            coercion = snapshot.llm?.coercion ?? 0f,
            confidence = snapshot.llm?.confidence ?? 0f
        };
    }
}
