using UnityEngine;

public class ScenarioCommandExecutor : MonoBehaviour
{
    public ScenarioController controller;
    public AnimationCommandTarget animationTarget;
    public AudioCommandTarget audioTarget;
    public TimelineCommandTarget timelineTarget;
    public CameraCommandTarget cameraTarget;

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
        if (!animationTarget)
            animationTarget = FindObjectOfType<AnimationCommandTarget>();
        if (!audioTarget)
            audioTarget = FindObjectOfType<AudioCommandTarget>();
        if (!timelineTarget)
            timelineTarget = FindObjectOfType<TimelineCommandTarget>();
        if (!cameraTarget)
            cameraTarget = FindObjectOfType<CameraCommandTarget>();
    }

    void OnEnable()
    {
        if (controller != null)
            controller.StepChanged += HandleStep;
    }

    void OnDisable()
    {
        if (controller != null)
            controller.StepChanged -= HandleStep;
    }

    void HandleStep(ScenarioStep step)
    {
        if (step?.commands == null) return;
        foreach (var cmd in step.commands)
            Execute(cmd);
    }

    void Execute(ScenarioCommand cmd)
    {
        if (cmd == null) return;
        switch (cmd.type)
        {
            case ScenarioCommandType.PlayAnimation:
                animationTarget?.Play(cmd.payload);
                break;
            case ScenarioCommandType.PlayTimeline:
                timelineTarget?.Play(cmd.payload);
                break;
            case ScenarioCommandType.PlayAudio:
                audioTarget?.Play(cmd.payload);
                break;
            case ScenarioCommandType.MoveCamera:
                cameraTarget?.JumpTo(cmd.payload);
                break;
            case ScenarioCommandType.TriggerVfx:
                timelineTarget?.TriggerVfx(cmd.payload);
                break;
            default:
                Debug.Log($"[ScenarioCommand] 未處理的命令 {cmd.type} payload={cmd.payload}");
                break;
        }
    }
}
