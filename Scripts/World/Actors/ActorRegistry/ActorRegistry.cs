using UnityEngine;

/// <summary>
/// Lightweight lookup for Mom/Kid roots so WorldDirector does not rely on scene hard references.
/// </summary>
public class ActorRegistry : MonoBehaviour
{
    [Header("Scene Actors")]
    public MomRoot mom;
    public KidRoot kid;

    public MomRoot Mom => mom != null ? mom : (mom = FindObjectOfType<MomRoot>());
    public KidRoot Kid => kid != null ? kid : (kid = FindObjectOfType<KidRoot>());

    void Awake()
    {
        // Warm cache early to avoid FindObjectOfType during play.
        if (!mom) mom = FindObjectOfType<MomRoot>();
        if (!kid) kid = FindObjectOfType<KidRoot>();
    }

    public MomActionResponder GetMomResponder()
    {
        var root = Mom;
        if (root == null)
        {
            RuntimeLog.Warning("[ActorRegistry] MomRoot not found in scene.");
            return null;
        }
        return root.actionResponder;
    }

    public KidEmotionResponder GetKidResponder()
    {
        var root = Kid;
        if (root == null)
        {
            RuntimeLog.Warning("[ActorRegistry] KidRoot not found in scene.");
            return null;
        }
        return root.emotionResponder;
    }
}
