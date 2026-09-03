using UnityEngine;

public class MomAnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private bool logEvents = true;

    [Header("Animator State Names")]
    [SerializeField] private string standingIdleState = "MomStanding Idle";
    [SerializeField] private string talkingState = "Talking";

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void PlayStandingIdle()
    {
        PlayState(standingIdleState);
    }

    public void PlayTalking()
    {
        PlayState(talkingState);
    }

    public void PlayCustom(string stateName)
    {
        PlayState(stateName);
    }

    private void PlayState(string stateName)
    {
        if (animator == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] Animator is missing.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning("[MomAnimationPlayer] State name is empty.", this);
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(layerIndex, stateHash))
        {
            Debug.LogWarning($"[MomAnimationPlayer] Animator state not found: {stateName}", this);
            return;
        }

        if (logEvents)
            Debug.Log($"[MomAnimationPlayer] Play animation: {stateName}", this);

        animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex);
    }
}
