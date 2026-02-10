using UnityEngine;

/// <summary>
/// Routes world signals (emotion & story actions) to actors via ActorRegistry.
/// </summary>
public class WorldDirector : MonoBehaviour
{
    [Header("Dependencies")]
    public ActorRegistry registry;
    public EmotionStateManager emotionState;

    [Header("Options")]
    public bool listenToEmotionState = true;
    [Tooltip("Extra logs for debug panel usage.")]
    public bool verboseLog = true;

    [Header("State (debug)")]
    [Range(0f, 100f)] public float lastEmotionScore;

    void Awake()
    {
        if (!registry)
            registry = FindObjectOfType<ActorRegistry>();
        if (!emotionState)
            emotionState = FindObjectOfType<EmotionStateManager>();
    }

    void OnEnable()
    {
        if (listenToEmotionState && emotionState != null)
            emotionState.OnEmotionChanged += HandleEmotionChanged;
    }

    void OnDisable()
    {
        if (listenToEmotionState && emotionState != null)
            emotionState.OnEmotionChanged -= HandleEmotionChanged;
    }

    // --- Entry points callable by SignalBus/Scenario/Debug panel ---

    /// <summary>Apply a numeric emotion score (0~100) to kid.</summary>
    public void ApplyEmotionScore(float score)
    {
        lastEmotionScore = Mathf.Clamp(score, 0f, 100f);
        var kid = registry ? registry.GetKidResponder() : null;
        if (kid == null)
        {
            Debug.LogWarning("[WorldDirector] Kid responder missing; score ignored.");
            return;
        }
        kid.ApplyScore(lastEmotionScore);
        if (verboseLog)
            Debug.Log($"[WorldDirector] Applied emotion score {lastEmotionScore:0} to Kid.");
    }

    /// <summary>Public hook for SignalBus-like caller: key + payload.</summary>
    public void ReceiveSignal(string key, object payload)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (key == WorldActions.EmotionScoreChanged && payload is float f)
        {
            ApplyEmotionScore(f);
            return;
        }

        var action = WorldActions.ParseMomAction(key);
        if (action != MomActionType.None)
            TriggerMomAction(action);
        else
            Debug.LogWarning($"[WorldDirector] Unknown signal {key}");
    }

    public void TriggerMomAction(string actionKey)
    {
        TriggerMomAction(WorldActions.ParseMomAction(actionKey));
    }

    public void TriggerMomAction(MomActionType action)
    {
        var mom = registry ? registry.GetMomResponder() : null;
        if (mom == null)
        {
            Debug.LogWarning("[WorldDirector] Mom responder missing; action ignored.");
            return;
        }
        mom.PlayAction(action);
        if (verboseLog)
            Debug.Log($"[WorldDirector] Triggered mom action {action}");
    }

    // --- Internal bridging to EmotionStateManager ---
    void HandleEmotionChanged(EmotionSnapshot snapshot)
    {
        if (snapshot == null) return;
        var score = ConvertSnapshotToScore(snapshot);
        ApplyEmotionScore(score);
    }

    float ConvertSnapshotToScore(EmotionSnapshot snapshot)
    {
        // Tension is roughly -5~5 in other tools; map to 0~100.
        float tensionScore = Mathf.InverseLerp(-5f, 5f, snapshot.tension) * 100f;

        // Stage gate mapping using EmotionStateManager thresholds if present.
        float stageScore = 50f;
        if (emotionState != null)
        {
            float min = emotionState.calmStage;
            float max = Mathf.Max(min + 0.01f, emotionState.anxiousStage);
            stageScore = Mathf.InverseLerp(min, max, snapshot.stage) * 100f;
        }

        // Weighted blend keeps both signals meaningful.
        var blended = Mathf.Clamp((tensionScore * 0.6f) + (stageScore * 0.4f), 0f, 100f);
        if (verboseLog)
            Debug.Log($"[WorldDirector] Emotion snapshot -> score {blended:0} (tension {snapshot.tension:0.00}, stage {snapshot.stage})");
        return blended;
    }
}
