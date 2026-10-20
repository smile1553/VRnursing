#if UNITY_EDITOR
using NUnit.Framework;

public class SpeechGateMatcherTests
{
    [Test]
    public void OneKeywordGroup_DoesNotAdvance()
    {
        AudioAnalysisResponse response = Response("我想要貼紙", "neutral", 0.9f);
        string reason;

        Assert.IsFalse(SpeechGateMatcher.IsMatch(
            response,
            "貼紙|獎勵;不打針|不會痛|一下子|別怕",
            "reduce_fear",
            out reason));
        Assert.AreEqual("keyword_groups:1/2", reason);
    }

    [Test]
    public void OverlappingLegacyKeywords_CountAsOneConcept()
    {
        Assert.AreEqual(1, SpeechGateMatcher.CountMatchedKeywordGroups(
            "等一下幫你量體溫",
            "量體溫|體溫|耳溫"));
    }

    [Test]
    public void TwoDifferentConceptGroups_Advance()
    {
        AudioAnalysisResponse response = Response("先送你一張貼紙，等等一下子就完成", "neutral", 0.1f);
        string reason;

        Assert.IsTrue(SpeechGateMatcher.IsMatch(
            response,
            "貼紙|獎勵;不打針|不會痛|一下子|別怕",
            "reduce_fear",
            out reason));
        Assert.AreEqual("keyword_groups:2/2", reason);
    }

    [Test]
    public void ConfidentActionTag_AllowsDifferentWording()
    {
        AudioAnalysisResponse response = Response("我們先用你喜歡的方法試試看", "reduce_fear", 0.82f);
        string reason;

        Assert.IsTrue(SpeechGateMatcher.IsMatch(
            response,
            "貼紙|獎勵;不打針|不會痛|一下子|別怕",
            "reduce_fear|reassure_child",
            out reason));
        StringAssert.StartsWith("action_tag:reduce_fear", reason);
    }

    [Test]
    public void LowConfidenceActionTag_DoesNotAdvance()
    {
        AudioAnalysisResponse response = Response("我們試試看", "reduce_fear", 0.4f);
        string reason;

        Assert.IsFalse(SpeechGateMatcher.IsMatch(
            response,
            "貼紙|獎勵;不打針|不會痛|一下子|別怕",
            "reduce_fear",
            out reason));
    }

    static AudioAnalysisResponse Response(string text, string actionTag, float confidence)
    {
        return new AudioAnalysisResponse
        {
            ok = true,
            source = "student_speech",
            text = text,
            actionTag = actionTag,
            confidence = confidence
        };
    }
}
#endif
