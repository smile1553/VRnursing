using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class MicrophoneDiagnostic : MonoBehaviour
{
    public int microphoneDeviceIndex = -1;
    public int requestedSampleRate = 16000;
    public int recordSeconds = 3;
    public bool autoRunOnStart = true;

    string micDevice;
    int activeSampleRate;

    IEnumerator Start()
    {
        if (!autoRunOnStart)
            yield break;

        yield return RunDiagnostic();
    }

    [ContextMenu("Run Microphone Diagnostic")]
    public void RunFromInspector()
    {
        StartCoroutine(RunDiagnostic());
    }

    IEnumerator RunDiagnostic()
    {
        Debug.Log("[MicDiag] starting diagnostic");

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.Log("[MicDiag] requesting microphone authorization");
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }

        bool authorized = Application.HasUserAuthorization(UserAuthorization.Microphone);
        Debug.Log("[MicDiag] microphone authorization = " + authorized);
        if (!authorized)
        {
            Debug.LogError("[MicDiag] microphone authorization denied");
            yield break;
        }

        string[] devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogError("[MicDiag] no microphone devices");
            yield break;
        }

        Debug.Log("[MicDiag] devices = " + string.Join(" | ", devices));

        if (microphoneDeviceIndex >= 0)
        {
            int idx = Mathf.Clamp(microphoneDeviceIndex, 0, devices.Length - 1);
            micDevice = devices[idx];
        }
        else
        {
            micDevice = devices[0];
        }

        int minFreq;
        int maxFreq;
        Microphone.GetDeviceCaps(micDevice, out minFreq, out maxFreq);
        activeSampleRate = Mathf.Max(8000, requestedSampleRate);
        if (maxFreq > 0)
            activeSampleRate = Mathf.Clamp(activeSampleRate, Mathf.Max(8000, minFreq), maxFreq);

        Debug.Log($"[MicDiag] selected={micDevice} minFreq={minFreq} maxFreq={maxFreq} activeSampleRate={activeSampleRate}");

        AudioClip clip = Microphone.Start(micDevice, false, Mathf.Max(1, recordSeconds), activeSampleRate);
        if (clip == null)
        {
            Debug.LogError("[MicDiag] Microphone.Start returned null");
            yield break;
        }

        float wait = 0f;
        while (Microphone.GetPosition(micDevice) <= 0 && wait < 5f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        int startPos = Microphone.GetPosition(micDevice);
        Debug.Log("[MicDiag] start position = " + startPos);

        yield return new WaitForSeconds(recordSeconds);

        int endPos = Microphone.GetPosition(micDevice);
        Debug.Log("[MicDiag] end position = " + endPos);

        Microphone.End(micDevice);

        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);

        float rms = ComputeRms(samples);
        float peak = ComputePeak(samples);
        string preview = BuildPreview(samples, 16);
        Debug.Log($"[MicDiag] samples={samples.Length} rms={rms:F6} peak={peak:F6} preview={preview}");

        try
        {
            byte[] wav = WavUtility.FromAudioFloat(samples, 1, activeSampleRate);
            string path = Path.Combine(Path.GetTempPath(), "unity_mic_diagnostic.wav");
            File.WriteAllBytes(path, wav);
            Debug.Log("[MicDiag] wav saved -> " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("[MicDiag] save failed: " + e.Message);
        }

        Destroy(clip);
    }

    static float ComputeRms(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return 0f;

        double sum = 0d;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        return Mathf.Sqrt((float)(sum / samples.Length));
    }

    static float ComputePeak(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return 0f;

        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        return peak;
    }

    static string BuildPreview(float[] samples, int count)
    {
        if (samples == null || samples.Length == 0)
            return "[]";

        int n = Mathf.Min(count, samples.Length);
        string[] parts = new string[n];
        for (int i = 0; i < n; i++)
            parts[i] = samples[i].ToString("F4");
        return "[" + string.Join(", ", parts) + "]";
    }
}
