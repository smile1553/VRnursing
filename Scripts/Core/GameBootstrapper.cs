using UnityEngine;

public class GameBootstrapper : MonoBehaviour
{
    [Header("Core References")]
    public EmotionStateManager emotionStateManager;

    [Header("Scenario")]
    public ScenarioController scenarioController;
    public ScenarioAsset defaultScenario;

    [Header("AI / Emotion")]
    public RunAI_Network runAiNetwork;

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
    }

    void Start()
    {
        if (runAiNetwork == null)
            Debug.LogWarning("[GameBootstrapper] RunAI_Network not found. Emotion feed will not start.");
        if (emotionStateManager == null)
            Debug.LogWarning("[GameBootstrapper] EmotionStateManager not found.");
        if (scenarioController == null)
            Debug.LogWarning("[GameBootstrapper] ScenarioController not found.");

        if (autoStartScenario && scenarioController != null)
            scenarioController.StartScenario();
    }
}
