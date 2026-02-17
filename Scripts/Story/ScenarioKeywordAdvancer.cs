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

    string _lastText;
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
        if (string.IsNullOrWhiteSpace(snapshot.text)) return;
        if (snapshot.text == _lastText) return;
        _lastText = snapshot.text;

        if (controller == null || controller.CurrentStep == null) return;
        if (controller.IsQuizActive) return;
        if (Time.time - _lastAdvanceTime < minIntervalSeconds) return;

        var step = controller.CurrentStep;
        if (requirePlayerAction && !step.playerActionRequired) return;
        if (step.expectedKeywords == null || step.expectedKeywords.Length == 0) return;

        if (IsMatch(step.expectedKeywords, snapshot.text))
        {
            _lastAdvanceTime = Time.time;
            controller.Next();
        }
    }

    bool IsMatch(string[] keywords, string text)
    {
        int hitCount = 0;
        for (int i = 0; i < keywords.Length; i++)
        {
            var kw = keywords[i];
            if (string.IsNullOrWhiteSpace(kw)) continue;
            if (text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                hitCount++;
        }

        if (requireAllKeywords)
            return hitCount >= keywords.Length;
        return hitCount > 0;
    }
}
