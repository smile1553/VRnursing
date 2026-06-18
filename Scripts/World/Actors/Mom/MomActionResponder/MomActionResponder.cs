using UnityEngine;

public class MomActionResponder : MonoBehaviour
{
    [Header("Animation Triggers")]
    public string approachTrigger = "MomApproach";
    public string comfortTrigger = "MomComfort";
    public string stickerTrigger = "MomSticker";
    public string roleplayTrigger = "MomRoleplay";

    public Animator animator;

    void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    public void PlayAction(MomActionType action)
    {
        if (action == MomActionType.None)
        {
            RuntimeLog.Warning("[MomAction] None action requested; skipped.");
            return;
        }

        if (!animator)
        {
            RuntimeLog.Warning($"[MomAction] Missing animator, logging only: {action}");
            return;
        }

        switch (action)
        {
            case MomActionType.Approach:
                animator.SetTrigger(approachTrigger);
                break;
            case MomActionType.Comfort:
                animator.SetTrigger(comfortTrigger);
                break;
            case MomActionType.ShowSticker:
                animator.SetTrigger(stickerTrigger);
                break;
            case MomActionType.Roleplay:
                animator.SetTrigger(roleplayTrigger);
                break;
        }

        RuntimeLog.Info($"[MomAction] Triggered {action}");
    }
}
