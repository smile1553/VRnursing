using UnityEngine;

/// <summary>
/// Thin wrapper around Mom Animator parameters.
/// </summary>
public class MomActor : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Bool parameters (loops)")]
    [SerializeField] private string talkingParam = "MomTalking";
    [SerializeField] private string sittingIdleParam = "MomSittingIdle";
    [SerializeField] private string bloodParam = "MomBlood";

    [Header("Trigger parameters (one-shots)")]
    [SerializeField] private string clapParam = "MomClap";
    [SerializeField] private string pointParam = "MomPoint";
    [SerializeField] private string bowParam = "MomBow";
    [SerializeField] private string helpLieDownParam = "MomHelpLieDown";
    [SerializeField] private string holdForTempParam = "MomHoldForTemp";
    [SerializeField] private string holdArmForBpParam = "MomHoldArmForBP";
    [SerializeField] private string sittingParam = "MomSitting";

    int _talkingHash, _sittingIdleHash, _bloodHash;
    int _clapHash, _pointHash, _bowHash, _helpLieDownHash, _holdTempHash, _holdBpHash, _sittingHash;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        CacheHashes();
    }

    void OnValidate()
    {
        CacheHashes();
    }

    void CacheHashes()
    {
        _talkingHash = Animator.StringToHash(talkingParam);
        _sittingIdleHash = Animator.StringToHash(sittingIdleParam);
        _bloodHash = Animator.StringToHash(bloodParam);

        _clapHash = Animator.StringToHash(clapParam);
        _pointHash = Animator.StringToHash(pointParam);
        _bowHash = Animator.StringToHash(bowParam);
        _helpLieDownHash = Animator.StringToHash(helpLieDownParam);
        _holdTempHash = Animator.StringToHash(holdForTempParam);
        _holdBpHash = Animator.StringToHash(holdArmForBpParam);
        _sittingHash = Animator.StringToHash(sittingParam);
    }

    public void SetTalking(bool value) => animator.SetBool(_talkingHash, value);
    public void SetSittingIdle(bool value) => animator.SetBool(_sittingIdleHash, value);
    public void SetBlood(bool value) => animator.SetBool(_bloodHash, value);

    public void DoClap() => animator.SetTrigger(_clapHash);
    public void DoPoint() => animator.SetTrigger(_pointHash);
    public void DoBow() => animator.SetTrigger(_bowHash);
    public void DoHelpLieDown() => animator.SetTrigger(_helpLieDownHash);
    public void DoHoldForTemp() => animator.SetTrigger(_holdTempHash);
    public void DoHoldArmForBP() => animator.SetTrigger(_holdBpHash);
    public void DoSitting() => animator.SetTrigger(_sittingHash);
}
