using UnityEngine;

public class KidRoot : ActorRoot
{
    [Header("Kid Parts")]
    public KidEmotionResponder emotionResponder;

    protected override void Awake()
    {
        base.Awake();
        if (!emotionResponder)
            emotionResponder = GetComponentInChildren<KidEmotionResponder>();
    }
}
