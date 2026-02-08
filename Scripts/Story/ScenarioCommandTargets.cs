using System;
using UnityEngine;
using UnityEngine.Playables;

public class AnimationCommandTarget : MonoBehaviour
{
    public AnimationBinding[] bindings;

    public void Play(string command)
    {
        var binding = FindBinding(command);
        if (binding == null)
        {
            Debug.LogWarning($"[AnimationCommand] 找不到對應命令 {command}");
            return;
        }
        if (!binding.animator)
            return;
        if (!string.IsNullOrEmpty(binding.triggerName))
            binding.animator.SetTrigger(binding.triggerName);
        else if (!string.IsNullOrEmpty(binding.stateName))
            binding.animator.Play(binding.stateName);
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
        var target = point.transform;
        if (!target)
            return;

        if (smoothMove)
        {
            _currentTarget = target;
        }
        else
        {
            rig.position = target.position;
            rig.rotation = target.rotation;
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
