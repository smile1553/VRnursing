using System.Collections;
using UnityEngine;

public class NurseryRhymeMusicController : MonoBehaviour
{
    private const string DefaultClipName = "headshoulderskneestoes";

    private static NurseryRhymeMusicController instance;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip nurseryRhymeClip;
    [SerializeField] private string resourcesClipName = DefaultClipName;
    [SerializeField] private float loopStartTime = 3f;
    [SerializeField] private float introVolume = 0.38f;
    [SerializeField] private float ambientVolume = 0.055f;
    [SerializeField] private float fadeToAmbientSeconds = 10f;

    private Coroutine fadeRoutine;

    public static void Play()
    {
        GetOrCreateInstance().PlayLoop();
    }

    private static NurseryRhymeMusicController GetOrCreateInstance()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<NurseryRhymeMusicController>(true);
        if (instance != null)
            return instance;

        GameObject controllerObject = new GameObject("NurseryRhymeMusicController");
        instance = controllerObject.AddComponent<NurseryRhymeMusicController>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ResolveReferences();
    }

    private void Update()
    {
        if (audioSource == null || nurseryRhymeClip == null || !audioSource.isPlaying)
            return;

        if (audioSource.time < Mathf.Max(0f, loopStartTime) - 0.05f)
            audioSource.time = Mathf.Min(Mathf.Max(0f, loopStartTime), nurseryRhymeClip.length - 0.05f);
    }

    private void PlayLoop()
    {
        ResolveReferences();

        if (audioSource == null || nurseryRhymeClip == null)
        {
            Debug.LogWarning("[NurseryRhymeMusicController] headshoulderskneestoes audio clip is missing.", this);
            return;
        }

        float startTime = Mathf.Clamp(loopStartTime, 0f, Mathf.Max(0f, nurseryRhymeClip.length - 0.05f));
        audioSource.clip = nurseryRhymeClip;
        audioSource.loop = true;
        audioSource.volume = introVolume;
        audioSource.time = startTime;

        if (!audioSource.isPlaying)
            audioSource.Play();

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeToAmbientRoutine());
    }

    private IEnumerator FadeToAmbientRoutine()
    {
        float startVolume = audioSource != null ? audioSource.volume : introVolume;
        float duration = Mathf.Max(0.01f, fadeToAmbientSeconds);
        float elapsed = 0f;

        while (audioSource != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(startVolume, ambientVolume, t);
            yield return null;
        }

        if (audioSource != null)
            audioSource.volume = ambientVolume;

        fadeRoutine = null;
    }

    private void ResolveReferences()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (nurseryRhymeClip == null)
            nurseryRhymeClip = Resources.Load<AudioClip>(resourcesClipName);

        if (nurseryRhymeClip == null)
            nurseryRhymeClip = FindAudioClipByName(resourcesClipName);
    }

    private static AudioClip FindAudioClipByName(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return null;

        AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (AudioClip clip in clips)
        {
            if (clip == null)
                continue;

            if (clip.name == clipName || clip.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return clip;
        }

        return null;
    }
}
