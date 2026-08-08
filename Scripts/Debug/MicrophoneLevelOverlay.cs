using System;
using UnityEngine;

public class MicrophoneLevelOverlay : MonoBehaviour
{
    public int microphoneDeviceIndex = -1;
    public int requestedSampleRate = 16000;
    public int bufferSeconds = 1;
    public bool autoStart = true;
    public bool showWindow = true;
    public Rect windowRect = new Rect(20, 20, 360, 150);

    string micDevice;
    AudioClip liveClip;
    int activeSampleRate;
    float currentRms;
    float currentPeak;
    string status = "Idle";

    void Start()
    {
        if (autoStart)
            StartMonitor();
    }

    void OnDisable()
    {
        StopMonitor();
    }

    void Update()
    {
        if (liveClip == null)
            return;

        if (!Microphone.IsRecording(micDevice))
        {
            status = "Mic stopped";
            return;
        }

        int pos = Microphone.GetPosition(micDevice);
        int clipSamples = liveClip.samples;
        if (pos <= 0 || clipSamples <= 0)
        {
            currentRms = 0f;
            currentPeak = 0f;
            status = "Waiting samples";
            return;
        }

        float[] allSamples = new float[clipSamples];
        liveClip.GetData(allSamples, 0);

        currentRms = ComputeRms(allSamples);
        currentPeak = ComputePeak(allSamples);
        status = "Recording";
    }

    [ContextMenu("Start Monitor")]
    public void StartMonitor()
    {
        StopMonitor();

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            status = "Mic permission denied";
            Debug.LogWarning("[MicOverlay] microphone authorization missing");
            return;
        }

        string[] devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            status = "No microphone";
            Debug.LogError("[MicOverlay] no microphone devices");
            return;
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

        liveClip = Microphone.Start(micDevice, true, Mathf.Max(1, bufferSeconds), activeSampleRate);
        status = liveClip != null ? "Starting..." : "Mic start failed";
        Debug.Log($"[MicOverlay] device={micDevice} minFreq={minFreq} maxFreq={maxFreq} activeSampleRate={activeSampleRate}");
    }

    [ContextMenu("Stop Monitor")]
    public void StopMonitor()
    {
        if (Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);

        if (liveClip != null)
        {
            Destroy(liveClip);
            liveClip = null;
        }

        currentRms = 0f;
        currentPeak = 0f;
    }

    void OnGUI()
    {
        if (!showWindow)
            return;

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Mic Overlay");
    }

    void DrawWindow(int id)
    {
        GUILayout.Label("Status: " + status);
        GUILayout.Label("Device: " + (string.IsNullOrEmpty(micDevice) ? "<none>" : micDevice));
        GUILayout.Label("Sample Rate: " + activeSampleRate);
        GUILayout.Label($"RMS: {currentRms:F6}");
        GUILayout.Label($"Peak: {currentPeak:F6}");

        float rmsBar = Mathf.Clamp01(currentRms * 50f);
        float peakBar = Mathf.Clamp01(currentPeak);
        GUILayout.Label("RMS");
        GUILayout.HorizontalSlider(rmsBar, 0f, 1f);
        GUILayout.Label("Peak");
        GUILayout.HorizontalSlider(peakBar, 0f, 1f);

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
}
