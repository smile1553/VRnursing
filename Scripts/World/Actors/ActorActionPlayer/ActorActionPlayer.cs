using UnityEngine;

/// <summary>
/// Placeholder actor action player. Extend later to drive animation timeline or sequence.
/// </summary>
public class ActorActionPlayer : MonoBehaviour
{
    public Animator animator;

    void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// Safely set a trigger on the assigned animator.
    /// </summary>
    public void PlayTrigger(string triggerName)
    {
        if (!animator || string.IsNullOrEmpty(triggerName)) return;
        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }
}
