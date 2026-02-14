using UnityEngine;

public enum KidEmotionState
{
    Calm,
    Uneasy,
    Crying,
    Meltdown
}

public enum MomActionType
{
    None,
    Approach,
    Comfort,
    ShowSticker,
    Roleplay
}

/// <summary>
/// Centralised keys used by WorldDirector/Debug tools. Keep in sync with TSV when expanded.
/// NEED_CORE_CHANGE: if SignalBus is wired, add the below keys to the core bus so scenario/system can broadcast them.
/// - "emotion_score" (float 0-100) to drive KidEmotionResponder
/// - "mom_approach" / "mom_comfort" / "mom_show_sticker" / "mom_roleplay" to drive MomActionResponder
/// </summary>
public static class WorldActions
{
    // Emotion routing keys
    public const string EmotionScoreChanged = "emotion_score"; // payload: float score 0~100

    // Mom actions (subset of story keys mapped to responder)
    public const string MomApproach = "mom_approach";
    public const string MomComfort = "mom_comfort";
    public const string MomShowSticker = "mom_show_sticker";
    public const string MomRoleplay = "mom_roleplay";

    public static MomActionType ParseMomAction(string actionKey)
    {
        if (string.IsNullOrEmpty(actionKey)) return MomActionType.None;
        var key = actionKey.ToLowerInvariant();

        if (key == MomComfort || key == "mom_play_music" || key == "mom_comfort")
            return MomActionType.Comfort;
        if (key == MomShowSticker || key == "mom_offer_sticker" || key == "reward_sticker")
            return MomActionType.ShowSticker;
        if (key == MomRoleplay || key == "mom_roleplay")
            return MomActionType.Roleplay;
        if (key == MomApproach || key == "mom_approach")
            return MomActionType.Approach;

        return MomActionType.None;
    }
}
