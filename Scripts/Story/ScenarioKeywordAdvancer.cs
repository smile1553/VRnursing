using System;
using System.Collections.Generic;
using UnityEngine;

public class ScenarioKeywordAdvancer : MonoBehaviour
{
    public enum InputGateMode
    {
        DebugOpen,
        ScenarioStrict
    }

    public ScenarioController controller;
    public EmotionStateManager emotionState;

    [Header("Keyword Match")]
    public bool requirePlayerAction = true;
    public bool requireAllKeywords = false;
    public float minIntervalSeconds = 1f;
    public bool enableLlmAssist = true;
    [Range(0f, 1f)] public float defaultLlmConfidence = 0.7f;
    [Header("Gate Mode")]
    public InputGateMode gateMode = InputGateMode.ScenarioStrict;
    [Header("Speech Fallback")]
    public bool allowAnySpeechFallback = true;
    [Min(1)] public int anySpeechMinChars = 2;
    [Min(1)] public int fallbackMinAttempts = 2;
    public bool logDecisionJson = false;

    string _lastSignature;
    float _lastAdvanceTime;
    readonly Dictionary<string, int> _attemptsByStep = new Dictionary<string, int>();

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
        if (!emotionState)
            emotionState = FindObjectOfType<EmotionStateManager>();
    }

    void OnEnable()
    {
        if (emotionState != null)
            emotionState.OnEmotionChanged += HandleEmotionChanged;
    }

    void OnDisable()
    {
        if (emotionState != null)
            emotionState.OnEmotionChanged -= HandleEmotionChanged;
    }

    void HandleEmotionChanged(EmotionSnapshot snapshot)
    {
        if (snapshot == null) return;

        var trace = new DecisionTrace();
        string stepId = controller != null && controller.CurrentStep != null ? controller.CurrentStep.id : string.Empty;
        trace.stepId = stepId;
        trace.text = snapshot.text ?? string.Empty;
        trace.intent = snapshot.llm?.intent ?? string.Empty;
        trace.confidence = snapshot.llm != null && !float.IsNaN(snapshot.llm.confidence) ? snapshot.llm.confidence : 0f;

        if (string.IsNullOrWhiteSpace(snapshot.text) && snapshot.llm == null)
        {
            trace.reason = "empty_input";
            LogTrace(trace);
            return;
        }

        // Include current step id so the same utterance can still advance after step changes.
        string signature = BuildSignature(stepId, snapshot);
        if (signature == _lastSignature)
        {
            trace.reason = "duplicate_signature";
            LogTrace(trace);
            return;
        }
        _lastSignature = signature;

        if (controller == null || controller.CurrentStep == null)
        {
            trace.reason = "no_current_step";
            LogTrace(trace);
            return;
        }
        if (controller.IsQuizActive)
        {
            trace.reason = "quiz_active";
            LogTrace(trace);
            return;
        }
        if (Time.time - _lastAdvanceTime < minIntervalSeconds)
        {
            trace.reason = "cooldown";
            LogTrace(trace);
            return;
        }

        var step = controller.CurrentStep;
        trace.stepId = step.id;
        trace.expectedKeywords = step.expectedKeywords;
        trace.expectedIntents = step.expectedIntents;

        if (requirePlayerAction && !step.playerActionRequired)
        {
            trace.reason = "player_action_not_required";
            LogTrace(trace);
            return;
        }

        List<string> hitKeywords = new List<string>();
        bool keywordMatched = IsKeywordMatch(step.expectedKeywords, snapshot.text, hitKeywords);
        bool llmMatched = !keywordMatched && IsLlmIntentMatch(step, snapshot.llm);
        trace.keywordMatched = keywordMatched;
        trace.llmMatched = llmMatched;
        trace.hitKeywords = hitKeywords.ToArray();

        if (keywordMatched || llmMatched)
        {
            _lastAdvanceTime = Time.time;
            _attemptsByStep[trace.stepId ?? string.Empty] = 0;
            trace.advanced = true;
            trace.reason = keywordMatched ? "keyword_match" : "llm_intent_match";
            LogTrace(trace);
            controller.Next();
            return;
        }

        // If ASR wording is off but player is clearly speaking, allow progression after a few attempts.
        bool allowFallback = gateMode == InputGateMode.DebugOpen && allowAnySpeechFallback;
        if (allowFallback && IsAnySpeechFallback(step, snapshot))
        {
            string key = trace.stepId ?? string.Empty;
            int attempts = 0;
            _attemptsByStep.TryGetValue(key, out attempts);
            attempts += 1;
            _attemptsByStep[key] = attempts;

            if (attempts >= Mathf.Max(1, fallbackMinAttempts))
            {
                _lastAdvanceTime = Time.time;
                _attemptsByStep[key] = 0;
                trace.advanced = true;
                trace.reason = "any_speech_fallback";
                LogTrace(trace);
                controller.Next();
                return;
            }

            trace.reason = $"fallback_wait_{attempts}/{Mathf.Max(1, fallbackMinAttempts)}";
            LogTrace(trace);
            return;
        }

        trace.reason = "no_match";
        LogTrace(trace);
    }

    bool IsKeywordMatch(string[] keywords, string text, List<string> hits)
    {
        if (keywords == null || keywords.Length == 0) return false;
        if (string.IsNullOrWhiteSpace(text)) return false;

        int hitCount = 0;
        int validKeywordCount = 0;
        for (int i = 0; i < keywords.Length; i++)
        {
            var kw = keywords[i];
            if (string.IsNullOrWhiteSpace(kw)) continue;
            validKeywordCount++;
            if (text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hitCount++;
                if (hits != null) hits.Add(kw);
            }
        }

        if (requireAllKeywords)
            return validKeywordCount > 0 && hitCount >= validKeywordCount;
        return hitCount > 0;
    }

    bool IsLlmIntentMatch(ScenarioStep step, EmotionLlmInfo llm)
    {
        if (!enableLlmAssist) return false;
        if (step == null || !step.allowLlmAssist) return false;
        if (llm == null || string.IsNullOrWhiteSpace(llm.intent)) return false;

        var intents = step.expectedIntents;
        if (intents == null || intents.Length == 0) return false;

        float confidence = float.IsNaN(llm.confidence) ? 0f : llm.confidence;
        float minConfidence = Mathf.Clamp01(step.minLlmConfidence > 0f ? step.minLlmConfidence : defaultLlmConfidence);
        if (confidence < minConfidence) return false;

        string currentIntent = llm.intent.Trim();
        for (int i = 0; i < intents.Length; i++)
        {
            var expected = intents[i];
            if (string.IsNullOrWhiteSpace(expected)) continue;
            if (string.Equals(expected.Trim(), currentIntent, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    bool IsAnySpeechFallback(ScenarioStep step, EmotionSnapshot snapshot)
    {
        if (step == null || snapshot == null) return false;
        if (!step.playerActionRequired) return false;

        string text = (snapshot.text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;

        int chars = text.Replace(" ", string.Empty).Length;
        return chars >= Mathf.Max(1, anySpeechMinChars);
    }

    static string BuildSignature(string stepId, EmotionSnapshot snapshot)
    {
        string text = snapshot.text ?? string.Empty;
        string intent = snapshot.llm?.intent ?? string.Empty;
        float conf = snapshot.llm != null && !float.IsNaN(snapshot.llm.confidence) ? snapshot.llm.confidence : 0f;
        return $"{stepId}|{text}|{intent}|{conf:0.00}";
    }

    void LogTrace(DecisionTrace trace)
    {
        if (!logDecisionJson) return;
    }

    [Serializable]
    class DecisionTrace
    {
        public string stepId;
        public string text;
        public string intent;
        public float confidence;
        public bool keywordMatched;
        public bool llmMatched;
        public bool advanced;
        public string reason;
        public string[] hitKeywords;
        public string[] expectedKeywords;
        public string[] expectedIntents;
    }
}
