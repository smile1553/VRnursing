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
    [SerializeField] private float introVolume = 0.14f;
    [SerializeField] private float ambientVolume = 0.045f;
    [SerializeField] private float fadeInSeconds = 1.5f;
    [SerializeField] private float fadeToAmbientSeconds = 10f;
    [SerializeField] private float fadeOutSeconds = 1.5f;

    private Coroutine fadeRoutine;

    public static void Play()
    {
        GetOrCreateInstance().PlayLoop();
    }

    public static void Stop()
    {
        if (instance == null)
            instance = FindObjectOfType<NurseryRhymeMusicController>(true);

        instance?.StopLoop();
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

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        float startTime = Mathf.Clamp(loopStartTime, 0f, Mathf.Max(0f, nurseryRhymeClip.length - 0.05f));
        audioSource.clip = nurseryRhymeClip;
        audioSource.loop = true;
        audioSource.volume = 0f;
        audioSource.time = startTime;

        if (!audioSource.isPlaying)
            audioSource.Play();

        fadeRoutine = StartCoroutine(FadeInThenAmbientRoutine());
    }

    private void StopLoop()
    {
        ResolveReferences();

        if (audioSource == null)
            return;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (!audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.volume = 0f;
            return;
        }

        fadeRoutine = StartCoroutine(FadeOutAndStopRoutine());
    }

    private IEnumerator FadeInThenAmbientRoutine()
    {
        float fadeInDuration = Mathf.Max(0.01f, fadeInSeconds);
        float elapsed = 0f;

        while (audioSource != null && elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            audioSource.volume = Mathf.Lerp(0f, introVolume, t);
            yield return null;
        }

        if (audioSource == null)
        {
            fadeRoutine = null;
            yield break;
        }

        audioSource.volume = introVolume;
        float fadeAmbientDuration = Mathf.Max(0.01f, fadeToAmbientSeconds);
        elapsed = 0f;

        while (audioSource != null && elapsed < fadeAmbientDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeAmbientDuration);
            audioSource.volume = Mathf.Lerp(introVolume, ambientVolume, t);
            yield return null;
        }

        if (audioSource != null)
            audioSource.volume = ambientVolume;

        fadeRoutine = null;
    }

    private IEnumerator FadeOutAndStopRoutine()
    {
        float startVolume = audioSource != null ? audioSource.volume : 0f;
        float duration = Mathf.Max(0.01f, fadeOutSeconds);
        float elapsed = 0f;

        while (audioSource != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.volume = 0f;
        }

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
