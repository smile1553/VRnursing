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
    public int recordSeconds = 3;
    [Range(0f, 0.05f)] public float minUploadRms = 0.003f;

    [Header("Auto Loop")]
    public float loopInterval = 0f; // 追加延遲；0 代表接力錄
    public bool continuousMicInLoop = true; // Loop 模式時持續開啟麥克風（系統麥克風燈會常亮）
    public bool forceSegmentedLoop = true; // 穩定優先：強制使用分段錄音，避免部分裝置在 continuous 模式無資料
    [Min(2)] public int continuousBufferSeconds = 20; // ring buffer 長度（秒）
    [Min(1)] public int maxQueuedChunks = 2; // server 忙時最多保留幾段，避免延遲後一次噴一堆舊結果
    [Header("Upload Queue")]
    [Min(1)] public int maxUploadQueue = 4;
    public bool dropOldestOnQueueFull = true;

    [Header("Voice Activity Detection (VAD)")]
    public bool enableVad = true;
    [Range(0f, 0.5f)] public float vadThreshold = 0.008f;      // legacy linear RMS threshold
    [Range(10f, 100f)] public float vadFrameMs = 30f;          // 分析用的 frame 長度（毫秒）
    public float vadPreRollMs = 200f;                          // 在檢測到語音前保留的緩衝
    public float vadPostRollMs = 200f;                         // 在語音結束後保留的緩衝
    public float vadMinSpeechMs = 350f;                        // 低於這個長度就視為無效
    [Header("Advanced VAD (dB)")]
    public bool useDbVad = true;
    public float startThresholdDb = -35f;
    public float endThresholdDb = -45f;
    public float maxSilenceMs = 600f;
    public float endPaddingMs = 200f;
    public bool autoCalibrateNoise = true;
    public float noiseCalibrateSec = 1f;
    public bool adaptiveNoiseTracking = true;
    [Range(0.001f, 1f)] public float noiseTrackAlpha = 0.08f;
    public float startAboveNoiseDb = 12f;
    public float endAboveNoiseDb = 6f;
    public float adaptiveNoiseCeilDb = -20f;
    public bool logVadState = true;

    [Header("Microphone Selection")]
    public int microphoneDeviceIndex = -1; // -1 = use OS default input device

    string micDevice;
    Coroutine loopRoutine;
    AudioClip liveClip;
    int liveReadPos;
    Coroutine startRetryRoutine;
    Coroutine uploadWorkerRoutine;
    readonly Queue<byte[]> pendingUploads = new Queue<byte[]>();
    bool useContinuousAtRuntime;
    bool noiseCalibrated;
    float calibratedNoiseFloorDb = -55f;
    string currentVadState = "Idle";

    [Header("Startup Retry")]
    public float startRetryInterval = 1f;
    public int startRetryMaxTimes = 10;

    void Start()
    {
        ConfigureMicrophoneDevice();
    }

    void OnDisable()
    {
        StopLoop();
    }

    // 給 UI 按鈕綁這個
    public void StartRecordAndUpload()
    {
        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogWarning("serverUrl not set yet.");
            return;
        }
        if (Microphone.devices == null || Microphone.devices.Length == 0) return;
        StartCoroutine(CaptureAndSendOnce());
    }

    public void StartLoop()
    {
        if (loopRoutine != null) return;

        if (!CanRecord())
        {
            Debug.LogWarning("[AudioUploader] StartLoop deferred: mic/server not ready.");
            if (startRetryRoutine == null)
                startRetryRoutine = StartCoroutine(RetryStartLoop());
            return;
        }

        useContinuousAtRuntime = continuousMicInLoop && !forceSegmentedLoop;
        noiseCalibrated = false;
        currentVadState = "Idle";
        if (continuousMicInLoop && forceSegmentedLoop)
            Debug.LogWarning("[AudioUploader] forceSegmentedLoop=true, skip continuous mic mode.");

        if (useContinuousAtRuntime && !StartContinuousMic())
        {
            Debug.LogWarning("[AudioUploader] Continuous mic start failed, fallback to segmented recording.");
            useContinuousAtRuntime = false;
        }

        EnsureUploadWorker();
        loopRoutine = StartCoroutine(CaptureLoop());
    }

    public void StopLoop()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        if (startRetryRoutine != null)
        {
            StopCoroutine(startRetryRoutine);
            startRetryRoutine = null;
        }

        StopContinuousMic();
        useContinuousAtRuntime = false;
        currentVadState = "Idle";
        if (uploadWorkerRoutine != null)
        {
            StopCoroutine(uploadWorkerRoutine);
            uploadWorkerRoutine = null;
        }
        pendingUploads.Clear();
    }

    bool CanRecord()
    {
        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogWarning("serverUrl not set yet.");
            return false;
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("microphone not ready.");
            return false;
        }

        return true;
    }

    void ConfigureMicrophoneDevice()
    {
        string[] devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            micDevice = null;
            Debug.LogError("[AudioUploader] No microphone detected!");
            return;
        }

        Debug.Log("[AudioUploader] Microphone devices: " + string.Join(" | ", devices));

        if (microphoneDeviceIndex < 0)
        {
            micDevice = null; // Unity null device name = OS default input
            Debug.Log("[AudioUploader] Using OS default microphone.");
            return;
        }

        int idx = Mathf.Clamp(microphoneDeviceIndex, 0, devices.Length - 1);
        micDevice = devices[idx];
        Debug.Log($"[AudioUploader] Using microphone[{idx}]={micDevice}");
    }

    IEnumerator RetryStartLoop()
    {
        int maxRetry = Mathf.Max(1, startRetryMaxTimes);
        float waitSec = Mathf.Max(0.2f, startRetryInterval);

        for (int i = 0; i < maxRetry; i++)
        {
            yield return new WaitForSeconds(waitSec);
            if (loopRoutine != null)
                break;

            if (!CanRecord())
                continue;

            startRetryRoutine = null;
            StartLoop();
            yield break;
        }

        Debug.LogError("[AudioUploader] StartLoop retry exhausted. Please check microphone permission/device.");
        startRetryRoutine = null;
    }

    IEnumerator CaptureLoop()
    {
        if (useContinuousAtRuntime)
        {
            yield return CaptureLoopContinuous();
            yield break;
        }

        while (true)
        {
            yield return CaptureAndSendOnce();
            if (loopInterval > 0f)
                yield return new WaitForSeconds(loopInterval);
        }
    }

    IEnumerator CaptureLoopContinuous()
    {
        int targetSamples = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.1f, recordSeconds) * sampleRate));
        List<float> pending = new List<float>(targetSamples * 2);
        int maxPendingSamples = targetSamples * Mathf.Max(1, maxQueuedChunks);

        while (true)
        {
            float[] incoming = ReadNewMicSamples();
            if (incoming != null && incoming.Length > 0)
                pending.AddRange(incoming);

            // Drop oldest backlog when server is slower than capture speed.
            if (pending.Count > maxPendingSamples)
            {
                int drop = pending.Count - maxPendingSamples;
                pending.RemoveRange(0, drop);
                Debug.LogWarning($"[AudioUploader] backlog drop {drop} samples (~{drop / (float)sampleRate:F2}s)");
            }

            while (pending.Count >= targetSamples)
            {
                float[] chunk = pending.GetRange(0, targetSamples).ToArray();
                pending.RemoveRange(0, targetSamples);
                yield return ProcessAndUploadChunk(chunk);

                if (loopInterval > 0f)
                    yield return new WaitForSeconds(loopInterval);
            }

            yield return null;
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

            Destroy(clip);
            yield return ProcessAndUploadChunk(samples);
            yield break;
        }

        yield return ProcessAndUploadChunk(samples);
    }

    IEnumerator ProcessAndUploadChunk(float[] rawSamples)
    {
        if (rawSamples == null || rawSamples.Length == 0)
            yield break;

        float[] uploadSamples = rawSamples;
        if (enableVad)
        {
            uploadSamples = ApplyVadTrim(rawSamples, sampleRate);
            if (!HasSpeech(uploadSamples))
            {
                Debug.LogWarning("[AudioUploader] VAD 沒偵測到語音，跳過上傳。");
                yield break;
            }
        }

        float rms = ComputeRms(uploadSamples);
        float peak = ComputePeak(uploadSamples);
        Debug.Log($"[AudioUploader] upload chunk samples={uploadSamples.Length} rms={rms:F5} peak={peak:F5} vad={enableVad}");

        if (rms < minUploadRms)
        {
            Debug.Log($"[AudioUploader] skip low-rms chunk rms={rms:F5} < minUploadRms={minUploadRms:F5}");
            yield break;
        }

        byte[] wav = WavUtility.FromAudioFloat(uploadSamples, 1, sampleRate);
        EnqueueUpload(wav);
        yield break;
    }

    void EnsureUploadWorker()
    {
        if (uploadWorkerRoutine == null)
            uploadWorkerRoutine = StartCoroutine(UploadWorker());
    }

    void EnqueueUpload(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return;

        int limit = Mathf.Max(1, maxUploadQueue);
        if (pendingUploads.Count >= limit)
        {
            if (!dropOldestOnQueueFull)
            {
                Debug.LogWarning($"[AudioUploader] upload queue full ({pendingUploads.Count}), drop newest chunk.");
                return;
            }

            pendingUploads.Dequeue();
            Debug.LogWarning($"[AudioUploader] upload queue full ({limit}), drop oldest chunk.");
        }

        pendingUploads.Enqueue(bytes);
        EnsureUploadWorker();
    }

    IEnumerator UploadWorker()
    {
        while (true)
        {
            if (pendingUploads.Count == 0)
            {
                if (loopRoutine == null)
                    break;

                yield return null;
                continue;
            }

            byte[] payload = pendingUploads.Dequeue();
            yield return Upload(payload);
        }

        uploadWorkerRoutine = null;
    }

    bool StartContinuousMic()
    {
        StopContinuousMic();
        if (Microphone.devices == null || Microphone.devices.Length == 0) return false;

        int lengthSec = Mathf.Max(2, continuousBufferSeconds);
        liveClip = Microphone.Start(micDevice, true, lengthSec, sampleRate);
        if (!liveClip) return false;

        liveReadPos = 0;
        return true;
    }

    void StopContinuousMic()
    {
        if (Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);

        if (liveClip != null)
        {
            Destroy(liveClip);
            liveClip = null;
        }
        liveReadPos = 0;
    }

    float[] ReadNewMicSamples()
    {
        if (liveClip == null) return null;
        if (!Microphone.IsRecording(micDevice)) return null;

        int clipSamples = liveClip.samples;
        if (clipSamples <= 0) return null;

        int currentPos = Microphone.GetPosition(micDevice);
        if (currentPos < 0) return null;
        if (currentPos == liveReadPos) return null;

        int available = currentPos - liveReadPos;
        if (available < 0) available += clipSamples;
        if (available <= 0) return null;

        float[] result = new float[available];
        int first = Mathf.Min(available, clipSamples - liveReadPos);
        float[] head = new float[first];
        liveClip.GetData(head, liveReadPos);
        Array.Copy(head, 0, result, 0, first);

        if (first < available)
        {
            float[] tail = new float[available - first];
            liveClip.GetData(tail, 0);
            Array.Copy(tail, 0, result, first, tail.Length);
        }

        liveReadPos = currentPos;
        return result;
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
        int preRollSamples = Mathf.Max(0, Mathf.RoundToInt(vadPreRollMs * 0.001f * sampleRate));
        int endPaddingSamples = Mathf.Max(0, Mathf.RoundToInt(endPaddingMs * 0.001f * sampleRate));
        int minSpeechSamples = Mathf.Max(0, Mathf.RoundToInt(vadMinSpeechMs * 0.001f * sampleRate));
        float maxSilenceSec = Mathf.Max(0f, maxSilenceMs * 0.001f);

        if (useDbVad && autoCalibrateNoise && !noiseCalibrated)
        {
            yield return CalibrateNoiseFloor(clip, frameSamples);
            noiseCalibrated = true;
        }

        GetRuntimeVadThresholds(out float runtimeStartDb, out float runtimeEndDb);

        Queue<float> preBuffer = preRollSamples > 0 ? new Queue<float>(preRollSamples + frameSamples) : null;
        List<float> collected = new List<float>();

        bool speechStarted = false;
        bool collectingPadding = false;
        int paddingCollected = 0;
        float silenceSec = 0f;
        int readPos = Mathf.Max(0, Microphone.GetPosition(micDevice) - frameSamples);
        float elapsed = 0f;
        float timeout = maxDuration + 1.0f;
        float[] frameBuffer = new float[frameSamples];

        currentVadState = "Idle";

        while (true)
        {
            bool recording = Microphone.IsRecording(micDevice);
            int currentPos = Microphone.GetPosition(micDevice);

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

            float rms = ComputeRms(frameBuffer);
            float db = LinearToDb(rms);

            if (!speechStarted)
            {
                currentVadState = "Idle";
                UpdateAdaptiveNoiseFloor(db);
                GetRuntimeVadThresholds(out runtimeStartDb, out runtimeEndDb);
                if (preBuffer != null)
                {
                    for (int i = 0; i < frameSamples; i++)
                    {
                        if (preBuffer.Count >= preRollSamples)
                            preBuffer.Dequeue();
                        preBuffer.Enqueue(frameBuffer[i]);
                    }
                }

                if (db >= runtimeStartDb)
                {
                    speechStarted = true;
                    currentVadState = "Speaking";
                    if (preBuffer != null && preBuffer.Count > 0)
                        collected.AddRange(preBuffer);
                    collected.AddRange(frameBuffer);
                    silenceSec = 0f;
                    if (logVadState)
                        Debug.Log($"[AudioUploader/VAD] start db={db:F1}, startTh={runtimeStartDb:F1}, endTh={runtimeEndDb:F1}");
                }
            }
            else
            {
                collected.AddRange(frameBuffer);

                if (collectingPadding)
                {
                    paddingCollected += frameSamples;
                    currentVadState = "EndPadding";
                    if (paddingCollected >= endPaddingSamples)
                        break;
                    continue;
                }

                if (db >= runtimeEndDb)
                {
                    silenceSec = 0f;
                    currentVadState = "Speaking";
                }
                else
                {
                    silenceSec += (float)frameSamples / sampleRate;
                    currentVadState = "SilenceCounting";
                    if (silenceSec >= maxSilenceSec)
                    {
                        collectingPadding = true;
                        paddingCollected = 0;
                        if (endPaddingSamples <= 0)
                            break;
                    }
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
            if (HasSpeech(trimmed))
                result = trimmed;
            else if (HasSpeech(fallback))
                result = fallback;
        }

        Destroy(clip);
        currentVadState = "Idle";
        onFinished?.Invoke(result);
    }

    IEnumerator CalibrateNoiseFloor(AudioClip clip, int frameSamples)
    {
        int targetSamples = Mathf.Max(frameSamples, Mathf.RoundToInt(Mathf.Max(0.2f, noiseCalibrateSec) * sampleRate));
        int readPos = 0;
        int collected = 0;
        List<float> dbValues = new List<float>();
        float[] frameBuffer = new float[frameSamples];
        float timeout = Mathf.Max(1.0f, noiseCalibrateSec + 0.8f);
        float elapsed = 0f;

        while (collected < targetSamples && elapsed < timeout)
        {
            int currentPos = Microphone.GetPosition(micDevice);
            int available = currentPos - readPos;
            if (available < frameSamples)
            {
                elapsed += Time.deltaTime;
                yield return null;
                continue;
            }

            clip.GetData(frameBuffer, readPos);
            readPos += frameSamples;
            collected += frameSamples;
            dbValues.Add(LinearToDb(ComputeRms(frameBuffer)));
        }

        if (dbValues.Count > 0)
        {
            float sum = 0f;
            for (int i = 0; i < dbValues.Count; i++)
                sum += dbValues[i];
            calibratedNoiseFloorDb = sum / dbValues.Count;
        }
        else
        {
            calibratedNoiseFloorDb = -55f;
        }

        if (logVadState)
            Debug.Log($"[AudioUploader/VAD] noiseFloor={calibratedNoiseFloorDb:F1} dB");
    }

    void GetRuntimeVadThresholds(out float runtimeStartDb, out float runtimeEndDb)
    {
        if (!useDbVad)
        {
            runtimeStartDb = LinearToDb(Mathf.Max(1e-6f, vadThreshold));
            runtimeEndDb = LinearToDb(Mathf.Max(1e-6f, vadThreshold * 0.7f));
            return;
        }

        bool useNoiseRelative = autoCalibrateNoise || adaptiveNoiseTracking;
        runtimeStartDb = useNoiseRelative ? Mathf.Max(startThresholdDb, calibratedNoiseFloorDb + startAboveNoiseDb) : startThresholdDb;
        runtimeEndDb = useNoiseRelative ? Mathf.Max(endThresholdDb, calibratedNoiseFloorDb + endAboveNoiseDb) : endThresholdDb;

        if (runtimeEndDb >= runtimeStartDb)
            runtimeEndDb = runtimeStartDb - 1f;
    }

    void UpdateAdaptiveNoiseFloor(float frameDb)
    {
        if (!useDbVad || !adaptiveNoiseTracking)
            return;

        // Only learn clearly non-speech frames to avoid pulling thresholds upward while user talks.
        float guardCeil = Mathf.Min(adaptiveNoiseCeilDb, startThresholdDb - 3f);
        if (frameDb > guardCeil)
            return;

        float alpha = Mathf.Clamp01(noiseTrackAlpha);
        calibratedNoiseFloorDb = Mathf.Lerp(calibratedNoiseFloorDb, frameDb, alpha);
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

    float LinearToDb(float rms)
    {
        return 20f * Mathf.Log10(Mathf.Max(1e-6f, rms));
    }

    bool HasSpeech(float[] source)
    {
        if (source == null || source.Length == 0) return false;

        int frameSamples = Mathf.Max(1, Mathf.RoundToInt(vadFrameMs * 0.001f * sampleRate));
        float threshold = Mathf.Max(0f, vadThreshold);
        int frames = Mathf.CeilToInt(source.Length / (float)frameSamples);

        for (int frame = 0; frame < frames; frame++)
        {
            int offset = frame * frameSamples;
            int count = Math.Min(frameSamples, source.Length - offset);
            if (count <= 0) break;

            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += Mathf.Abs(source[offset + i]);

            if ((sum / count) >= threshold)
                return true;
        }

        return false;
    }

    float ComputeRms(float[] source)
    {
        if (source == null || source.Length == 0) return 0f;

        double sum = 0.0;
        for (int i = 0; i < source.Length; i++)
            sum += source[i] * source[i];

        return Mathf.Sqrt((float)(sum / source.Length));
    }

    float ComputePeak(float[] source)
    {
        if (source == null || source.Length == 0) return 0f;

        float peak = 0f;
        for (int i = 0; i < source.Length; i++)
        {
            float a = Mathf.Abs(source[i]);
            if (a > peak) peak = a;
        }

        return peak;
    }
}
