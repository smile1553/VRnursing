using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives MomActor and KidActor based on scripted events.
/// </summary>
public class WorldDirector : MonoBehaviour
{
    [SerializeField] private MomActor mom;
    [SerializeField] private KidActor kid;
    [Range(0f, 100f)] public float lastEmotionScore;

    [Header("Anchors (optional)")]
    [SerializeField] private Transform momStandAnchor;
    [SerializeField] private Transform momSitAnchor;
    [SerializeField] private Transform kidAnchor;

    void Reset()
    {
        mom = FindObjectOfType<MomActor>();
        kid = FindObjectOfType<KidActor>();
    }

    void Awake()
    {
        if (!mom) mom = FindObjectOfType<MomActor>();
        if (!kid) kid = FindObjectOfType<KidActor>();

        if (!mom) Debug.LogError("[WorldDirector] MomActor not found in scene.");
        if (!kid) Debug.LogError("[WorldDirector] KidActor not found in scene.");
    }

    // -------------------------------------------------- Public entry points

    public void InitAct1()
    {
        if (!mom || !kid) return;
        mom.SetTalking(false);
        kid.ClearAllLoops();
    }

    public void HandleEvent(string eventId)
    {
        if (!mom || !kid || string.IsNullOrEmpty(eventId)) return;

        switch (eventId)
        {
            case "Act1_Init":
                InitAct1();
                break;

            case "Act1_NurseEnter":
                StartCoroutine(MomSay(2f));
                break;

            case "Act1_KidFear":
                SetKidSingleLoop(kid.SetCrying);
                break;

            case "Act1_BreathingObserve":
                SetKidSingleLoop(kid.SetSleeping);
                break;

            case "Act1_StopFear":
                kid.ClearAllLoops();
                break;

            case "Act1_TempPrep":
                mom.DoPoint();
                kid.ClearAllLoops(); // optional: SetKidSingleLoop(kid.SetTurningHead);
                break;

            case "Act1_TempMeasure":
                mom.DoHoldForTemp();
                kid.DoRubbing();
                break;

            case "Act1_BPPrep":
                mom.DoPoint();
                SetKidSingleLoop(kid.SetKickingOut);
                break;

            case "Act1_BPMeasure":
                mom.DoHoldArmForBP();
                kid.DoDodge();
                break;

            case "Act1_MomClapCheer":
                mom.DoClap();
                break;
        }
    }

    public IEnumerator MomSay(float seconds)
    {
        if (!mom) yield break;
        mom.SetTalking(true);
        yield return new WaitForSeconds(seconds);
        mom.SetTalking(false);
    }

    // -------------------------------------------------- Helpers

    private void SetKidSingleLoop(System.Action<bool> loopSetter)
    {
        if (kid == null || loopSetter == null) return;
        kid.ClearAllLoops();
        loopSetter(true);
    }

    private bool ApplyKidEmotionState(string state)
    {
        if (kid == null || string.IsNullOrWhiteSpace(state)) return false;

        if (string.Equals(state, "Calm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Normal", StringComparison.OrdinalIgnoreCase))
        {
            kid.ClearAllLoops();
            return true;
        }

        if (string.Equals(state, "Uneasy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Fear", StringComparison.OrdinalIgnoreCase))
        {
            SetKidSingleLoop(kid.SetDisbelief);
            return true;
        }

        if (string.Equals(state, "Crying", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Cry", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Meltdown", StringComparison.OrdinalIgnoreCase))
        {
            SetKidSingleLoop(kid.SetCrying);
            return true;
        }

        return false;
    }

    // -------------------------------------------------- Legacy compatibility for debug panels

    public void KidReactFear() => HandleEvent("Act1_KidFear");
    public void PrepareTempMeasure() => HandleEvent("Act1_TempMeasure");
    public void PrepareBPMeasure() => HandleEvent("Act1_BPMeasure");
    public void MomGestureCheer() => HandleEvent("Act1_MomClapCheer");

    public void ApplyEmotionScore(float score)
    {
        lastEmotionScore = Mathf.Clamp(score, 0f, 100f);
        if (kid == null) return;

        if (lastEmotionScore >= 70f)
            SetKidSingleLoop(kid.SetCrying);
        else if (lastEmotionScore >= 40f)
            SetKidSingleLoop(kid.SetDisbelief);
        else
            kid.ClearAllLoops();
    }

    public void TriggerMomAction(MomActionType action)
    {
        if (mom == null) return;
        switch (action)
        {
            case MomActionType.Approach:
                mom.DoPoint(); // placeholder for approach gesture
                break;
            case MomActionType.Comfort:
                StartCoroutine(MomSay(1f));
                break;
            case MomActionType.ShowSticker:
                mom.DoClap();
                break;
            case MomActionType.Roleplay:
                mom.DoBow();
                break;
        }
    }

    public void TriggerMomAction(string actionKey)
    {
        TriggerMomAction(WorldActions.ParseMomAction(actionKey));
    }

    // -------------------------------------------------- Signal bridge compatibility
    public void ReceiveSignal(string key, SignalPayload payload)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (key == "emotion_score" && payload != null)
        {
            if (ApplyKidEmotionState(payload.kidEmotionState))
                return;

            float score = payload.tension != 0f
                ? Mathf.InverseLerp(-5f, 5f, payload.tension) * 100f
                : payload.stage * 25f;
            ApplyEmotionScore(score);
            return;
        }

        HandleEvent(key);
    }

    // -------------------------------------------------- Anchors
    public void SnapKidToAnchor()
    {
        if (kid != null && kidAnchor != null)
            kid.transform.SetPositionAndRotation(kidAnchor.position, kidAnchor.rotation);
    }

    public void SnapMomToStand()
    {
        if (mom != null && momStandAnchor != null)
            mom.transform.SetPositionAndRotation(momStandAnchor.position, momStandAnchor.rotation);
    }

    public void SnapMomToSit()
    {
        if (mom != null && momSitAnchor != null)
            mom.transform.SetPositionAndRotation(momSitAnchor.position, momSitAnchor.rotation);
    }
}
