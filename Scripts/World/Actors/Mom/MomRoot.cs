using UnityEngine;

public class MomRoot : ActorRoot
{
    [Header("Mom Parts")]
    public MomActionResponder actionResponder;

    protected override void Awake()
    {
        base.Awake();
        if (!actionResponder)
            actionResponder = GetComponentInChildren<MomActionResponder>();
    }
}
