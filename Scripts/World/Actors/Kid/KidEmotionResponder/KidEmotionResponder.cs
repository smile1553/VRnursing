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

    KidEmotionState _current = KidEmotionState.Calm;

    void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    public void ApplyScore(float score)
    {
        var clamped = Mathf.Clamp(score, 0f, 100f);
        var next = DetermineState(clamped);
        if (next == _current) return;
        _current = next;
        PlayState(next);
        RuntimeLog.Info($"[KidEmotion] score {clamped:0} => {_current}");
    }

    public void ForceState(KidEmotionState state)
    {
        _current = state;
        PlayState(state);
        RuntimeLog.Info($"[KidEmotion] forced state {state}");
    }

    KidEmotionState DetermineState(float score)
    {
        if (score >= meltdownThreshold) return KidEmotionState.Meltdown;
        if (score >= cryThreshold) return KidEmotionState.Crying;
        if (score >= uneasyThreshold) return KidEmotionState.Uneasy;
        return KidEmotionState.Calm;
    }

    void PlayState(KidEmotionState state)
    {
        if (!animator)
        {
            RuntimeLog.Warning("[KidEmotion] Missing animator, only logging state change.");
            return;
        }

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
