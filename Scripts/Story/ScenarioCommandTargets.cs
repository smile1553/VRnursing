using System;
using UnityEngine;
using UnityEngine.Playables;

public class AnimationCommandTarget : MonoBehaviour
{
    public Animator defaultAnimator;
    public bool enableConventionFallback = true;
    [Header("Speaker Auto Route")]
    public Animator motherAnimator;
    public Animator childAnimator;
    public string speakerTriggerName = "Talk";
    public bool useSpeakerTrigger = true;
    public bool useSpeakerBoolParam = true;
    public string motherSpeakingParam = "MomTalking";
    public string childSpeakingParam = "KidTalking";
    public AnimationBinding[] bindings;

    public bool Play(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;

        // 支援 bool 開關：例如 "MomTalking=true"
        int eq = command.IndexOf('=');
        if (eq > 0)
        {
            string param = command.Substring(0, eq).Trim();
            string val = command.Substring(eq + 1).Trim();
            if (bool.TryParse(val, out bool b))
            {
                var bindingForBool = FindBinding(param) ?? FindBinding(command);
                var anim = bindingForBool != null ? bindingForBool.animator : null;
                if (anim)
                {
                    anim.SetBool(param, b);
                    return true;
                }
                if (TrySetBoolOnAny(param, b))
                    return true;
            }
        }

        var binding = FindBinding(command);
        if (binding == null)
        {
            if (enableConventionFallback && TryPlayByConvention(command))
                return true;
            Debug.LogWarning($"[AnimationCommand] 找不到對應命令 {command}");
            return false;
        }
        if (!binding.animator)
            return false;
        if (!string.IsNullOrEmpty(binding.triggerName))
        {
            binding.animator.SetTrigger(binding.triggerName);
            return true;
        }
        else if (!string.IsNullOrEmpty(binding.stateName))
        {
            binding.animator.Play(binding.stateName);
            return true;
        }
        return false;
    }

    AnimationBinding FindBinding(string command)
    {
        if (bindings == null) return null;
        for (int i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b != null && b.Matches(command))
                return b;
        }
        return null;
    }

    bool TryPlayByConvention(string command)
    {
        string candidate = command?.Trim();
        if (string.IsNullOrEmpty(candidate))
            return false;

        string prefix = "";
        int sep = candidate.IndexOf(':');
        if (sep > 0 && sep + 1 < candidate.Length)
        {
            prefix = candidate.Substring(0, sep).Trim().ToLowerInvariant();
            candidate = candidate.Substring(sep + 1).Trim();
        }

        if (string.IsNullOrEmpty(candidate))
            return false;

        var anim = ResolveAnimator(prefix, candidate);
        if (!anim)
            return false;

        if (prefix == "speaker" && useSpeakerTrigger && !string.IsNullOrWhiteSpace(speakerTriggerName))
        {
            if (HasTrigger(anim, speakerTriggerName))
            {
                anim.SetTrigger(speakerTriggerName);
                return true;
            }
            if (anim.HasState(0, Animator.StringToHash(speakerTriggerName)))
            {
                anim.Play(speakerTriggerName);
                return true;
            }
        }

        if (prefix == "speaker" && useSpeakerBoolParam)
        {
            string speakingParam = GetSpeakerSpeakingParam(candidate);
            if (!string.IsNullOrWhiteSpace(speakingParam) && HasBool(anim, speakingParam))
            {
                anim.SetBool(speakingParam, true);
                return true;
            }
        }

        if (anim.HasState(0, Animator.StringToHash(candidate)))
        {
            anim.Play(candidate);
            return true;
        }
        if (HasTrigger(anim, candidate))
        {
            anim.SetTrigger(candidate);
            return true;
        }

        return false;
    }

    Animator ResolveAnimator(string prefix, string key)
    {
        if (prefix == "speaker")
            return ResolveSpeakerAnimator(key);
        return defaultAnimator;
    }

    Animator ResolveSpeakerAnimator(string speaker)
    {
        string s = (speaker ?? "").Trim().ToLowerInvariant();
        switch (s)
        {
            case "mother":
                return motherAnimator ? motherAnimator : defaultAnimator;
            case "child":
                return childAnimator ? childAnimator : defaultAnimator;
            default:
                return defaultAnimator;
        }
    }

    static bool HasTrigger(Animator animator, string name)
    {
        if (!animator || string.IsNullOrEmpty(name))
            return false;

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.type == AnimatorControllerParameterType.Trigger &&
                string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static bool HasBool(Animator animator, string name)
    {
        if (!animator || string.IsNullOrEmpty(name))
            return false;

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.type == AnimatorControllerParameterType.Bool &&
                string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    bool TrySetBoolOnAny(string param, bool value)
    {
        if (string.IsNullOrWhiteSpace(param))
            return false;

        var animators = new Animator[] { motherAnimator, childAnimator, defaultAnimator };
        for (int i = 0; i < animators.Length; i++)
        {
            var anim = animators[i];
            if (!anim) continue;
            if (!HasBool(anim, param)) continue;
            anim.SetBool(param, value);
            return true;
        }
        return false;
    }

    string GetSpeakerSpeakingParam(string speaker)
    {
        string s = (speaker ?? "").Trim().ToLowerInvariant();
        switch (s)
        {
            case "mother": return motherSpeakingParam;
            case "child": return childSpeakingParam;
            default: return string.Empty;
        }
    }
}

[Serializable]
public class AnimationBinding
{
    public string command;
    public Animator animator;
    public string triggerName;
    public string stateName;

    public bool Matches(string other)
    {
        return !string.IsNullOrEmpty(command) && string.Equals(command, other, StringComparison.OrdinalIgnoreCase);
    }
}

public class AudioCommandTarget : MonoBehaviour
{
    public AudioSource defaultSource;
    public AudioBinding[] bindings;

    public void Play(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        var binding = FindBinding(command);
        if (binding == null)
        {
            Debug.LogWarning($"[AudioCommand] 找不到對應命令 {command}");
            return;
        }
        var src = binding.source ? binding.source : defaultSource;
        if (!src)
            return;
        if (binding.clip)
            src.PlayOneShot(binding.clip);
        else
            src.Play();
    }

    AudioBinding FindBinding(string command)
    {
        if (bindings == null) return null;
        for (int i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b != null && b.Matches(command))
                return b;
        }
        return null;
    }
}

