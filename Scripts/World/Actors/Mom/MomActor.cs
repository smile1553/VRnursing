using UnityEngine;

/// <summary>
/// Thin wrapper around Mom Animator parameters.
/// </summary>
public class MomActor : MonoBehaviour
{
    [SerializeField] private Animator animator;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
    }

    public void SetTalking(bool value) => animator.SetBool("MomTalking", value);
    public void DoClap() => animator.SetTrigger("MomClap");
    public void DoPoint() => animator.SetTrigger("MomPoint");
    public void DoBow() => animator.SetTrigger("MomBow");
    public void DoHelpLieDown() => animator.SetTrigger("MomHelpLieDown");
    public void DoHoldForTemp() => animator.SetTrigger("MomHoldForTemp");
    public void DoHoldArmForBP() => animator.SetTrigger("MomHoldArmForBP");
}
