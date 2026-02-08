using UnityEngine;

public class ScenarioDebugConsole : MonoBehaviour
{
    public ScenarioController controller;
    public EmotionStateManager emotionState;
    public bool showWindow = true;
    public Rect windowRect = new Rect(20, 20, 520, 360);

    string _jumpStepId = "";
    Vector2 _scroll;

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
        if (!emotionState)
            emotionState = FindObjectOfType<EmotionStateManager>();
    }

    void OnGUI()
    {
        if (!showWindow) return;
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Scenario Debug");
    }

    void DrawWindow(int id)
    {
        GUILayout.Label(controller && controller.CurrentStep != null ? $"Step: {controller.CurrentStep.id} ({controller.CurrentStepIndex})" : "Step: --");
        var emo = emotionState?.Current;
        if (emo != null)
            GUILayout.Label($"Tension {emo.tension:0.00}  Stage {emo.stage}");
        else
            GUILayout.Label("No emotion data");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Start")) controller?.StartScenario();
        if (GUILayout.Button("Prev")) controller?.Previous();
        if (GUILayout.Button("Next")) controller?.Next();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Jump Id", GUILayout.Width(60));
        _jumpStepId = GUILayout.TextField(_jumpStepId);
        if (GUILayout.Button("Go", GUILayout.Width(50))) controller?.JumpToStepId(_jumpStepId);
        GUILayout.EndHorizontal();

        if (controller?.scenario?.steps != null)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(120));
            for (int i = 0; i < controller.scenario.steps.Count; i++)
            {
                var step = controller.scenario.steps[i];
                if (step == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(i.ToString(), GUILayout.Width(30));
                GUILayout.Label(step.id, GUILayout.Width(120));
                if (GUILayout.Button("Jump", GUILayout.Width(60))) controller.JumpToIndex(i);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        if (GUILayout.Button("Close")) showWindow = false;
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
}
