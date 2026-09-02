using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AudioUploader : MonoBehaviour
{
    [Header("Server")]
    [HideInInspector] public string serverUrl;  // ← 不寫死，由外部指定，例如 http://IP:8000/audio

    [Header("Record")]
    public int sampleRate = 16000;
    public int recordSeconds = 1;

    [Header("Auto Loop")]
    public float loopInterval = 0f; // 追加延遲；0 代表接力錄

    [Header("Voice Activity Detection (VAD)")]
    public bool enableVad = false;
    [Range(0f, 0.5f)] public float vadThreshold = 0.02f;      // 每個 frame 的平均能量門檻
    [Range(10f, 100f)] public float vadFrameMs = 30f;          // 分析用的 frame 長度（毫秒）
    public float vadPreRollMs = 80f;                           // 在檢測到語音前保留的緩衝
    public float vadPostRollMs = 120f;                         // 在語音結束後保留的緩衝
    public float vadMinSpeechMs = 120f;                        // 低於這個長度就視為無效

    string micDevice;
    Coroutine loopRoutine;

    void Start()
    {
        if (Microphone.devices.Length > 0) micDevice = Microphone.devices[0];
        else Debug.LogError("No microphone detected!");
    }

    // 給 UI 按鈕綁這個
    public void StartRecordAndUpload()
    {
        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogWarning("serverUrl not set yet.");
            return;
        }
        if (micDevice == null) return;
        StartCoroutine(CaptureAndSendOnce());
    }

    public void StartLoop()
    {
        if (loopRoutine != null) return;
        if (!CanRecord()) return;
        loopRoutine = StartCoroutine(CaptureLoop());
    }

    public void StopLoop()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }
    }

    bool CanRecord()
    {
        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogWarning("serverUrl not set yet.");
            return false;
        }
        if (micDevice == null)
        {
            Debug.LogWarning("microphone not ready.");
            return false;
        }
        return true;
    }

    IEnumerator CaptureLoop()
    {
        while (true)
        {
            yield return CaptureAndSendOnce();
            if (loopInterval > 0f)
                yield return new WaitForSeconds(loopInterval);
        }
    }

    IEnumerator CaptureAndSendOnce()
    {
        if (!CanRecord()) yield break;

        float[] samples = null;

        if (enableVad)
        {
            yield return CaptureUsingVad(result => samples = result);
            if (samples == null || samples.Length == 0)
            {
                Debug.LogWarning("[AudioUploader] VAD 沒偵測到語音，跳過上傳。");
                yield break;
            }
        }
        else
        {
            AudioClip clip = Microphone.Start(micDevice, false, Mathf.Max(1, recordSeconds), sampleRate);
            if (!clip)
            {
                Debug.LogError("[AudioUploader] 無法啟動錄音。");
                yield break;
            }

            while (Microphone.GetPosition(micDevice) <= 0) yield return null;
            yield return new WaitForSeconds(recordSeconds);
            Microphone.End(micDevice);

            samples = new float[clip.samples];
            clip.GetData(samples, 0);

            byte[] wavData = WavUtility.FromAudioFloat(samples, 1, sampleRate);
            Destroy(clip);
            yield return Upload(wavData);
            yield break;
        }

        byte[] vadWav = WavUtility.FromAudioFloat(samples, 1, sampleRate);
        yield return Upload(vadWav);
    }

    IEnumerator CaptureUsingVad(Action<float[]> onFinished)
    {
        float maxDuration = Mathf.Max(1, recordSeconds);
        AudioClip clip = Microphone.Start(micDevice, false, Mathf.CeilToInt(maxDuration), sampleRate);
        if (!clip)
        {
            onFinished?.Invoke(null);
            yield break;
        }

        while (Microphone.GetPosition(micDevice) <= 0) yield return null;

        int frameSamples = Mathf.Max(1, Mathf.RoundToInt(vadFrameMs * 0.001f * sampleRate));
        float threshold = Mathf.Max(0f, vadThreshold);
        int preRollSamples = Mathf.Max(0, Mathf.RoundToInt(vadPreRollMs * 0.001f * sampleRate));
        int postRollFrames = Mathf.Max(0, Mathf.CeilToInt(vadPostRollMs / Mathf.Max(1f, vadFrameMs)));
        int minSpeechSamples = Mathf.Max(0, Mathf.RoundToInt(vadMinSpeechMs * 0.001f * sampleRate));

        Queue<float> preBuffer = preRollSamples > 0 ? new Queue<float>(preRollSamples + frameSamples) : null;
        List<float> collected = new List<float>();

        bool speechStarted = false;
        int silenceFrames = 0;
        int readPos = 0;
        float elapsed = 0f;
        float timeout = maxDuration + 0.5f;

        float[] frameBuffer = new float[frameSamples];

        while (true)
        {
            bool recording = Microphone.IsRecording(micDevice);
            int currentPos = Microphone.GetPosition(micDevice);

            if (!recording && currentPos <= 0 && readPos >= currentPos)
                break;

            int samplesAvailable = currentPos - readPos;
            if (samplesAvailable < frameSamples)
            {
                if (!recording && samplesAvailable <= 0)
                    break;

                elapsed += Time.deltaTime;
                if (elapsed > timeout)
                    break;

                yield return null;
                continue;
            }

            clip.GetData(frameBuffer, readPos);
            readPos += frameSamples;

            float sum = 0f;
            for (int i = 0; i < frameSamples; i++)
                sum += Mathf.Abs(frameBuffer[i]);
            float avg = sum / frameSamples;

            if (!speechStarted)
            {
                if (preBuffer != null)
                {
                    for (int i = 0; i < frameSamples; i++)
                    {
                        if (preBuffer.Count >= preRollSamples)
                            preBuffer.Dequeue();
                        preBuffer.Enqueue(frameBuffer[i]);
                    }
                }

                if (avg >= threshold)
                {
                    speechStarted = true;
                    if (preBuffer != null && preBuffer.Count > 0)
                        collected.AddRange(preBuffer);
                    collected.AddRange(frameBuffer);
                    silenceFrames = 0;
                }
            }
            else
            {
                collected.AddRange(frameBuffer);
                if (avg >= threshold)
                    silenceFrames = 0;
                else
                {
                    silenceFrames++;
                    if (postRollFrames <= 0 || silenceFrames >= postRollFrames)
                        break;
                }
            }

            if (readPos >= clip.samples)
                break;
        }

        int recordedSamples = Mathf.Clamp(Mathf.Max(readPos, Microphone.GetPosition(micDevice)), 0, clip.samples);
        float[] fallback = null;
        if (recordedSamples > 0)
        {
            fallback = new float[recordedSamples];
            clip.GetData(fallback, 0);
        }

        Microphone.End(micDevice);

        float[] result = null;
        if (collected.Count >= minSpeechSamples)
        {
            result = collected.ToArray();
        }
        else if (fallback != null)
        {
            float[] trimmed = ApplyVadTrim(fallback, sampleRate);
            if (!ReferenceEquals(trimmed, fallback))
                result = trimmed;
        }

        Destroy(clip);
        onFinished?.Invoke(result);
    }

    IEnumerator Upload(byte[] bytes)
    {
        using (UnityWebRequest req = new UnityWebRequest(serverUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "audio/wav");
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (req.result == UnityWebRequest.Result.Success)
#else
            if (!req.isNetworkError && !req.isHttpError)
#endif
                Debug.Log("Upload OK: " + req.downloadHandler.text);
            else
                Debug.LogError("Upload Error: " + req.error);
        }
    }

    float[] ApplyVadTrim(float[] source, int sr)
    {
        if (source == null || source.Length == 0) return source;
        int frameSamples = Mathf.Max(1, Mathf.RoundToInt(vadFrameMs * 0.001f * sr));
        if (frameSamples <= 0) return source;

        int totalFrames = Mathf.CeilToInt(source.Length / (float)frameSamples);
        if (totalFrames <= 1) return source;

        float threshold = Mathf.Max(0f, vadThreshold);
        int firstSpeechFrame = -1;
        int lastSpeechFrame = -1;

        for (int frame = 0; frame < totalFrames; frame++)
        {
            int offset = frame * frameSamples;
            int count = Math.Min(frameSamples, source.Length - offset);
            if (count <= 0) break;

            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += Mathf.Abs(source[offset + i]);

            float avg = sum / count;
            if (avg >= threshold)
            {
                if (firstSpeechFrame < 0)
                    firstSpeechFrame = frame;
                lastSpeechFrame = frame;
            }
        }

        if (firstSpeechFrame < 0 || lastSpeechFrame < 0) return source; // 未偵測到語音

        int preRollFrames = Mathf.RoundToInt(vadPreRollMs / vadFrameMs);
        int postRollFrames = Mathf.RoundToInt(vadPostRollMs / vadFrameMs);

        int startFrame = Mathf.Max(0, firstSpeechFrame - preRollFrames);
        int endFrame = Mathf.Min(totalFrames - 1, lastSpeechFrame + postRollFrames);

        int startSample = startFrame * frameSamples;
        int endSample = Math.Min(source.Length, (endFrame + 1) * frameSamples);
        int length = endSample - startSample;

        int minSpeechSamples = Mathf.RoundToInt(vadMinSpeechMs * 0.001f * sr);
        if (length <= 0 || length < minSpeechSamples)
            return source; // 避免切得太短導致資料太少

        if (length >= source.Length) return source;

        float[] trimmed = new float[length];
        Array.Copy(source, startSample, trimmed, 0, length);
        return trimmed;
    }
}
