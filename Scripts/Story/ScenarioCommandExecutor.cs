using UnityEngine;

public class ScenarioCommandExecutor : MonoBehaviour
{
    public ScenarioController controller;
    public AnimationCommandTarget animationTarget;
    public AudioCommandTarget audioTarget;
    public TimelineCommandTarget timelineTarget;
    public CameraCommandTarget cameraTarget;
    [Header("Auto Animation Fallback")]
    public bool autoAnimationFallback = true;
    public bool fallbackUseStepId = true;
    public bool fallbackUseSpeaker = true;

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
        if (step == null) return;

        bool hasAnimationCommand = false;
        if (step.commands != null)
        {
            foreach (var cmd in step.commands)
            {
                if (cmd != null && cmd.type == ScenarioCommandType.PlayAnimation)
                    hasAnimationCommand = true;
                Execute(cmd);
            }
        }

        if (!autoAnimationFallback || animationTarget == null || hasAnimationCommand)
            return;

        if (fallbackUseStepId && !string.IsNullOrWhiteSpace(step.id))
        {
            if (animationTarget.Play($"step:{step.id}"))
                return;
            if (animationTarget.Play(step.id))
                return;
        }

        if (fallbackUseSpeaker)
        {
            string speaker = step.speaker.ToString();
            if (animationTarget.Play($"speaker:{speaker}"))
                return;
            animationTarget.Play(speaker);
        }
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
                RuntimeLog.Info($"[ScenarioCommand] 未處理的命令 {cmd.type} payload={cmd.payload}");
                break;
        }
    }
}
