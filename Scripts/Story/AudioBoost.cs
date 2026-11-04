using UnityEngine;

// Makes a quiet clip louder than AudioSource.volume = 1 allows, by playing extra copies of it
// perfectly in sync (2 = twice as loud, 3 = three times...).
public static class AudioBoost
{
    private const string RootName = "__AudioBoost";

    public static void Play(AudioSource main, float loudness)
    {
        if (main == null || main.clip == null)
            return;

        int copies = Mathf.Clamp(Mathf.RoundToInt(loudness) - 1, 0, 4);
        Transform root = main.transform.Find(RootName);
        if (root == null)
        {
            if (copies == 0)
                return;
            root = new GameObject(RootName).transform;
            root.SetParent(main.transform, false);
        }

        while (root.childCount < copies)
        {
            GameObject child = new GameObject("Copy" + root.childCount);
            child.transform.SetParent(root, false);
            child.AddComponent<AudioSource>().playOnAwake = false;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            AudioSource copy = root.GetChild(i).GetComponent<AudioSource>();
            if (copy == null)
                continue;

            copy.Stop();
            if (i >= copies)
                continue;

            copy.clip = main.clip;
            copy.loop = main.loop;
            copy.spatialBlend = main.spatialBlend;
            copy.outputAudioMixerGroup = main.outputAudioMixerGroup;
            copy.pitch = main.pitch;
            copy.volume = 1f;
            copy.timeSamples = Mathf.Clamp(main.timeSamples, 0, Mathf.Max(0, main.clip.samples - 1));
            copy.Play();
        }
    }

    public static void Stop(AudioSource main)
    {
        if (main == null)
            return;

        Transform root = main.transform.Find(RootName);
        if (root == null)
            return;

        foreach (AudioSource copy in root.GetComponentsInChildren<AudioSource>(true))
            copy.Stop();
    }
}
