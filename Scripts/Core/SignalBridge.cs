using System;
using UnityEngine;

public class SignalBridge : MonoBehaviour
{
    [Header("References")]
    public ScenarioController story;
    public EmotionStateManager emotion;
    public WorldDirector world;
    public SignalBus bus;

    [Header("Debug")]
    public bool logSignals = true;

    void Awake()
    {
        if (!story) story = FindObjectOfType<ScenarioController>();
        if (!emotion) emotion = FindObjectOfType<EmotionStateManager>();
        if (!world) world = FindObjectOfType<WorldDirector>();
        if (!bus) bus = FindObjectOfType<SignalBus>();
    }

    void OnEnable()
    {
        if (bus != null)
        {
            bus.OnUIEvent += HandleUIEvent;
            bus.OnInputEvent += HandleInputEvent;
        }

        if (story != null)
        {
            story.stepStarted.AddListener(OnStepStarted);
            story.stepCompleted.AddListener(OnStepCompleted);
            story.onScenarioCompleted.AddListener(OnScenarioCompleted);
            story.quizAnswered.AddListener(OnQuizAnswered);
        }

        if (emotion != null)
            emotion.OnEmotionChanged += OnEmotionChanged;
    }

    void OnDisable()
    {
        if (bus != null)
        {
            bus.OnUIEvent -= HandleUIEvent;
            bus.OnInputEvent -= HandleInputEvent;
        }

        if (story != null)
        {
            story.stepStarted.RemoveListener(OnStepStarted);
            story.stepCompleted.RemoveListener(OnStepCompleted);
            story.onScenarioCompleted.RemoveListener(OnScenarioCompleted);
            story.quizAnswered.RemoveListener(OnQuizAnswered);
        }

        if (emotion != null)
            emotion.OnEmotionChanged -= OnEmotionChanged;
    }

    // UI -> Story
    void HandleUIEvent(string key, SignalPayload payload)
    {
        if (logSignals) Debug.Log($"[SignalBridge] UI {key} {payload}");

        if (key == "UI.Next")
        {
            story?.Next();
        }
        else if (key == "UI.Choice")
        {
            if (payload != null)
                story?.SelectChoice(payload.choiceIndex);
        }
    }

    // Input.Click / UI.Click -> Story + World
    void HandleInputEvent(string key, SignalPayload payload)
    {
        if (logSignals) Debug.Log($"[SignalBridge] Input {key} {payload}");
        if (payload == null || string.IsNullOrEmpty(payload.targetId)) return;

        switch (payload.targetId)
        {
            case "teddy_bear":
            case "stethoscope":
            case "thermometer":
            case "blood_pressure":
                // TODO: 依 targetId 對應 Story 行為
                world?.ReceiveSignal("interaction." + payload.targetId, payload);
                break;
        }
    }

    // Story -> World
    void OnStepStarted(string stepId)
    {
        EmitWorld("story.step_started", new SignalPayload { stepId = stepId });
    }

    void OnStepCompleted(string stepId)
    {
        EmitWorld("story.step_completed", new SignalPayload { stepId = stepId });
    }

    void OnScenarioCompleted()
    {
        EmitWorld("story.scenario_completed", new SignalPayload());
    }

    void OnQuizAnswered(ScenarioQuiz quiz, int choiceIndex, bool correct)
    {
        EmitWorld("story.quiz_answered", new SignalPayload { stepId = story?.CurrentStep?.id, choiceIndex = choiceIndex, correct = correct });
    }

    void OnEmotionChanged(EmotionSnapshot snapshot)
    {
        if (snapshot == null) return;
        EmitWorld("emotion_score", new SignalPayload { stage = snapshot.stage, tension = snapshot.tension });
    }

    void EmitWorld(string key, SignalPayload payload)
    {
        if (logSignals) Debug.Log($"[SignalBridge] World {key} {payload}");
        world?.ReceiveSignal(key, payload);
    }
}

[Serializable]
public class SignalPayload
{
    public string stepId;
    public int choiceIndex;
    public bool correct;
    public int stage;
    public float tension;
    public string targetId;

    public override string ToString()
    {
        return $"{{stepId={stepId}, choiceIndex={choiceIndex}, correct={correct}, stage={stage}, tension={tension}, targetId={targetId}}}";
    }
}
