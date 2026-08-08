using System;
using System.IO;
using UnityEngine;

public class MicrophoneBareMinimum : MonoBehaviour
{
    public int microphoneDeviceIndex = -1;
    public int requestedSampleRate = 16000;
    public int recordSeconds = 3;
    public bool autoRunOnStart = true;
    public bool showWindow = true;
    public Rect windowRect = new Rect(20, 180, 380, 180);

    string micDevice;
    int activeSampleRate;
    string status = "Idle";
    float lastRms;
    float lastPeak;
    string lastSavePath = "";
    string preview = "[]";
    bool running;

    void Start()
    {
        if (autoRunOnStart)
            StartCoroutine(RunOnce());
    }

    [ContextMenu("Run Once")]
    public void RunFromInspector()
    {
        if (!running)
            StartCoroutine(RunOnce());
    }

    System.Collections.IEnumerator RunOnce()
    {
        running = true;
        status = "Requesting permission";

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            status = "Permission denied";
            running = false;
            yield break;
        }

        string[] devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            status = "No devices";
            running = false;
            yield break;
        }

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

        status = $"Recording {micDevice} @ {activeSampleRate}";

        AudioClip clip = Microphone.Start(micDevice, false, Mathf.Max(1, recordSeconds), activeSampleRate);
        if (clip == null)
        {
            status = "Microphone.Start failed";
            running = false;
            yield break;
        }

        float wait = 0f;
        while (Microphone.GetPosition(micDevice) <= 0 && wait < 5f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(recordSeconds);
        Microphone.End(micDevice);

        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);
        Destroy(clip);

        lastRms = ComputeRms(samples);
        lastPeak = ComputePeak(samples);
        preview = BuildPreview(samples, 12);

        try
        {
            string path = Path.Combine(Path.GetTempPath(), "unity_mic_bare_minimum.wav");
            WriteWav(path, samples, activeSampleRate);
            lastSavePath = path;
            status = $"Saved: {path}";
            Debug.Log("[MicBare] wav saved -> " + path);
            Debug.Log($"[MicBare] rms={lastRms:F6} peak={lastPeak:F6} preview={preview}");
        }
        catch (Exception e)
        {
            status = "Save failed: " + e.Message;
            Debug.LogError("[MicBare] " + e);
        }

        running = false;
    }

    void OnGUI()
    {
        if (!showWindow)
            return;

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Mic Bare Minimum");
    }

    void DrawWindow(int id)
    {
        GUILayout.Label("Status: " + status);
        GUILayout.Label("Device: " + (string.IsNullOrEmpty(micDevice) ? "<none>" : micDevice));
        GUILayout.Label("Sample Rate: " + activeSampleRate);
        GUILayout.Label($"RMS: {lastRms:F6}");
        GUILayout.Label($"Peak: {lastPeak:F6}");
        GUILayout.Label("Preview: " + preview);
        GUILayout.Label("Saved: " + lastSavePath);
        if (GUILayout.Button(running ? "Running..." : "Run Once") && !running)
            StartCoroutine(RunOnce());
        GUI.DragWindow(new Rect(0, 0, 10000, 24));
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

    static void WriteWav(string path, float[] samples, int sampleRate)
    {
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            int channels = 1;
            short bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);
            int dataSize = samples.Length * 2;

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);

            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
                bw.Write(s);
            }
        }
    }
}
