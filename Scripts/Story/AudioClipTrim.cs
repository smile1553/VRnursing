using System.Collections.Generic;
using UnityEngine;

// Length of the audible part of an AudioClip (trailing silence removed). Cached per clip.
// Falls back to clip.length when the sample data cannot be read (e.g. streaming clips).
public static class AudioClipTrim
{
    private static readonly Dictionary<AudioClip, float> Cache = new Dictionary<AudioClip, float>();

    public static float GetAudibleLength(AudioClip clip, float threshold = 0.02f)
    {
        if (clip == null)
            return 0f;
        if (Cache.TryGetValue(clip, out float cached))
            return cached;

        float result = clip.length;
        try
        {
            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();

            int channels = Mathf.Max(1, clip.channels);
            float[] data = new float[clip.samples * channels];
            if (clip.loadState == AudioDataLoadState.Loaded && clip.GetData(data, 0))
            {
                int last = -1;
                for (int i = data.Length - 1; i >= 0; i--)
                {
                    if (Mathf.Abs(data[i]) > threshold)
                    {
                        last = i;
                        break;
                    }
                }
                if (last >= 0)
                    result = Mathf.Min(clip.length, (last / channels + 1) / (float)clip.frequency + 0.05f);
            }
        }
        catch (System.Exception)
        {
            result = clip.length;
        }

        Cache[clip] = result;
        return result;
    }
}
