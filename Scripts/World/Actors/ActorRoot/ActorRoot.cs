using UnityEngine;

public class ActorRoot : MonoBehaviour
{
    [Header("Actor Components")]
    [SerializeField] protected Animator animator;

    protected virtual void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    protected void SetTriggerSafe(string trigger)
    {
        if (animator == null)
        {
            RuntimeLog.Warning($"[{nameof(ActorRoot)}] Missing animator on {name}, trigger {trigger} skipped.");
            return;
        }
        if (string.IsNullOrEmpty(trigger))
        {
            RuntimeLog.Warning($"[{nameof(ActorRoot)}] Empty trigger requested on {name}.");
            return;
        }
        animator.ResetTrigger(trigger);
        animator.SetTrigger(trigger);
    }
}
