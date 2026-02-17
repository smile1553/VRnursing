using UnityEngine;

/// <summary>
/// Thin wrapper around Kid Animator parameters.
/// </summary>
public class KidActor : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Bool parameters (loops)")]
    [SerializeField] private string talkParam = "KidTalking";
    [SerializeField] private string cryParam = "KidCrying";
    [SerializeField] private string disbeliefParam = "KidDisbelief";
    [SerializeField] private string turningHeadParam = "KidTurningHead";
    [SerializeField] private string sleepingParam = "KidLayingSleep";
    [SerializeField] private string kickingOutParam = "KidKickingOut";
    [SerializeField] private string catchBearParam = "KidCatchBear";
    [SerializeField] private string bloodTestingParam = "KidBloodTesting";

    [Header("Trigger parameters (one-shots)")]
    [SerializeField] private string holdTempParam = "KidHoldStillForTemp";
    [SerializeField] private string holdBpParam = "KidHoldStillForBP";
    [SerializeField] private string cryOnceParam = "KidCry";
    [SerializeField] private string lookUpParam = "KidLookUp";
    [SerializeField] private string turnToMomParam = "KidTurnToMom";
    [SerializeField] private string dodgeParam = "KidDodge";
    [SerializeField] private string rubbingParam = "KidRubbing";

    // cached hashes
    int _talkHash, _cryHash, _disbeliefHash, _turnHeadHash, _sleepHash, _kickHash, _catchBearHash, _bloodTestHash;
    int _holdTempHash, _holdBpHash, _cryOnceHash, _lookUpHash, _turnToMomHash, _dodgeHash, _rubbingHash;

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
        _talkHash = Animator.StringToHash(talkParam);
        _cryHash = Animator.StringToHash(cryParam);
        _disbeliefHash = Animator.StringToHash(disbeliefParam);
        _turnHeadHash = Animator.StringToHash(turningHeadParam);
        _sleepHash = Animator.StringToHash(sleepingParam);
        _kickHash = Animator.StringToHash(kickingOutParam);
        _catchBearHash = Animator.StringToHash(catchBearParam);
        _bloodTestHash = Animator.StringToHash(bloodTestingParam);

        _holdTempHash = Animator.StringToHash(holdTempParam);
        _holdBpHash = Animator.StringToHash(holdBpParam);
        _cryOnceHash = Animator.StringToHash(cryOnceParam);
        _lookUpHash = Animator.StringToHash(lookUpParam);
        _turnToMomHash = Animator.StringToHash(turnToMomParam);
        _dodgeHash = Animator.StringToHash(dodgeParam);
        _rubbingHash = Animator.StringToHash(rubbingParam);
    }

    // ---------- Loop states (bools) ----------
    public void SetTalking(bool value) => animator.SetBool(_talkHash, value);
    public void SetCrying(bool value) => animator.SetBool(_cryHash, value);
    public void SetDisbelief(bool value) => animator.SetBool(_disbeliefHash, value);
    public void SetTurningHead(bool value) => animator.SetBool(_turnHeadHash, value);
    public void SetSleeping(bool value) => animator.SetBool(_sleepHash, value);
    public void SetKickingOut(bool value) => animator.SetBool(_kickHash, value);
    public void SetCatchBear(bool value) => animator.SetBool(_catchBearHash, value);
    public void SetBloodTesting(bool value) => animator.SetBool(_bloodTestHash, value);

    public void ClearAllLoops()
    {
        animator.SetBool(_talkHash, false);
        animator.SetBool(_cryHash, false);
        animator.SetBool(_disbeliefHash, false);
        animator.SetBool(_turnHeadHash, false);
        animator.SetBool(_sleepHash, false);
        animator.SetBool(_kickHash, false);
        animator.SetBool(_catchBearHash, false);
        animator.SetBool(_bloodTestHash, false);
    }

    // ---------- One-shot triggers ----------
    public void DoCry() => animator.SetTrigger(_cryOnceHash);
    public void DoLookUp() => animator.SetTrigger(_lookUpHash);
    public void DoTurnToMom() => animator.SetTrigger(_turnToMomHash);
    public void DoHoldStillForTemp() => animator.SetTrigger(_holdTempHash);
    public void DoHoldStillForBP() => animator.SetTrigger(_holdBpHash);
    public void DoDodge() => animator.SetTrigger(_dodgeHash);
    public void DoRubbing() => animator.SetTrigger(_rubbingHash);
}