[Serializable]
public class AudioBinding
{
    public string command;
    public AudioSource source;
    public AudioClip clip;

    public bool Matches(string other)
    {
        return !string.IsNullOrEmpty(command) && string.Equals(command, other, StringComparison.OrdinalIgnoreCase);
    }
}

public class TimelineCommandTarget : MonoBehaviour
{
    public TimelineBinding[] bindings;

    public void Play(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        var binding = FindBinding(command);
        if (binding?.director)
            binding.director.Play();
    }

    public void TriggerVfx(string command)
    {
        Play(command); // 預設同 Play 行為，可之後細分
    }

    TimelineBinding FindBinding(string command)
    {
        if (bindings == null) return null;
        for (int i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b != null && b.Matches(command))
                return b;
        }
        return null;
    }
}

[Serializable]
public class TimelineBinding
{
    public string command;
    public PlayableDirector director;

    public bool Matches(string other)
    {
        return !string.IsNullOrEmpty(command) && string.Equals(command, other, StringComparison.OrdinalIgnoreCase);
    }
}

public class CameraCommandTarget : MonoBehaviour
{
    public Transform rig;
    public CameraPoint[] points;
    public float moveSpeed = 3f;
    public bool smoothMove = true;

    Transform _currentTarget;

    void Awake()
    {
        if (!rig && Camera.main)
            rig = Camera.main.transform;
    }

    void Update()
    {
        if (!smoothMove || !_currentTarget || !rig)
            return;
        rig.position = Vector3.Lerp(rig.position, _currentTarget.position, Time.deltaTime * moveSpeed);
        rig.rotation = Quaternion.Slerp(rig.rotation, _currentTarget.rotation, Time.deltaTime * moveSpeed);
    }

    public void JumpTo(string id)
    {
        var point = FindPoint(id);
        if (point == null)
        {
            Debug.LogWarning($"[CameraCommand] 找不到 {id}");
            return;
        }
        if (!rig)
            return;
        if (smoothMove)
        {
            _currentTarget = point.transform;
        }
        else
        {
            rig.position = point.transform.position;
            rig.rotation = point.transform.rotation;
        }
    }

    CameraPoint FindPoint(string id)
    {
        if (points == null) return null;
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            if (p != null && p.Matches(id))
                return p;
        }
        return null;
    }
}

[Serializable]
public class CameraPoint
{
    public string id;
    public Transform transform;

    public bool Matches(string other)
    {
        return transform && !string.IsNullOrEmpty(id) && string.Equals(id, other, StringComparison.OrdinalIgnoreCase);
    }
}
