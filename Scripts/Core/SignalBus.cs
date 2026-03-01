using System;
using UnityEngine;

public class SignalBus : MonoBehaviour
{
    public static SignalBus Instance { get; private set; }

    public event Action<ScenarioStep> OnScenarioStepChanged;
    public event Action<int> OnEmotionStageChanged;
    public event Action<EmotionSnapshot> OnEmotionSnapshot;
    public event Action<string, SignalPayload> OnInputEvent;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void PublishScenarioStepChanged(ScenarioStep step) => OnScenarioStepChanged?.Invoke(step);
    public void PublishEmotionStageChanged(int stage) => OnEmotionStageChanged?.Invoke(stage);
    public void PublishEmotionSnapshot(EmotionSnapshot snapshot) => OnEmotionSnapshot?.Invoke(snapshot);
    public void PublishInputEvent(string key, SignalPayload payload) => OnInputEvent?.Invoke(key, payload);
}
