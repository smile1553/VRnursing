using System;

[Serializable]
public class SignalPayload
{
    public string stepId;
    public int choiceIndex;
    public bool correct;
    public int stage;
    public float tension;
    public string targetId;

    public override string ToString()
    {
        return $"{{stepId={stepId}, choiceIndex={choiceIndex}, correct={correct}, stage={stage}, tension={tension}, targetId={targetId}}}";
    }
}
