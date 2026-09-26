using UnityEngine;

public class YayaAnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private bool logEvents = true;

    [Header("Animator State Names")]
    [SerializeField] private string sittingIdleState = "sitting_idle";
    [SerializeField] private string sittingDisbeliefState = "sitting_disbelief";
    [SerializeField] private string sittingRubbingArmState = "sitting_rubbing_arm";
    [SerializeField] private string layingSleepingState = "laying_sleeping";
    [SerializeField] private string layingDownState = "lying_down";
    [SerializeField] private string kickingOutState = "kicking_out";

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void PlaySittingIdle()
    {
        PlayState(sittingIdleState);
    }

    public void PlaySittingDisbelief()
    {
        PlayState(sittingDisbeliefState);
    }

    public void PlaySittingRubbingArm()
    {
        PlayState(sittingRubbingArmState);
    }

    public void PlayLayingSleeping()
    {
        PlayState(layingSleepingState);
    }

    public void PlayLayingDown()
    {
        PlayState(layingDownState);
    }

    public void PlayKickingOut()
    {
        PlayState(kickingOutState);
    }

    public void PlayCustom(string stateName)
    {
        PlayState(stateName);
    }

    private void PlayState(string stateName)
    {
        if (animator == null)
        {
            Debug.LogWarning("[YayaAnimationPlayer] Animator is missing.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning("[YayaAnimationPlayer] State name is empty.", this);
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(layerIndex, stateHash))
        {
            Debug.LogWarning($"[YayaAnimationPlayer] Animator state not found: {stateName}", this);
            return;
        }

        if (logEvents)
            Debug.Log($"[YayaAnimationPlayer] Play animation: {stateName}", this);

        animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex);
    }
}
