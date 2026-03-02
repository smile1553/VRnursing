using System;
using System.Collections.Generic;
using UnityEngine;

public enum ScenarioSpeaker
{
    Narrator,
    Nurse,
    Mother,
    Child,
    System
}

[CreateAssetMenu(menuName = "VRNursing/Scenario Asset", fileName = "ScenarioAsset")]
public class ScenarioAsset : ScriptableObject
{
    public List<ScenarioStep> steps = new();
}

[Serializable]
public class ScenarioStep
{
    public string id;
    public ScenarioSpeaker speaker;
    [TextArea(2, 5)] public string dialogue;
    public string cursorTargetId;
    [Tooltip("自動進入下一步前的延遲（秒）; <=0 代表等玩家互動")]
    public float autoAdvanceDelay = 0f;
    public bool waitForClick = true;
    [Header("玩家行為")]
    public bool playerActionRequired;
    [TextArea]
    public string playerPrompt;
    public string[] expectedKeywords;
    [Tooltip("允許使用 LLM intent 作為輔助判斷")]
    public bool allowLlmAssist = true;
    [Tooltip("可接受的 LLM intent 名稱")]
    public string[] expectedIntents;
    [Range(0f, 1f)]
    [Tooltip("LLM intent 最低信心值")]
    public float minLlmConfidence = 0.7f;
    public ScenarioHint[] hints;
    public ScenarioSubtitle subtitle;
    public ScenarioQuiz quiz;
    public ScenarioEmotionGate emotionGate;
    public ScenarioCommand[] commands;
    [Tooltip("預設下一步索引; -1 代表依序+1")]
    public int explicitNextIndex = -1;
}

[Serializable]
public class ScenarioSubtitle
{
    [TextArea]
    public string text;
    public float duration = 3f;
}

[Serializable]
public class ScenarioQuiz
{
    [TextArea]
    public string question;
    [TextArea]
    public string explanation;
    public string[] options = new string[4];
    public int correctIndex;
    public bool requireCorrectToProceed = true;
}

[Serializable]
public class ScenarioEmotionGate
{
    public bool blockWhenAnxious;
    [Tooltip("達到這個 Stage (含) 視為太緊張")]
    public int anxiousStageThreshold = 1;
    public int fallbackStepIndex = -1;
    [Tooltip("需要降到這個 Stage (含) 才繼續")]
    public int calmStageRequirement = 0;
    [TextArea] public string blockedSubtitle;
}

[Serializable]
public class ScenarioCommand
{
    public ScenarioCommandType type;
    public string payload;
}

public enum ScenarioCommandType
{
    None,
    PlayAnimation,
    PlayTimeline,
    PlayAudio,
    TriggerVfx,
    MoveCamera
}

[Serializable]
public class ScenarioHint
{
    [TextArea]
    public string text;
    public float showDelay;
    [Tooltip("需要錯誤/失敗多少次後才顯示；0=立即")] public int minFailures;
}
