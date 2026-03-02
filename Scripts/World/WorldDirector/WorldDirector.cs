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
    [SerializeField] private Transform momAnchor;
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

    public void SnapKidToAnchor()
    {
        if (kid != null && kidAnchor != null)
            kid.transform.SetPositionAndRotation(kidAnchor.position, kidAnchor.rotation);
    }

    public void SnapMomToAnchor()
    {
        if (mom != null && momAnchor != null)
            mom.transform.SetPositionAndRotation(momAnchor.position, momAnchor.rotation);
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

    // -------------------------------------------------- Legacy compatibility for debug panels

    public void KidReactFear() => HandleEvent("Act1_KidFear");
    public void PrepareTempMeasure() => HandleEvent("Act1_TempMeasure");
    public void PrepareBPMeasure() => HandleEvent("Act1_BPMeasure");
    public void MomGestureCheer() => HandleEvent("Act1_MomClapCheer");

    public void ApplyEmotionScore(float score)
    {
        lastEmotionScore = Mathf.Clamp(score, 0f, 100f);
        if (kid == null) return;

        // Simple mapping: high score -> crying, mid -> disbelief, low -> idle
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
            // Map tension (-5~5) to 0~100 if provided; fallback to stage if tension is 0.
            float score = payload.tension != 0f
                ? Mathf.InverseLerp(-5f, 5f, payload.tension) * 100f
                : payload.stage * 25f;
            ApplyEmotionScore(score);
            return;
        }

        // Fallback: treat key as eventId for HandleEvent (e.g., Act1_* or interaction.*)
        HandleEvent(key);
    }
}
