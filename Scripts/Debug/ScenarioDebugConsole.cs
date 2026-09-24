using UnityEngine;
using System.Collections.Generic;

public class ScenarioDebugConsole : MonoBehaviour
{
    public ScenarioController controller;
    public EmotionStateManager emotionState;
    public EmotionStateSimulator emotionSimulator;
    public bool showWindow = true;
    public Rect windowRect = new Rect(20, 20, 520, 360);
    public bool showLogConsole = true;
    public int maxLogEntries = 80;
    public Rect logWindowRect = new Rect(560, 20, 700, 360);

    string _jumpStepId = "";
    Vector2 _scroll;
    Vector2 _logScroll;
    readonly Queue<string> _logEntries = new Queue<string>();

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
        if (!emotionState)
            emotionState = FindObjectOfType<EmotionStateManager>();
        if (!emotionSimulator)
            emotionSimulator = FindObjectOfType<EmotionStateSimulator>();
    }

    void OnEnable()
    {
        Application.logMessageReceived += HandleLogMessage;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLogMessage;
    }

    void OnGUI()
    {
        if (!showWindow) return;
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Scenario Debug");
        if (showLogConsole)
            logWindowRect = GUILayout.Window(GetInstanceID() + 1, logWindowRect, DrawLogWindow, "Runtime Console");
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
        if (GUILayout.Button("Stage 0")) SetStage(0);
        if (GUILayout.Button("Stage 1")) SetStage(1);
        if (GUILayout.Button("Stage 2")) SetStage(2);
        if (GUILayout.Button("ApplyManual")) emotionSimulator?.ApplyManual();
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

    void DrawLogWindow(int id)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear", GUILayout.Width(80)))
            _logEntries.Clear();
        if (GUILayout.Button("Hide", GUILayout.Width(80)))
            showLogConsole = false;
        GUILayout.Label($"Entries: {_logEntries.Count}");
        GUILayout.EndHorizontal();

        _logScroll = GUILayout.BeginScrollView(_logScroll);
        foreach (var entry in _logEntries)
            GUILayout.Label(entry);
        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        string prefix = type switch
        {
            LogType.Error => "[Error]",
            LogType.Assert => "[Assert]",
            LogType.Warning => "[Warn]",
            LogType.Exception => "[Exception]",
            _ => "[Log]"
        };

        string line = $"{prefix} {condition}";
        _logEntries.Enqueue(line);
        while (_logEntries.Count > Mathf.Max(10, maxLogEntries))
            _logEntries.Dequeue();

        _logScroll.y = float.MaxValue;
    }

    void SetStage(int stage)
    {
        if (emotionSimulator == null) return;
        emotionSimulator.stage = stage;
    }
}
