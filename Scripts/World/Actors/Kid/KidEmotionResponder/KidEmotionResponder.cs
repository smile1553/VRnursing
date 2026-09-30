using UnityEngine;

public class KidEmotionResponder : MonoBehaviour
{
    [Header("Animation Triggers")]
    public string calmTrigger = "KidCalm";
    public string uneasyTrigger = "KidUneasy";
    public string cryTrigger = "KidCry";
    public string meltdownTrigger = "KidMeltdown";

    [Header("Thresholds (score 0~100)")]
    [Range(0f,100f)] public float uneasyThreshold = 20f;
    [Range(0f,100f)] public float cryThreshold = 55f;
    [Range(0f,100f)] public float meltdownThreshold = 80f;

    [Header("Refs")]
    public Animator animator;

    [Header("Backend State")]
    [Tooltip("When enabled, emotion changes move one step at a time: Calm <-> Uneasy <-> Crying <-> Meltdown.")]
    public bool stepThroughEmotionOrder = true;
    [Min(0f)] public float secondsBetweenSteps = 0.75f;
    public bool logStateChanges = true;

    KidEmotionState _current = KidEmotionState.Calm;
    KidEmotionState _target = KidEmotionState.Calm;
    float _nextStepAt;

    void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (_current == _target)
            return;
        if (Time.time < _nextStepAt)
            return;

        StepTowardTarget();
    }

    public void ApplyScore(float score)
    {
        var clamped = Mathf.Clamp(score, 0f, 100f);
        var next = DetermineState(clamped);
        ApplyBackendState(next, $"score {clamped:0}");
    }

    public void ApplyBackendState(string state)
    {
        KidEmotionState parsed;
        if (!TryParseState(state, out parsed))
        {
            RuntimeLog.Warning($"[KidEmotion] Unknown backend state '{state}'.");
            return;
        }

        ApplyBackendState(parsed, "backend");
    }

    public void ApplyBackendState(KidEmotionState state)
    {
        ApplyBackendState(state, "backend");
    }

    public void ForceState(KidEmotionState state)
    {
        _target = state;
        _current = state;
        _nextStepAt = Time.time + secondsBetweenSteps;
        PlayState(state);
        if (logStateChanges)
            RuntimeLog.Info($"[KidEmotion] forced state {state}");
    }

    KidEmotionState DetermineState(float score)
    {
        if (score >= meltdownThreshold) return KidEmotionState.Meltdown;
        if (score >= cryThreshold) return KidEmotionState.Crying;
        if (score >= uneasyThreshold) return KidEmotionState.Uneasy;
        return KidEmotionState.Calm;
    }

    void ApplyBackendState(KidEmotionState state, string source)
    {
        _target = state;

        if (!stepThroughEmotionOrder)
        {
            ForceState(state);
            return;
        }

        if (_current == _target)
        {
            if (logStateChanges)
                RuntimeLog.Info($"[KidEmotion] {source} target already {_current}");
            return;
        }

        if (Time.time >= _nextStepAt)
            StepTowardTarget();
        else if (logStateChanges)
            RuntimeLog.Info($"[KidEmotion] {source} target {_target}; waiting to step from {_current}");
    }

    void StepTowardTarget()
    {
        int current = (int)_current;
        int target = (int)_target;
        int next = current + (target > current ? 1 : -1);

        _current = (KidEmotionState)Mathf.Clamp(next, 0, 3);
        _nextStepAt = Time.time + secondsBetweenSteps;
        PlayState(_current);

        if (logStateChanges)
            RuntimeLog.Info($"[KidEmotion] step {_current} target={_target}");
    }

    static bool TryParseState(string state, out KidEmotionState parsed)
    {
        parsed = KidEmotionState.Calm;
        if (string.IsNullOrWhiteSpace(state))
            return false;

        string normalized = state.Trim();
        if (System.Enum.TryParse(normalized, true, out parsed))
            return true;

        switch (normalized.ToLowerInvariant())
        {
            case "normal":
            case "neutral":
            case "calm_loop":
                parsed = KidEmotionState.Calm;
                return true;
            case "fear":
            case "anxious":
            case "uneasy_loop":
                parsed = KidEmotionState.Uneasy;
                return true;
            case "cry":
            case "crying_loop":
                parsed = KidEmotionState.Crying;
                return true;
            case "meltdown_loop":
                parsed = KidEmotionState.Meltdown;
                return true;
            default:
                return false;
        }
    }

    void PlayState(KidEmotionState state)
    {
        if (!animator)
        {
            RuntimeLog.Warning("[KidEmotion] Missing animator, only logging state change.");
            return;
        }

        animator.ResetTrigger(calmTrigger);
        animator.ResetTrigger(uneasyTrigger);
        animator.ResetTrigger(cryTrigger);
        animator.ResetTrigger(meltdownTrigger);

        switch (state)
        {
            case KidEmotionState.Meltdown:
                animator.SetTrigger(meltdownTrigger);
                break;
            case KidEmotionState.Crying:
                animator.SetTrigger(cryTrigger);
                break;
            case KidEmotionState.Uneasy:
                animator.SetTrigger(uneasyTrigger);
                break;
            default:
                animator.SetTrigger(calmTrigger);
                break;
        }
    }
}
