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

    // keywordGroups may hold several lists separated by "||" (for example the list saved in the
    // scene and the newer list in the code); the best of them counts.
    public static int CountMatchedKeywordGroups(string text, string keywordGroups)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keywordGroups))
            return 0;

        if (keywordGroups.IndexOf("||", StringComparison.Ordinal) >= 0)
        {
            int best = 0;
            foreach (string list in keywordGroups.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries))
                best = Math.Max(best, CountMatchedKeywordGroups(text, list));
            return best;
        }

        string normalizedText = Normalize(FixMishearings(text));
        string[] groups = keywordGroups.IndexOf(';') >= 0
            ? keywordGroups.Split(';')
            : keywordGroups.Split('|');
        int matches = 0;
        var matchedConcepts = new List<string>();

        foreach (string group in groups)
        {
            // The first word of this group that is said and is not part of a word already counted
            // ("媽媽好" and "媽媽" are one word said once, not two).
            string matchedConcept = null;
            string[] variants = group.Split('|');
            foreach (string variant in variants)
            {
                string normalizedVariant = Normalize(variant);
                if (normalizedVariant.Length == 0)
                    continue;
                if (normalizedText.IndexOf(normalizedVariant, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (OverlapsAny(normalizedVariant, matchedConcepts))
                    continue;
                matchedConcept = normalizedVariant;
                break;
            }

            if (matchedConcept == null)
                continue;

            matchedConcepts.Add(matchedConcept);
            matches++;
        }

        return matches;
    }

    static bool OverlapsAny(string concept, List<string> existing)
    {
        foreach (string item in existing)
        {
            if (item.IndexOf(concept, StringComparison.OrdinalIgnoreCase) >= 0 ||
                concept.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    // For the parts that pass on any single word: true when the speech (after the
    // mishearing fixes) contains one of the '|' separated words.
    public static bool ContainsAnyWord(string text, string keywords)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keywords))
            return false;
        text = FixMishearings(text);
        foreach (string part in keywords.Split('|'))
        {
            string keyword = part.Trim();
            if (keyword.Length > 0 && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
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

    // Speech recognition often writes a word that sounds the same with other characters
    // ("耳聞" for "耳溫", "亞牙" for "芽芽"). These are put right before matching, so a correct
    // sentence is not rejected because of how it was written down. Longer phrases come first.
    static readonly string[][] Mishearings =
    {
        new[] { "馬好", "媽媽好" }, new[] { "爺爺", "芽芽" }, new[] { "亞牙", "芽芽" }, new[] { "咬牙", "芽芽" }, new[] { "瑤瑤", "芽芽" },
        new[] { "癌癌", "芽芽" }, new[] { "雅芽", "芽芽" }, new[] { "亞芽", "芽芽" }, new[] { "芽牙", "芽芽" }, new[] { "牙芽", "芽芽" },
        new[] { "牙牙", "芽芽" }, new[] { "鴨鴨", "芽芽" }, new[] { "呀呀", "芽芽" }, new[] { "亞亞", "芽芽" }, new[] { "雅雅", "芽芽" },
        new[] { "丫丫", "芽芽" }, new[] { "啞啞", "芽芽" }, new[] { "耳聞", "耳溫" }, new[] { "耳文", "耳溫" }, new[] { "耳紋", "耳溫" },
        new[] { "耳穩", "耳溫" }, new[] { "爾溫", "耳溫" }, new[] { "二溫", "耳溫" }, new[] { "耳瘟", "耳溫" }, new[] { "耳溫牆", "耳溫槍" },
        new[] { "耳溫腔", "耳溫槍" }, new[] { "耳溫鎗", "耳溫槍" }, new[] { "體文", "體溫" }, new[] { "體聞", "體溫" }, new[] { "提溫", "體溫" },
        new[] { "體瘟", "體溫" }, new[] { "體窩", "體溫" }, new[] { "兩耳溫", "量耳溫" }, new[] { "亮耳溫", "量耳溫" }, new[] { "涼耳溫", "量耳溫" },
        new[] { "梁耳溫", "量耳溫" }, new[] { "糧耳溫", "量耳溫" }, new[] { "兩體溫", "量體溫" }, new[] { "亮體溫", "量體溫" }, new[] { "涼體溫", "量體溫" },
        new[] { "梁體溫", "量體溫" }, new[] { "糧體溫", "量體溫" }, new[] { "生命燈象", "生命徵象" }, new[] { "生命正象", "生命徵象" }, new[] { "生命症象", "生命徵象" },
        new[] { "生命爭象", "生命徵象" }, new[] { "生命真相", "生命徵象" }, new[] { "生命徵像", "生命徵象" }, new[] { "生命蒸象", "生命徵象" }, new[] { "生命證象", "生命徵象" },
        new[] { "聽診氣", "聽診器" }, new[] { "聽診起", "聽診器" }, new[] { "聽枕器", "聽診器" }, new[] { "停診器", "聽診器" }, new[] { "聽診期", "聽診器" },
        new[] { "心掉", "心跳" }, new[] { "新跳", "心跳" }, new[] { "心間脈", "心尖脈" }, new[] { "新尖脈", "心尖脈" }, new[] { "心尖麥", "心尖脈" },
        new[] { "雪壓", "血壓" }, new[] { "血鴨", "血壓" }, new[] { "學壓", "血壓" }, new[] { "鴨脈帶", "壓脈帶" }, new[] { "壓麥帶", "壓脈帶" },
        new[] { "壓脈袋", "壓脈帶" }, new[] { "押脈帶", "壓脈帶" }, new[] { "鐵紙", "貼紙" }, new[] { "貼址", "貼紙" }, new[] { "帖紙", "貼紙" },
        new[] { "貼指", "貼紙" }, new[] { "雄雄", "熊熊" }, new[] { "兇兇", "熊熊" }, new[] { "小繩", "小熊" }, new[] { "少熊", "小熊" },
        new[] { "小雄", "小熊" }, new[] { "小兇", "小熊" }, new[] { "沒有發生", "沒有發燒" }, new[] { "沒發生", "沒發燒" }, new[] { "中氣", "充氣" },
        new[] { "衝氣", "充氣" }, new[] { "涅", "捏" }, new[] { "逼一聲", "嗶一聲" }, new[] { "必一聲", "嗶一聲" }, new[] { "嗶一身", "嗶一聲" },
        new[] { "逼神", "嗶一聲" }, new[] { "快痛的", "不會痛的" }, new[] { "不會燙", "不會痛" }, new[] { "股理", "鼓勵" }, new[] { "鼓立", "鼓勵" },
        new[] { "馬麻", "媽媽" }, new[] { "護理獅", "護理師" }, new[] { "戶理師", "護理師" }, new[] { "互理師", "護理師" }, new[] { "生命跡象", "生命徵象" },
        new[] { "生命徵相", "生命徵象" }, new[] { "呼息", "呼吸" }, new[] { "忽吸", "呼吸" }, new[] { "起浮", "起伏" }, new[] { "氣伏", "起伏" },
        new[] { "關察", "觀察" }, new[] { "官察", "觀察" }, new[] { "噗通", "撲通" }, new[] { "撲咚", "撲通" }, new[] { "噗咚", "撲通" },
        new[] { "二朵", "耳朵" }, new[] { "耳躲", "耳朵" }, new[] { "配配豬", "佩佩豬" }, new[] { "培培豬", "佩佩豬" }, new[] { "沛沛豬", "佩佩豬" },
        new[] { "佩佩朱", "佩佩豬" }, new[] { "玩藕", "玩偶" }, new[] { "晚偶", "玩偶" }, new[] { "永敢", "勇敢" }, new[] { "湧敢", "勇敢" },
        new[] { "試範", "示範" }, new[] { "是範", "示範" }, new[] { "安府", "安撫" }, new[] { "按撫", "安撫" }, new[] { "心肩脈", "心尖脈" },
        new[] { "壓脈代", "壓脈帶" }, new[] { "鴨麥帶", "壓脈帶" }, new[] { "食欲", "食慾" }, new[] { "拖水", "脫水" }, new[] { "托水", "脫水" },
        new[] { "側量", "測量" }, new[] { "策量", "測量" }, new[] { "涼血壓", "量血壓" }, new[] { "兩血壓", "量血壓" }, new[] { "梁血壓", "量血壓" },
        new[] { "側血壓", "量血壓" }, new[] { "涼一下", "量一下" }, new[] { "兩一下", "量一下" }, new[] { "袋子", "帶子" }, new[] { "發騷", "發燒" },
        new[] { "花燒", "發燒" },
        new[] { "邦", "幫" }, new[] { "姐姐", "姊姊" }, new[] { "爲", "為" }, new[] { "裏", "裡" }, new[] { "纔", "才" },
        new[] { "着", "著" },
    };

    public static string FixMishearings(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        foreach (string[] pair in Mishearings)
            value = value.Replace(pair[0], pair[1]);
        return value;
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
