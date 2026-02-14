using UnityEngine;

/// <summary>
/// Simple audio controller placeholder for world actions.
/// Add clip references and play logic as needed.
/// </summary>
public class AudioController : MonoBehaviour
{
    public AudioSource source;

    void Awake()
    {
        if (!source)
            source = GetComponent<AudioSource>();
    }

    public void PlayClip(AudioClip clip)
    {
        if (clip == null || source == null) return;
        source.PlayOneShot(clip);
    }
}
