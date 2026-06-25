using UnityEngine;
using System;

public class GameBootstrapper : MonoBehaviour
{
    [Header("Core References")]
    public EmotionStateManager emotionStateManager;

    [Header("Scenario")]
    public ScenarioController scenarioController;
    public ScenarioAsset defaultScenario;

    [Header("AI / Emotion")]
    public RunAI_Network runAiNetwork;

    [Header("Assessment")]
    public bool autoCreateScoreExport = true;
    public ScenarioScoreManager scoreManager;
    public ScenarioScoreCsvExporter scoreCsvExporter;

    [Header("Options")]
    public bool dontDestroyOnLoad = true;
    public bool autoStartScenario = true;

    void Awake()
    {
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (!emotionStateManager)
            emotionStateManager = FindObjectOfType<EmotionStateManager>();
        if (!scenarioController)
            scenarioController = FindObjectOfType<ScenarioController>();
        if (!runAiNetwork)
            runAiNetwork = FindObjectOfType<RunAI_Network>();

        if (scenarioController && defaultScenario && scenarioController.scenario == null)
            scenarioController.scenario = defaultScenario;

        EnsureScenarioCommandRuntime();
        EnsureAssessmentRuntime();
    }

    void Start()
    {
        if (runAiNetwork == null)
            RuntimeLog.Warning("[GameBootstrapper] RunAI_Network not found. Emotion feed will not start.");
        if (emotionStateManager == null)
            RuntimeLog.Warning("[GameBootstrapper] EmotionStateManager not found.");
        if (scenarioController == null)
            RuntimeLog.Warning("[GameBootstrapper] ScenarioController not found.");

        if (autoStartScenario && scenarioController != null)
            scenarioController.StartScenario();
    }

    void EnsureScenarioCommandRuntime()
    {
        var controller = scenarioController ? scenarioController : FindObjectOfType<ScenarioController>();
        if (controller == null)
        {
            RuntimeLog.Warning("[GameBootstrapper] ScenarioController not found. Skip command runtime setup.");
            return;
        }

        var executor = FindObjectOfType<ScenarioCommandExecutor>();
        var animTarget = FindObjectOfType<AnimationCommandTarget>();

        if (executor == null)
        {
            var go = new GameObject("ScenarioCommandRuntime");
            executor = go.AddComponent<ScenarioCommandExecutor>();
            RuntimeLog.Info("[GameBootstrapper] Auto-created ScenarioCommandExecutor.");
        }

        if (animTarget == null)
        {
            var go = executor.gameObject;
            animTarget = go.GetComponent<AnimationCommandTarget>();
            if (animTarget == null)
                animTarget = go.AddComponent<AnimationCommandTarget>();
            RuntimeLog.Info("[GameBootstrapper] Auto-created AnimationCommandTarget.");
        }

        executor.controller = controller;
        executor.animationTarget = animTarget;

        AutoBindLikelyAnimators(animTarget);
    }

    static void AutoBindLikelyAnimators(AnimationCommandTarget target)
    {
        if (target == null) return;

        var animators = FindObjectsOfType<Animator>();
        foreach (var anim in animators)
        {
            if (!anim) continue;
            string n = (anim.name ?? "").ToLowerInvariant();

            if (target.motherAnimator == null && (n.Contains("mom") || n.Contains("mother")))
                target.motherAnimator = anim;

            if (target.childAnimator == null && (n.Contains("kid") || n.Contains("child")))
                target.childAnimator = anim;
        }

        if (target.defaultAnimator == null)
            target.defaultAnimator = target.motherAnimator ? target.motherAnimator : target.childAnimator;

        string mom = target.motherAnimator ? target.motherAnimator.name : "null";
        string kid = target.childAnimator ? target.childAnimator.name : "null";
        string def = target.defaultAnimator ? target.defaultAnimator.name : "null";
        RuntimeLog.Info($"[GameBootstrapper] Animation auto-bind mother={mom}, child={kid}, default={def}");
    }

    void EnsureAssessmentRuntime()
    {
        if (!autoCreateScoreExport) return;

        if (!scoreManager)
            scoreManager = FindObjectOfType<ScenarioScoreManager>();
        if (!scoreManager)
        {
            var go = new GameObject("ScenarioAssessmentRuntime");
            scoreManager = go.AddComponent<ScenarioScoreManager>();
            RuntimeLog.Info("[GameBootstrapper] Auto-created ScenarioScoreManager.");
        }

        if (!scoreCsvExporter)
            scoreCsvExporter = FindObjectOfType<ScenarioScoreCsvExporter>();
        if (!scoreCsvExporter)
        {
            scoreCsvExporter = scoreManager.gameObject.AddComponent<ScenarioScoreCsvExporter>();
            RuntimeLog.Info("[GameBootstrapper] Auto-created ScenarioScoreCsvExporter.");
        }

        scoreManager.controller = scenarioController;
        scoreCsvExporter.scoreManager = scoreManager;
    }
}
