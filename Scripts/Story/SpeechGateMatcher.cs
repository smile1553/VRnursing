using System;
using System.Collections.Generic;

/// <summary>
/// Matches a speech response by semantic action tag first, then by multiple
/// keyword concept groups. Groups are separated by ';' and variants inside a
/// group are separated by '|'. A single isolated keyword can never pass when
/// requiredKeywordGroups is two or more.
/// </summary>
public static class SpeechGateMatcher
{
    public const float DefaultMinSemanticConfidence = 0.65f;
    public const int DefaultRequiredKeywordGroups = 2;

    public static bool IsMatch(
        AudioAnalysisResponse response,
        string keywordGroups,
        string expectedActionTags,
        out string reason,
        float minSemanticConfidence = DefaultMinSemanticConfidence,
        int requiredKeywordGroups = DefaultRequiredKeywordGroups)
    {
        reason = "no_match";
        if (response == null || response.ignored ||
            !string.Equals(response.source, "student_speech", StringComparison.OrdinalIgnoreCase))
        {
            reason = "invalid_response";
            return false;
        }

        if (MatchesActionTag(response.actionTag, response.confidence, expectedActionTags, minSemanticConfidence))
        {
            reason = $"action_tag:{response.actionTag}:{response.confidence:0.00}";
            return true;
        }

        int matchedGroups = CountMatchedKeywordGroups(response.text, keywordGroups);
        int required = Math.Max(2, requiredKeywordGroups);
        reason = $"keyword_groups:{matchedGroups}/{required}";
        return matchedGroups >= required;
    }

    public static bool TryParseAndMatch(
        string json,
        string keywordGroups,
        string expectedActionTags,
        out AudioAnalysisResponse response,
        out string reason,
        float minSemanticConfidence = DefaultMinSemanticConfidence,
        int requiredKeywordGroups = DefaultRequiredKeywordGroups)
    {
        response = null;
        reason = "invalid_json";
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            response = UnityEngine.JsonUtility.FromJson<AudioAnalysisResponse>(json);
        }
        catch (Exception)
        {
            return false;
        }

        return IsMatch(
            response,
            keywordGroups,
            expectedActionTags,
            out reason,
            minSemanticConfidence,
            requiredKeywordGroups);
    }

    public static int CountMatchedKeywordGroups(string text, string keywordGroups)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keywordGroups))
            return 0;

        string normalizedText = Normalize(text);
        string[] groups = keywordGroups.IndexOf(';') >= 0
            ? keywordGroups.Split(';')
            : keywordGroups.Split('|');
        int matches = 0;
        var matchedConcepts = new List<string>();

        foreach (string group in groups)
        {
            string matchedConcept = null;
            string[] variants = group.Split('|');
            foreach (string variant in variants)
            {
                string normalizedVariant = Normalize(variant);
                if (normalizedVariant.Length == 0)
                    continue;
                if (normalizedText.IndexOf(normalizedVariant, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedConcept = normalizedVariant;
                    break;
                }
            }

            if (matchedConcept == null)
                continue;

            bool overlapsExistingConcept = false;
            foreach (string existing in matchedConcepts)
            {
                if (existing.IndexOf(matchedConcept, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    matchedConcept.IndexOf(existing, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    overlapsExistingConcept = true;
                    break;
                }
            }

            if (overlapsExistingConcept)
                continue;

            matchedConcepts.Add(matchedConcept);
            matches++;
        }

        return matches;
    }

    static bool MatchesActionTag(
        string actionTag,
        float confidence,
        string expectedActionTags,
        float minSemanticConfidence)
    {
        if (string.IsNullOrWhiteSpace(actionTag) ||
            string.IsNullOrWhiteSpace(expectedActionTags) ||
            confidence < Math.Max(0f, Math.Min(1f, minSemanticConfidence)))
        {
            return false;
        }

        string[] expected = expectedActionTags.Split('|');
        foreach (string item in expected)
        {
            if (string.Equals(item.Trim(), actionTag.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var result = new char[value.Length];
        int count = 0;
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character))
                continue;
            result[count++] = char.ToLowerInvariant(character);
        }
        return new string(result, 0, count);
    }
}
