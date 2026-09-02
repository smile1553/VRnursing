using UnityEngine;

[CreateAssetMenu(menuName = "VRNursing/Assessment/Score Profile", fileName = "ScenarioScoreProfile")]
public class ScenarioScoreProfile : ScriptableObject
{
    [Header("Category Weights")]
    [Min(0f)] public float knowledgeWeight = 40f;
    [Min(0f)] public float processWeight = 25f;
    [Min(0f)] public float communicationWeight = 25f;
    [Min(0f)] public float emotionCareWeight = 10f;

    [Header("Quiz Retries")]
    [Range(0f, 1f)] public float secondAttemptMultiplier = 0.6f;
    [Range(0f, 1f)] public float laterAttemptMultiplier = 0.3f;

    [Header("Emotion-care Intents")]
    public string[] emotionCareIntents =
    {
        "reassure", "calm_guidance", "reduce_fear", "distract",
        "role_play_demo", "reassure_child", "temp_reassurance"
    };
}
