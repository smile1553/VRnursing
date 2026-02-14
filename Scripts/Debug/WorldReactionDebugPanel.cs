using UnityEngine;

public class WorldReactionDebugPanel : MonoBehaviour
{
    public WorldDirector director;
    public Rect windowRect = new Rect(20, 300, 360, 260);
    public bool showWindow = true;

    [Header("Emotion Test")]
    [Range(0f, 100f)] public float emotionScore = 0f;

    void Awake()
    {
        if (!director)
            director = FindObjectOfType<WorldDirector>();
    }

    void OnGUI()
    {
        if (!showWindow) return;
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "World Reaction Debug");
    }

    void DrawWindow(int id)
    {
        if (director == null)
        {
            GUILayout.Label("WorldDirector not found.");
            if (GUILayout.Button("Refresh")) director = FindObjectOfType<WorldDirector>();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
            return;
        }

        GUILayout.Label("Emotion Score 0~100");
        emotionScore = GUILayout.HorizontalSlider(emotionScore, 0f, 100f);
        GUILayout.Label($"Current: {emotionScore:0}");
        if (GUILayout.Button("Apply Score")) director.ApplyEmotionScore(emotionScore);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0"))  SetScore(0);
        if (GUILayout.Button("25")) SetScore(25);
        if (GUILayout.Button("50")) SetScore(50);
        if (GUILayout.Button("75")) SetScore(75);
        if (GUILayout.Button("100")) SetScore(100);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("Mom Actions");
        if (GUILayout.Button("Approach")) director.TriggerMomAction(MomActionType.Approach);
        if (GUILayout.Button("Comfort")) director.TriggerMomAction(MomActionType.Comfort);
        if (GUILayout.Button("Show Sticker")) director.TriggerMomAction(MomActionType.ShowSticker);
        if (GUILayout.Button("Roleplay")) director.TriggerMomAction(MomActionType.Roleplay);

        if (GUILayout.Button("Close")) showWindow = false;
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    void SetScore(float value)
    {
        emotionScore = value;
        director.ApplyEmotionScore(emotionScore);
    }
}
