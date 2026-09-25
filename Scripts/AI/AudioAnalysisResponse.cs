using System;
using UnityEngine;

[Serializable]
public sealed class AudioAnalysisResponse
{
    public bool ok;
    public bool ignored;
    public bool duplicate;
    public string requestId;
    public string utteranceId;
    public string scenarioStepId;
    public string studentRunId;
    public string source;
    public string text;
    public float tension;
    public float patienceScore;
    public string previousKidEmotionState;
    public string emotionState;
    public string kidEmotionState;
    public string intent;
    public string actionTag;
    public float confidence;
    public float coercion;
    // Python rounds this to one decimal place, so it must not be deserialized as int.
    public float processingMs;

    public string ResolvedKidEmotionState =>
        !string.IsNullOrWhiteSpace(kidEmotionState) ? kidEmotionState : emotionState;
}

public static class AudioAnalysisResponseContract
{
    static readonly string[] RequiredProperties =
    {
        "ok", "ignored", "duplicate", "requestId", "utteranceId",
        "scenarioStepId", "studentRunId", "source", "text", "tension", "patienceScore",
        "previousKidEmotionState", "intent", "actionTag", "confidence",
        "coercion", "processingMs"
    };

    public static bool TryParseAndValidate(
        string json,
        string expectedRequestId,
        string expectedScenarioStepId,
        string expectedStudentRunId,
        out AudioAnalysisResponse response,
        out string error)
    {
        response = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(expectedRequestId) ||
            string.IsNullOrWhiteSpace(expectedScenarioStepId) ||
            string.IsNullOrWhiteSpace(expectedStudentRunId))
        {
            error = "Expected requestId, scenarioStepId and studentRunId must all be non-empty.";
            return false;
        }

        if (!BackendJsonObjectValidator.HasRequiredTopLevelProperties(json, RequiredProperties))
        {
            error = "Response is not a valid JSON object or is missing a required top-level property.";
            return false;
        }

        bool hasEmotionState = BackendJsonObjectValidator.HasRequiredTopLevelProperties(json, "emotionState") ||
            BackendJsonObjectValidator.HasRequiredTopLevelProperties(json, "kidEmotionState");
        if (!hasEmotionState)
        {
            error = "Response is missing top-level emotionState/kidEmotionState.";
            return false;
        }

        try
        {
            response = JsonUtility.FromJson<AudioAnalysisResponse>(json);
        }
        catch (Exception exception)
        {
            error = "Response JSON could not be parsed: " + exception.Message;
            return false;
        }

        if (response == null)
        {
            error = "Response JSON parsed to null.";
            return false;
        }
        if (!response.ok)
        {
            error = "Response ok was false.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(response.requestId) ||
            !string.Equals(response.requestId, expectedRequestId, StringComparison.Ordinal))
        {
            error = $"Response requestId mismatch. expected={expectedRequestId} actual={response.requestId}";
            return false;
        }
        if (!string.Equals(response.scenarioStepId ?? string.Empty,
            expectedScenarioStepId ?? string.Empty, StringComparison.Ordinal))
        {
            error = $"Response scenarioStepId mismatch. expected={expectedScenarioStepId} actual={response.scenarioStepId}";
            return false;
        }
        if (string.IsNullOrWhiteSpace(response.studentRunId) ||
            !string.Equals(response.studentRunId, expectedStudentRunId, StringComparison.Ordinal))
        {
            error = $"Response studentRunId mismatch. expected={expectedStudentRunId} actual={response.studentRunId}";
            return false;
        }
        if (string.IsNullOrWhiteSpace(response.utteranceId))
        {
            error = "Response utteranceId is empty.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(response.source))
        {
            error = "Response source is empty.";
            return false;
        }
        if (!response.ignored && string.IsNullOrWhiteSpace(response.ResolvedKidEmotionState))
        {
            error = "Non-ignored response has no emotion state.";
            return false;
        }
        if (float.IsNaN(response.tension) || float.IsInfinity(response.tension) ||
            float.IsNaN(response.patienceScore) || float.IsInfinity(response.patienceScore) ||
            float.IsNaN(response.confidence) || float.IsInfinity(response.confidence) ||
            float.IsNaN(response.coercion) || float.IsInfinity(response.coercion) ||
            response.processingMs < 0)
        {
            error = "Response contains an invalid numeric value.";
            return false;
        }

        return true;
    }
}
