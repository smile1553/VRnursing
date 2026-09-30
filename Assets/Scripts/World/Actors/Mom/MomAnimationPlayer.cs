using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Animations;
using UnityEngine.Playables;

public class MomAnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private bool logEvents = true;

    [Header("Animator State Names")]
    [SerializeField] private string angryState = "Angry";
    [SerializeField] private string clappingState = "Clapping";
    [SerializeField] private string walkStartState = "Female Start Walking";
    [SerializeField] private string standingIdleState = "MomStanding Idle";
    [SerializeField] private string pointingState = "Pointing";
    [SerializeField] private string quickBowState = "Quick Informal Bow";
    [SerializeField] private string talkingState = "Talking";
    [SerializeField] private string bendState = "Armature|MomBend";

    [Header("Clip Fallbacks")]
    [SerializeField] private AnimationClip momBendClip;

    private PlayableGraph clipGraph;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void OnDisable()
    {
        StopClipFallback();
    }

    private void OnDestroy()
    {
        StopClipFallback();
    }

    public void PlayAngry()
    {
        PlayState(angryState);
    }

    public void PlayClapping()
    {
        PlayFirstAvailableState(clappingState, "Base Layer.Clapping", "Clapping");
    }

    public void PlayWalkStart()
    {
        PlayState(walkStartState);
    }

    public void PlayStandingIdle()
    {
        PlayState(standingIdleState);
    }

    public void PlayPointing()
    {
        PlayState(pointingState);
    }

    public void PlayQuickBow()
    {
        PlayState(quickBowState);
    }

    public void PlayTalking()
    {
        PlayState(talkingState);
    }

    public void PlayBend()
    {
        if (PlayFirstAvailableStateDirect(bendState, "Base Layer.Listen_Mom", "Listen_Mom", "Base Layer.Armature|MomBend", "Armature|MomBend", "Base Layer.MomBend", "MomBend", "Mom Bend", "Mom_Bend", "Base Layer.Mom_Bend"))
            return;

        PlayClipFallback(ref momBendClip, true, "Listen_Mom", "MomBend", "Armature|MomBend", "Mom Bend", "Mom_Bend");
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

        StopClipFallback();
        if (logEvents)
            Debug.Log($"[MomAnimationPlayer] Play animation: {stateName}", this);

        animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex);
    }

    private bool PlayFirstAvailableState(params string[] stateNames)
    {
        if (animator == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] Animator is missing.", this);
            return false;
        }

        foreach (string stateName in stateNames)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(layerIndex, stateHash))
                continue;

            StopClipFallback();
            if (logEvents)
                Debug.Log($"[MomAnimationPlayer] Play animation: {stateName}", this);

            animator.CrossFadeInFixedTime(stateHash, fadeDuration, layerIndex);
            return true;
        }

        Debug.LogWarning("[MomAnimationPlayer] None of the requested animator states were found.", this);
        return false;
    }

    private bool PlayFirstAvailableStateDirect(params string[] stateNames)
    {
        if (animator == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] Animator is missing.", this);
            return false;
        }

        foreach (string stateName in stateNames)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(layerIndex, stateHash))
                continue;

            StopClipFallback();
            if (logEvents)
                Debug.Log($"[MomAnimationPlayer] Play animation: {stateName}", this);

            animator.Play(stateHash, layerIndex, 0f);
            animator.Update(0f);
            return true;
        }

        Debug.LogWarning("[MomAnimationPlayer] None of the requested animator states were found. Trying clip fallback.", this);
        return false;
    }

    private void PlayClipFallback(ref AnimationClip assignedClip, bool loop, params string[] clipNames)
    {
        if (animator == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] Animator is missing.", this);
            return;
        }

        if (assignedClip == null)
            assignedClip = FindAnimationClipByName(clipNames);

        if (assignedClip == null)
        {
            Debug.LogWarning("[MomAnimationPlayer] AnimationClip fallback not found. Drag MomBend clip into Mom Bend Clip.", this);
            return;
        }

        StopClipFallback();
        if (logEvents)
            Debug.Log($"[MomAnimationPlayer] Play clip fallback: {assignedClip.name}", this);

        clipGraph = PlayableGraph.Create($"{name}_{assignedClip.name}_Fallback");
        AnimationClipPlayable playable = AnimationClipPlayable.Create(clipGraph, assignedClip);
        playable.SetApplyFootIK(false);
        playable.SetDuration(assignedClip.length);
        playable.SetTime(0d);
        playable.SetSpeed(1d);
        if (loop)
            playable.SetDuration(double.PositiveInfinity);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(clipGraph, "Animation", animator);
        output.SetSourcePlayable(playable);
        clipGraph.Play();
    }

    private void StopClipFallback()
    {
        if (clipGraph.IsValid())
            clipGraph.Destroy();
    }

    private static AnimationClip FindAnimationClipByName(params string[] clipNames)
    {
        AnimationClip loadedClip = FindLoadedAnimationClipByName(clipNames);
        if (loadedClip != null)
            return loadedClip;

#if UNITY_EDITOR
        return FindEditorAnimationClipByName(clipNames);
#else
        return null;
#endif
    }

    private static AnimationClip FindLoadedAnimationClipByName(params string[] clipNames)
    {
        AnimationClip[] clips = Resources.FindObjectsOfTypeAll<AnimationClip>();
        foreach (string rawName in clipNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            foreach (AnimationClip clip in clips)
            {
                if (clip != null && clip.name.IndexOf(rawName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    private static AnimationClip FindEditorAnimationClipByName(params string[] clipNames)
    {
        foreach (string rawName in clipNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            string[] guids = AssetDatabase.FindAssets($"{rawName} t:AnimationClip");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip != null && clip.name.IndexOf(rawName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return clip;
                }
            }
        }
        return FindEditorAnimationClipByModelName(clipNames);
    }
    private static AnimationClip FindEditorAnimationClipByModelName(params string[] clipNames)
    {
        foreach (string rawName in clipNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            string[] guids = AssetDatabase.FindAssets($"{rawName} t:Model");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip != null && !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                        return clip;
                }
            }
        }

        return null;
    }
#endif
}