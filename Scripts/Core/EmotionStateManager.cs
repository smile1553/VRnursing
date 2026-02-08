using System;
using UnityEngine;

[Serializable]
public class EmotionSnapshot
{
    public float tension;
    public string emotion;
    public EmotionLlmInfo llm;
    public int stage;
    public string rawJson;
    public string timestampIso;

    public EmotionSnapshot Clone()
    {
        return new EmotionSnapshot
        {
            tension = tension,
            emotion = emotion,
            llm = llm != null ? llm.Clone() : null,
            stage = stage,
            rawJson = rawJson,
            timestampIso = timestampIso
        };
    }
}

[Serializable]
public class EmotionLlmInfo
{
    public string intent;
    public string sentiment;
    public float toxicity;
    public float coercion;
    public float confidence;

    public EmotionLlmInfo Clone()
    {
        return (EmotionLlmInfo)MemberwiseClone();
    }
}

public class EmotionStateManager : MonoBehaviour
{
    [Header("Dependencies")]
    public RunAI_Network network;
    public RunAI runAi;

    [Header("Stage Gates")]
    public int anxiousStage = 1;  // >= 1 代表緊張、不配合
    public int calmStage = 0;     // <= 0 代表已安撫

    [Header("Manual Debug")]
    public bool allowManualOverride = true;

    public static EmotionStateManager Instance { get; private set; }
    public EmotionSnapshot Current { get; private set; }
    public bool IsManualOverrideActive { get; private set; }

    public bool IsAnxious => Current != null && Current.stage >= anxiousStage;
    public bool IsCalm => Current != null && Current.stage <= calmStage;

    public event Action<EmotionSnapshot> OnEmotionChanged;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogWarning("Duplicate EmotionStateManager detected. Destroying the new one.");
            Destroy(this);
            return;
        }
        Instance = this;

        if (!runAi) runAi = GetComponent<RunAI>();
        if (!network) network = GetComponent<RunAI_Network>();
    }

    void OnEnable()
    {
        if (network != null)
            network.EmotionJsonReceived += HandleJson;
        if (runAi != null)
            runAi.StageChanged += HandleStageChanged;
    }

    void OnDisable()
    {
        if (network != null)
            network.EmotionJsonReceived -= HandleJson;
        if (runAi != null)
            runAi.StageChanged -= HandleStageChanged;
    }

    public void ClearManualOverride()
    {
        IsManualOverrideActive = false;
    }

    public void ApplyManualState(float tension, int stage, string emotion = null, EmotionLlmInfo llm = null)
    {
        if (!allowManualOverride)
        {
            Debug.LogWarning("[EmotionState] Manual override disabled.");
            return;
        }

        var snapshot = new EmotionSnapshot
        {
            tension = tension,
            emotion = emotion,
            llm = llm,
            stage = stage,
            timestampIso = DateTime.UtcNow.ToString("o"),
            rawJson = "{\"manual\":true}"
        };

        IsManualOverrideActive = true;
        SetSnapshot(snapshot);
    }

    void HandleJson(string json)
    {
        var snapshot = Current?.Clone() ?? new EmotionSnapshot();
        snapshot.rawJson = json;
        snapshot.timestampIso = DateTime.UtcNow.ToString("o");

        try
        {
            var data = JsonUtility.FromJson<EmotionPayload>(json);
            if (data != null)
            {
                snapshot.tension = data.tension;
                snapshot.emotion = data.emotion;
                if (data.llm != null)
                {
                    snapshot.llm = new EmotionLlmInfo
                    {
                        intent = data.llm.intent,
                        sentiment = data.llm.sentiment,
                        toxicity = data.llm.toxicity,
                        coercion = data.llm.coercion,
                        confidence = data.llm.confidence
                    };
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EmotionState] Failed to parse JSON: {e.Message}");
        }

        IsManualOverrideActive = false;
        snapshot.stage = runAi ? runAi.CurrentStage : snapshot.stage;
        SetSnapshot(snapshot);
    }

    void HandleStageChanged(int stage)
    {
        var snapshot = Current?.Clone() ?? new EmotionSnapshot();
        snapshot.stage = stage;
        if (string.IsNullOrEmpty(snapshot.timestampIso))
            snapshot.timestampIso = DateTime.UtcNow.ToString("o");
        IsManualOverrideActive = false;
        SetSnapshot(snapshot);
    }

    void SetSnapshot(EmotionSnapshot snapshot)
    {
        Current = snapshot;
        OnEmotionChanged?.Invoke(Current);
    }

    [Serializable]
    class EmotionPayload
    {
        public float tension;
        public string emotion;
        public LlmPayload llm;
    }

    [Serializable]
    class LlmPayload
    {
        public string intent;
        public string sentiment;
        public float toxicity;
        public float coercion;
        public float confidence;
    }
}
