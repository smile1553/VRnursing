using System;
using UnityEngine;

public class EmotionStateSimulator : MonoBehaviour
{
    public EmotionStateManager manager;

    [Header("Manual Control")]
    public float tension = 0f;
    public int stage = 0;
    public string emotion = "neutral";
    public string intent = "explain";
    public string sentiment = "neutral";
    [Range(0f, 1f)] public float toxicity;
    [Range(0f, 1f)] public float coercion;
    [Range(0f, 1f)] public float confidence = 0.5f;

    [Header("Sequence Player")]
    public EmotionSimClip clip;
    public bool playOnStart = true;
    public bool loopSequence = true;

    bool _playing;
    float _time;

    void Awake()
    {
        if (!manager)
            manager = FindObjectOfType<EmotionStateManager>();
    }

    void Start()
    {
        if (playOnStart && clip != null && clip.keyframes != null && clip.keyframes.Length > 0)
            PlaySequence();
    }

    void Update()
    {
        if (_playing)
            TickSequence();
    }

    public void ApplyManual()
    {
        if (!manager)
            return;
        manager.ApplyManualState(tension, stage, emotion, new EmotionLlmInfo
        {
            intent = intent,
            sentiment = sentiment,
            toxicity = toxicity,
            coercion = coercion,
            confidence = confidence
        });
    }

    public void PlaySequence()
    {
        if (clip == null || clip.keyframes == null || clip.keyframes.Length == 0)
        {
            RuntimeLog.Warning("[EmotionSim] clip empty");
            return;
        }
        _playing = true;
        _time = 0f;
        ApplyFromClip(clip.keyframes[0]);
    }

    public void StopSequence()
    {
        _playing = false;
    }

    void TickSequence()
    {
        if (clip == null || clip.keyframes == null || clip.keyframes.Length == 0)
            return;

        _time += Time.deltaTime;
        var duration = clip.Duration;
        if (_time > duration)
        {
            if (loopSequence)
                _time %= duration;
            else
            {
                _time = duration;
                _playing = false;
            }
        }

        EmotionSimKeyframe a = clip.keyframes[0];
        EmotionSimKeyframe b = clip.keyframes[clip.keyframes.Length - 1];
        for (int i = 0; i < clip.keyframes.Length; i++)
        {
            var kf = clip.keyframes[i];
            if (kf.time <= _time) a = kf;
            if (kf.time >= _time)
            {
                b = kf;
                break;
            }
        }

        float span = Mathf.Max(0.0001f, b.time - a.time);
        float t = Mathf.Clamp01((_time - a.time) / span);
        float blendedTension = Mathf.Lerp(a.tension, b.tension, t);
        int blendedStage = Mathf.RoundToInt(Mathf.Lerp(a.stage, b.stage, t));
        ApplyFromBlend(blendedTension, blendedStage, a, b, t);
    }

    void ApplyFromBlend(float tensionValue, int stageValue, EmotionSimKeyframe a, EmotionSimKeyframe b, float t)
    {
        var llm = new EmotionLlmInfo
        {
            intent = t < 0.5f ? a.intent : b.intent,
            sentiment = t < 0.5f ? a.sentiment : b.sentiment,
            toxicity = Mathf.Lerp(a.toxicity, b.toxicity, t),
            coercion = Mathf.Lerp(a.coercion, b.coercion, t),
            confidence = Mathf.Lerp(a.confidence, b.confidence, t)
        };
        manager?.ApplyManualState(tensionValue, stageValue, t < 0.5f ? a.emotion : b.emotion, llm);
    }

    void ApplyFromClip(EmotionSimKeyframe keyframe)
    {
        manager?.ApplyManualState(keyframe.tension, keyframe.stage, keyframe.emotion, new EmotionLlmInfo
        {
            intent = keyframe.intent,
            sentiment = keyframe.sentiment,
            toxicity = keyframe.toxicity,
            coercion = keyframe.coercion,
            confidence = keyframe.confidence
        });
    }
}

[CreateAssetMenu(menuName = "VRNursing/Emotion Sim Clip", fileName = "EmotionSimClip")]
public class EmotionSimClip : ScriptableObject
{
    public EmotionSimKeyframe[] keyframes;
    public float Duration
    {
        get
        {
            if (keyframes == null || keyframes.Length == 0) return 0f;
            float max = 0f;
            foreach (var k in keyframes)
                if (k != null)
                    max = Mathf.Max(max, k.time);
            return max;
        }
    }
}

[Serializable]
public class EmotionSimKeyframe
{
    public float time;
    public float tension;
    public int stage;
    public string emotion = "neutral";
    public string intent = "explain";
    public string sentiment = "neutral";
    [Range(0f, 1f)] public float toxicity;
    [Range(0f, 1f)] public float coercion;
    [Range(0f, 1f)] public float confidence = 0.5f;
}
