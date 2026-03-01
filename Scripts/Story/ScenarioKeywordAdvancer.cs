using System;
using UnityEngine;

public class ScenarioKeywordAdvancer : MonoBehaviour
{
    public ScenarioController controller;
    public EmotionStateManager emotionState;

    [Header("Keyword Match")]
    public bool requirePlayerAction = true;
    public bool requireAllKeywords = false;
    public float minIntervalSeconds = 1f;
    public bool enableLlmAssist = true;
    [Range(0f, 1f)] public float defaultLlmConfidence = 0.7f;

    string _lastSignature;
    float _lastAdvanceTime;

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
        if (string.IsNullOrWhiteSpace(snapshot.text) && snapshot.llm == null) return;
        string signature = BuildSignature(snapshot);
        if (signature == _lastSignature) return;
        _lastSignature = signature;

        if (controller == null || controller.CurrentStep == null) return;
        if (controller.IsQuizActive) return;
        if (Time.time - _lastAdvanceTime < minIntervalSeconds) return;

        var step = controller.CurrentStep;
        if (requirePlayerAction && !step.playerActionRequired) return;

        bool keywordMatched = IsKeywordMatch(step.expectedKeywords, snapshot.text);
        bool llmMatched = !keywordMatched && IsLlmIntentMatch(step, snapshot.llm);
        if (keywordMatched || llmMatched)
        {
            _lastAdvanceTime = Time.time;
            controller.Next();
        }
    }

    bool IsKeywordMatch(string[] keywords, string text)
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
                hitCount++;
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

    static string BuildSignature(EmotionSnapshot snapshot)
    {
        string text = snapshot.text ?? string.Empty;
        string intent = snapshot.llm?.intent ?? string.Empty;
        float conf = snapshot.llm != null && !float.IsNaN(snapshot.llm.confidence) ? snapshot.llm.confidence : 0f;
        return $"{text}|{intent}|{conf:0.00}";
    }
}
