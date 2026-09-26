using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class AudioUploader : MonoBehaviour
{
    const int UploadSampleRate = 16000;
    const float AutomaticEndSilenceMs = 500f;

    [Header("Server")]
    [HideInInspector] public string serverUrl;  // ← 不寫死，由外部指定，例如 http://IP:8000/audio

    [Header("Record")]
    public int sampleRate = 16000;
    public int recordSeconds = 3;
    [Range(0f, 0.05f)] public float minUploadRms = 0.003f;
    [Range(1f, 8f)] public float uploadGain = 3f;
    public bool saveDebugWav = false;

    [Header("Auto Loop")]
    public float loopInterval = 0f; // 追加延遲；0 代表接力錄
    public bool continuousMicInLoop = true; // Loop 模式時持續開啟麥克風（系統麥克風燈會常亮）
    public bool forceSegmentedLoop = false; // 僅在特定裝置無法使用持續麥克風時才啟用 fallback
    [Min(2)] public int continuousBufferSeconds = 20; // ring buffer 長度（秒）
    [Min(1)] public int maxQueuedChunks = 2; // server 忙時最多保留幾段，避免延遲後一次噴一堆舊結果
    [Header("Upload Queue")]
    [Min(1)] public int maxUploadQueue = 4;
    public bool dropOldestOnQueueFull = true;
    [Min(1)] public int uploadTimeoutSeconds = 30;
    [Tooltip("0 retries transient /audio failures until the same request succeeds.")]
    public int maxUploadRetries = 0;
    [Min(0.1f)] public float uploadRetryDelaySeconds = 2f;

    [Header("Complete Utterance")]
    [Min(3f)] public float maxUtteranceSeconds = 20f;

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
    public float maxSilenceMs = 500f;
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
    int activeSampleRate;
    Coroutine loopRoutine;
    AudioClip liveClip;
    int liveReadPos;
    AudioClip pushToTalkClip;
    bool pushToTalkRecording;
    float pushToTalkStartedAt;
    Coroutine startRetryRoutine;
    Coroutine uploadWorkerRoutine;
    readonly Queue<AudioUploadWorkItem> pendingUploads = new Queue<AudioUploadWorkItem>();
    readonly HashSet<string> deliveredRequestIds = new HashSet<string>(StringComparer.Ordinal);
    bool useContinuousAtRuntime;
    bool captureEnabled;
    bool uploadInProgress;
    bool blockingUploadFailure;
    int outstandingRequestCount;
    string lastUploadError;
    bool noiseCalibrated;
    float calibratedNoiseFloorDb = -55f;
    string currentVadState = "Idle";

    public event Action<AudioAnalysisResponse, string> AudioResponseAccepted;
    public event Action AudioProcessingStateChanged;

    public bool IsAudioProcessingIdle => outstandingRequestCount == 0 &&
        pendingUploads.Count == 0 && !uploadInProgress &&
        loopRoutine == null && !pushToTalkRecording;
    public bool HasBlockingAudioFailure => blockingUploadFailure;
    public int OutstandingRequestCount => outstandingRequestCount;
    public string LastUploadError => lastUploadError;
    public bool IsPushToTalkRecording => pushToTalkRecording;

    [Header("Startup Retry")]
    public float startRetryInterval = 1f;
    public int startRetryMaxTimes = 10;

    void Start()
    {
        ConfigureMicrophoneDevice();
        ResolveRecordingSampleRate();
    }

    void OnDisable()
    {
        StopLoop();
    }

    // 給 UI 按鈕綁這個
    public void StartRecordAndUpload()
    {
        Debug.LogWarning("[AudioUploader] Manual recording button is disabled. Login starts automatic VAD capture.");
    }

    public void StartLoop()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.LogWarning("[AudioUploader] Microphone permission not granted.");
            if (startRetryRoutine == null)
                startRetryRoutine = StartCoroutine(RequestMicAndRetry());
            return;
        }

        ConfigureMicrophoneDevice();
        ResolveRecordingSampleRate();

        if (!CanRecord())
        {
            Debug.LogWarning("[AudioUploader] StartLoop deferred: mic/server not ready.");
            if (startRetryRoutine == null)
                startRetryRoutine = StartCoroutine(RetryStartLoop());
            return;
        }

        if (loopRoutine != null)
            return;

        CancelPushToTalkRecording();
        // Keep the formal automatic-capture policy in code. Older scene files
        // may still serialize the former segmented-loop settings, and those
        // values must not silently turn Push-to-Talk/segmented capture back on.
        continuousMicInLoop = true;
        forceSegmentedLoop = false;
        maxSilenceMs = AutomaticEndSilenceMs;
        noiseCalibrated = false;
        currentVadState = "Idle";
        captureEnabled = true;
        useContinuousAtRuntime = continuousMicInLoop && !forceSegmentedLoop;

        if (useContinuousAtRuntime && !StartContinuousMic())
        {
            Debug.LogWarning("[AudioUploader] Continuous microphone unavailable; falling back to segmented capture.");
            useContinuousAtRuntime = false;
        }

        loopRoutine = StartCoroutine(CaptureLoop());
        Debug.Log(useContinuousAtRuntime
            ? "[AudioUploader] Automatic VAD capture started with one continuous microphone session."
            : "[AudioUploader] Automatic VAD capture started in segmented fallback mode.");
        NotifyProcessingStateChanged();
    }

    public void StartPushToTalk()
    {
        if (pushToTalkRecording)
            return;

        StudentRunContext context = StudentRunContext.Current;
        if (!context.HasStudentRun || !context.StudentRunAccepted || context.ScenarioCompleted)
        {
            Debug.LogWarning("[AudioUploader] Push-to-Talk requires an active accepted Student Run.");
            return;
        }
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            Debug.LogWarning("[AudioUploader] Push-to-Talk start ignored: server is not connected.");
            return;
        }

        ConfigureMicrophoneDevice();
        ResolveRecordingSampleRate();
        if (!CanRecord())
        {
            Debug.LogWarning("[AudioUploader] Push-to-Talk start ignored: server or microphone is not ready.");
            return;
        }

        int maximumSeconds = Mathf.Max(1, Mathf.CeilToInt(maxUtteranceSeconds));
        pushToTalkClip = Microphone.Start(micDevice, false, maximumSeconds, activeSampleRate);
        if (pushToTalkClip == null)
        {
            Debug.LogError("[AudioUploader] Push-to-Talk could not start the microphone.");
            return;
        }

        pushToTalkStartedAt = Time.realtimeSinceStartup;
        pushToTalkRecording = true;
        Debug.Log($"[AudioUploader] Push-to-Talk started; maximum={maximumSeconds}s sampleRate={activeSampleRate}.");
    }

    public void StopPushToTalkAndUpload()
    {
        if (!pushToTalkRecording || pushToTalkClip == null)
            return;

        AudioClip clip = pushToTalkClip;
        int recordedSamples = Microphone.GetPosition(micDevice);
        float elapsed = Time.realtimeSinceStartup - pushToTalkStartedAt;

        // A non-looping microphone returns to position zero after reaching its
        // maximum length. In that case the complete safety buffer is valid.
        if (recordedSamples <= 0 && elapsed >= Mathf.Max(1f, maxUtteranceSeconds) - 0.1f)
            recordedSamples = clip.samples;

        Microphone.End(micDevice);
        pushToTalkClip = null;
        pushToTalkRecording = false;
        pushToTalkStartedAt = 0f;

        recordedSamples = Mathf.Clamp(recordedSamples, 0, clip.samples);
        int recordingSampleRate = clip.frequency > 0 ? clip.frequency : activeSampleRate;
        int minimumSamples = Mathf.Max(1,
            Mathf.RoundToInt(vadMinSpeechMs * 0.001f * recordingSampleRate));
        if (recordedSamples < minimumSamples)
        {
            Debug.Log($"[AudioUploader] Push-to-Talk discarded: {recordedSamples} samples is shorter than {minimumSamples}.");
            Destroy(clip);
            return;
        }

        float[] interleavedSamples = new float[recordedSamples * clip.channels];
        clip.GetData(interleavedSamples, 0);
        float[] samples = ConvertToMonoAndResample(
            interleavedSamples, Mathf.Max(1, clip.channels), recordingSampleRate, UploadSampleRate);
        Destroy(clip);
        activeSampleRate = UploadSampleRate;

        // VAD is applied only after release, for silence trim and speech
        // validation. It no longer starts or ends an utterance.
        StartCoroutine(ProcessAndUploadChunk(samples));
    }

    public void StopLoop()
    {
        CancelPushToTalkRecording();

        captureEnabled = false;
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }
        StopContinuousMic();
        useContinuousAtRuntime = false;

        if (uploadWorkerRoutine != null)
        {
            StopCoroutine(uploadWorkerRoutine);
            uploadWorkerRoutine = null;
        }
        pendingUploads.Clear();
        outstandingRequestCount = 0;
        uploadInProgress = false;
        NotifyProcessingStateChanged();
    }

    public void StopCaptureAndFlush()
    {
        if (pushToTalkRecording)
            StopPushToTalkAndUpload();

        captureEnabled = false;
        bool finishContinuousUtterance = useContinuousAtRuntime && loopRoutine != null;
        if (loopRoutine != null && !finishContinuousUtterance)
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
        NotifyProcessingStateChanged();
    }

    void CancelPushToTalkRecording()
    {
        if (!pushToTalkRecording && pushToTalkClip == null)
            return;

        Microphone.End(micDevice);
        if (pushToTalkClip != null)
            Destroy(pushToTalkClip);
        pushToTalkClip = null;
        pushToTalkRecording = false;
        pushToTalkStartedAt = 0f;
    }

    public void ResetForStudent()
    {
        StopLoop();
        noiseCalibrated = false;
        currentVadState = "Idle";
        blockingUploadFailure = false;
        lastUploadError = string.Empty;
        deliveredRequestIds.Clear();
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

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.LogWarning("microphone permission denied.");
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
            // Unity uses null for the operating system's current default input.
            // Do not force devices[0]: its order is not stable between Editor,
            // Quest Link, and standalone Android builds.
            micDevice = null;
            Debug.Log("[AudioUploader] Using OS default microphone (device=null).");
            return;
        }

        int idx = Mathf.Clamp(microphoneDeviceIndex, 0, devices.Length - 1);
        micDevice = devices[idx];
        Debug.Log($"[AudioUploader] Using microphone[{idx}]={micDevice}");
    }

    void ResolveRecordingSampleRate()
    {
        activeSampleRate = Mathf.Max(8000, sampleRate);
        if (Microphone.devices == null || Microphone.devices.Length == 0)
            return;

        int minFreq;
        int maxFreq;
        Microphone.GetDeviceCaps(micDevice, out minFreq, out maxFreq);
        if (minFreq == 0 && maxFreq == 0)
        {
            Debug.Log($"[AudioUploader] Mic caps unknown, fallback sampleRate={activeSampleRate}");
            return;
        }

        if (maxFreq > 0)
            activeSampleRate = Mathf.Clamp(activeSampleRate, Mathf.Max(8000, minFreq), maxFreq);

        if (activeSampleRate <= 0)
            activeSampleRate = maxFreq > 0 ? maxFreq : Mathf.Max(8000, sampleRate);

        Debug.Log($"[AudioUploader] Mic caps device={(string.IsNullOrEmpty(micDevice) ? "<OS default>" : micDevice)} minFreq={minFreq} maxFreq={maxFreq} activeSampleRate={activeSampleRate}");
    }

    IEnumerator RequestMicAndRetry()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        startRetryRoutine = null;
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.LogError("[AudioUploader] Microphone authorization denied.");
            yield break;
        }

        StartLoop();
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
        }
        else
        {
            while (captureEnabled)
            {
                yield return CaptureAndSendOnce();
                if (loopInterval > 0f && captureEnabled)
                    yield return new WaitForSeconds(loopInterval);
            }
        }

        loopRoutine = null;
        currentVadState = "Idle";
        NotifyProcessingStateChanged();
    }

    IEnumerator CaptureLoopContinuous()
    {
        int frameSamples = Mathf.Max(1, Mathf.RoundToInt(vadFrameMs * 0.001f * activeSampleRate));
        int preRollSamples = Mathf.Max(0, Mathf.RoundToInt(vadPreRollMs * 0.001f * activeSampleRate));
        int minimumSpeechSamples = Mathf.Max(1, Mathf.RoundToInt(vadMinSpeechMs * 0.001f * activeSampleRate));
        int maximumUtteranceSamples = Mathf.Max(minimumSpeechSamples,
            Mathf.RoundToInt(Mathf.Max(3f, maxUtteranceSeconds) * activeSampleRate));
        float endSilenceSeconds = Mathf.Max(0.05f, maxSilenceMs * 0.001f);

        var pending = new List<float>(frameSamples * 8);
        int pendingOffset = 0;
        var frame = new float[frameSamples];
        var preBuffer = new Queue<float>(Mathf.Max(1, preRollSamples + frameSamples));
        var utterance = new List<float>(Mathf.Min(maximumUtteranceSamples, activeSampleRate * 8));
        bool speechStarted = false;
        float silenceSeconds = 0f;

        while (captureEnabled)
        {
            float[] incoming = ReadNewMicSamples();
            if (incoming != null && incoming.Length > 0)
                pending.AddRange(incoming);

            while (pending.Count - pendingOffset >= frameSamples && captureEnabled)
            {
                pending.CopyTo(pendingOffset, frame, 0, frameSamples);
                pendingOffset += frameSamples;
                if (pendingOffset >= frameSamples * 32)
                {
                    pending.RemoveRange(0, pendingOffset);
                    pendingOffset = 0;
                }

                float frameRms = ComputeRms(frame);
                float db = LinearToDb(frameRms);
                GetRuntimeVadThresholds(out float runtimeStartDb, out float runtimeEndDb);

                if (!speechStarted)
                {
                    currentVadState = "Idle";
                    UpdateAdaptiveNoiseFloor(db);
                    GetRuntimeVadThresholds(out runtimeStartDb, out runtimeEndDb);
                    AppendRing(preBuffer, frame, preRollSamples);

                    bool started = useDbVad ? db >= runtimeStartDb : frameRms >= vadThreshold;
                    if (started)
                    {
                        speechStarted = true;
                        silenceSeconds = 0f;
                        utterance.Clear();
                        if (preBuffer.Count > 0)
                            utterance.AddRange(preBuffer);
                        else
                            utterance.AddRange(frame);
                        currentVadState = "Speaking";
                    }
                    continue;
                }

                utterance.AddRange(frame);
                bool stillSpeaking = useDbVad ? db >= runtimeEndDb : frameRms >= vadThreshold;
                if (stillSpeaking)
                {
                    silenceSeconds = 0f;
                    currentVadState = "Speaking";
                }
                else
                {
                    silenceSeconds += frameSamples / (float)activeSampleRate;
                    currentVadState = "SilenceCounting";
                }

                bool endedBySilence = silenceSeconds >= endSilenceSeconds;
                bool endedByLimit = utterance.Count >= maximumUtteranceSamples;
                if (!endedBySilence && !endedByLimit)
                    continue;

                int completeSampleCount = utterance.Count;
                if (endedBySilence)
                {
                    int detectedSilenceSamples = Mathf.RoundToInt(silenceSeconds * activeSampleRate);
                    int keepTrailingSamples = Mathf.RoundToInt(
                        Mathf.Max(0f, endPaddingMs) * 0.001f * activeSampleRate);
                    completeSampleCount -= Mathf.Max(0, detectedSilenceSamples - keepTrailingSamples);
                }
                completeSampleCount = Mathf.Clamp(completeSampleCount, 0, utterance.Count);
                var completeUtterance = new float[completeSampleCount];
                if (completeSampleCount > 0)
                    utterance.CopyTo(0, completeUtterance, 0, completeSampleCount);
                speechStarted = false;
                silenceSeconds = 0f;
                utterance.Clear();
                preBuffer.Clear();
                currentVadState = "Idle";

                if (completeUtterance.Length >= minimumSpeechSamples)
                    yield return ProcessAndUploadChunk(completeUtterance, true);
                if (loopInterval > 0f)
                    yield return new WaitForSeconds(loopInterval);
            }

            yield return null;
        }

        // Scenario completion may occur while the student is finishing the
        // final sentence. Flush the speech already captured before allowing
        // StudentResultUploader to freeze ToneScore.
        if (speechStarted)
        {
            for (int i = pendingOffset; i < pending.Count; i++)
                utterance.Add(pending[i]);
            if (utterance.Count >= minimumSpeechSamples)
                yield return ProcessAndUploadChunk(utterance.ToArray(), true);
        }
    }

    static void AppendRing(Queue<float> target, float[] samples, int capacity)
    {
        if (target == null || samples == null || capacity <= 0)
            return;
        for (int i = 0; i < samples.Length; i++)
        {
            while (target.Count >= capacity)
                target.Dequeue();
            target.Enqueue(samples[i]);
        }
    }

    IEnumerator CaptureAndSendOnce()
    {
        if (!CanRecord()) yield break;

        float[] samples = null;

        // Continuous mode has already applied the adaptive dB VAD and complete-
        // utterance boundary. Running the legacy linear VAD again can reject a
        // quiet sentence that the adaptive detector correctly accepted.
        if (enableVad && !useContinuousAtRuntime)
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
            AudioClip clip = Microphone.Start(micDevice, false, Mathf.Max(1, recordSeconds), activeSampleRate);
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

    IEnumerator ProcessAndUploadChunk(float[] rawSamples, bool alreadyVadSegmented = false)
    {
        if (rawSamples == null || rawSamples.Length == 0)
            yield break;

        int sourceRate = Mathf.Max(8000, activeSampleRate);
        float[] uploadSamples = sourceRate == UploadSampleRate
            ? rawSamples
            : ConvertToMonoAndResample(rawSamples, 1, sourceRate, UploadSampleRate);
        if (enableVad && !alreadyVadSegmented)
        {
            uploadSamples = ApplyVadTrim(uploadSamples, UploadSampleRate);
            if (!HasSpeech(uploadSamples))
            {
                Debug.LogWarning("[AudioUploader] VAD 沒偵測到語音，跳過上傳。");
                yield break;
            }
        }

        ApplyUploadGain(uploadSamples);

        float rms = ComputeRms(uploadSamples);
        float peak = ComputePeak(uploadSamples);
        Debug.Log($"[AudioUploader] upload chunk samples={uploadSamples.Length} rms={rms:F5} peak={peak:F5} vad={enableVad}");

        if (saveDebugWav && Debug.isDebugBuild)
            SaveDebugWav(uploadSamples);

        if (rms < minUploadRms)
        {
            Debug.Log($"[AudioUploader] skip low-rms chunk rms={rms:F5} < minUploadRms={minUploadRms:F5}");
            yield break;
        }

        byte[] wav = WavUtility.FromAudioFloat(uploadSamples, 1, UploadSampleRate);
        EnqueueUpload(CreateWorkItem(wav));
        yield break;
    }

    void SaveDebugWav(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return;

        try
        {
            byte[] wav = WavUtility.FromAudioFloat(samples, 1, UploadSampleRate);
            string path = Path.Combine(Path.GetTempPath(), "unity_mic_debug.wav");
            File.WriteAllBytes(path, wav);
            Debug.Log($"[AudioUploader] debug wav saved -> {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AudioUploader] debug wav save failed: {e.Message}");
        }
    }

    void ApplyUploadGain(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return;

        float gain = Mathf.Max(1f, uploadGain);
        if (Mathf.Approximately(gain, 1f))
            return;

        for (int i = 0; i < samples.Length; i++)
            samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
    }

    void EnsureUploadWorker()
    {
        if (uploadWorkerRoutine == null)
        {
            Debug.Log("[AudioUploader] EnsureUploadWorker -> start worker");
            uploadWorkerRoutine = StartCoroutine(UploadWorker());
        }
    }

    AudioUploadWorkItem CreateWorkItem(byte[] bytes)
    {
        StudentRunContext context = StudentRunContext.Current;
        if (!context.HasStudentRun || !context.StudentRunAccepted)
        {
            Debug.LogWarning("[AudioUploader] Speech was captured without an accepted Student Run; upload skipped.");
            return null;
        }

        ScenarioController scenario = FindObjectOfType<ScenarioController>();
        string scenarioStepId = scenario != null && scenario.CurrentStep != null
            ? scenario.CurrentStep.id ?? string.Empty
            : string.Empty;
        if (string.IsNullOrWhiteSpace(scenarioStepId))
        {
            blockingUploadFailure = true;
            lastUploadError = "Cannot upload /audio because Scenario CurrentStep.id is empty.";
            Debug.LogError("[AudioUploader] " + lastUploadError);
            NotifyProcessingStateChanged();
            return null;
        }

        return new AudioUploadWorkItem
        {
            wav = bytes,
            requestId = Guid.NewGuid().ToString(),
            scenarioStepId = scenarioStepId,
            studentRunId = context.ResultId
        };
    }

    void EnqueueUpload(AudioUploadWorkItem item)
    {
        if (item == null || item.wav == null || item.wav.Length == 0)
            return;

        int limit = Mathf.Max(1, maxUploadQueue);
        if (pendingUploads.Count >= limit)
        {
            if (!dropOldestOnQueueFull)
            {
                Debug.LogWarning($"[AudioUploader] upload queue full ({pendingUploads.Count}), drop newest chunk.");
                return;
            }

            AudioUploadWorkItem dropped = pendingUploads.Dequeue();
            CompleteRequest(dropped, "Upload queue full; oldest utterance was dropped.", true);
            Debug.LogWarning($"[AudioUploader] upload queue full ({limit}), drop oldest chunk.");
        }

        pendingUploads.Enqueue(item);
        outstandingRequestCount++;
        Debug.Log($"[AudioUploader] enqueue requestId={item.requestId} wavBytes={item.wav.Length} queue={pendingUploads.Count}");
        NotifyProcessingStateChanged();
        EnsureUploadWorker();
    }

    IEnumerator UploadWorker()
    {
        Debug.Log("[AudioUploader] UploadWorker started");
        while (true)
        {
            if (pendingUploads.Count == 0)
            {
                if (loopRoutine == null)
                    break;

                yield return null;
                continue;
            }

            Debug.Log($"[AudioUploader] UploadWorker dequeue queue_before={pendingUploads.Count}");
            AudioUploadWorkItem item = pendingUploads.Dequeue();
            Debug.Log($"[AudioUploader] UploadWorker sending requestId={item.requestId} bytes={item.wav.Length} queue_after={pendingUploads.Count}");
            uploadInProgress = true;
            NotifyProcessingStateChanged();
            yield return Upload(item);
            uploadInProgress = false;
            NotifyProcessingStateChanged();
        }

        Debug.Log("[AudioUploader] UploadWorker stopped");
        uploadWorkerRoutine = null;
    }

    bool StartContinuousMic()
    {
        StopContinuousMic();
        if (Microphone.devices == null || Microphone.devices.Length == 0) return false;

        int lengthSec = Mathf.Max(2, continuousBufferSeconds);
        liveClip = Microphone.Start(micDevice, true, lengthSec, activeSampleRate);
        if (!liveClip) return false;

        activeSampleRate = liveClip.frequency > 0 ? liveClip.frequency : activeSampleRate;
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

        int channels = Mathf.Max(1, liveClip.channels);
        int first = Mathf.Min(available, clipSamples - liveReadPos);
        float[] interleaved = new float[available * channels];
        float[] head = new float[first * channels];
        liveClip.GetData(head, liveReadPos);
        Array.Copy(head, 0, interleaved, 0, head.Length);

        if (first < available)
        {
            float[] tail = new float[(available - first) * channels];
            liveClip.GetData(tail, 0);
            Array.Copy(tail, 0, interleaved, head.Length, tail.Length);
        }

        liveReadPos = currentPos;
        if (channels == 1)
            return interleaved;

        var mono = new float[available];
        for (int frame = 0; frame < available; frame++)
        {
            float sum = 0f;
            int offset = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                sum += interleaved[offset + channel];
            mono[frame] = sum / channels;
        }
        return mono;
    }

    IEnumerator CaptureUsingVad(Action<float[]> onFinished)
    {
        float maxDuration = Mathf.Max(1, recordSeconds);
        AudioClip clip = Microphone.Start(micDevice, false, Mathf.CeilToInt(maxDuration), activeSampleRate);
        if (!clip)
        {
            onFinished?.Invoke(null);
            yield break;
        }

        while (Microphone.GetPosition(micDevice) <= 0) yield return null;

        int frameSamples = Mathf.Max(1, Mathf.RoundToInt(vadFrameMs * 0.001f * activeSampleRate));
        int preRollSamples = Mathf.Max(0, Mathf.RoundToInt(vadPreRollMs * 0.001f * activeSampleRate));
        int endPaddingSamples = Mathf.Max(0, Mathf.RoundToInt(endPaddingMs * 0.001f * activeSampleRate));
        int minSpeechSamples = Mathf.Max(0, Mathf.RoundToInt(vadMinSpeechMs * 0.001f * activeSampleRate));
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

    IEnumerator Upload(AudioUploadWorkItem item)
    {
        if (item == null)
            yield break;

        if (!IsCurrentStudentRun(item))
        {
            CompleteRequest(item, "Student Run changed before /audio upload completed.", false);
            yield break;
        }

        Debug.Log($"[AudioUploader] Upload begin requestId={item.requestId} url={serverUrl} bytes={item.wav?.Length ?? 0}");
        using (UnityWebRequest req = new UnityWebRequest(serverUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(item.wav);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "audio/wav");
            req.SetRequestHeader("X-Request-Id", item.requestId);
            req.SetRequestHeader("X-Scenario-Step-Id", item.scenarioStepId ?? string.Empty);
            req.SetRequestHeader("X-Student-Run-Id", item.studentRunId ?? string.Empty);
            req.timeout = Mathf.Max(1, uploadTimeoutSeconds);
            Debug.Log("[AudioUploader] SendWebRequest start");
            yield return req.SendWebRequest();
            Debug.Log("[AudioUploader] SendWebRequest finished");

#if UNITY_2020_2_OR_NEWER
            bool transportSuccess = req.result == UnityWebRequest.Result.Success;
#else
            bool transportSuccess = !req.isNetworkError && !req.isHttpError;
#endif
            if (!transportSuccess)
            {
                string failure = $"HTTP {req.responseCode} {req.error}";
                if (IsTransientFailure(req.responseCode))
                {
                    yield return RetrySameRequest(item, failure);
                    yield break;
                }

                CompleteRequest(item, failure, true);
                yield break;
            }

            if (!IsCurrentStudentRun(item))
            {
                CompleteRequest(item, "Student Run changed while /audio was in flight.", false);
                yield break;
            }

            string json = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            AudioAnalysisResponse response;
            string validationError;
            if (!AudioAnalysisResponseContract.TryParseAndValidate(
                json, item.requestId, item.scenarioStepId, item.studentRunId,
                out response, out validationError))
            {
                CompleteRequest(item, validationError, true);
                yield break;
            }

            bool firstDelivery = RememberDeliveredRequestId(response.requestId);
            if (response.ignored)
            {
                Debug.Log($"[AudioUploader] ignored requestId={response.requestId}; no emotion or ToneScore update.");
            }
            else if (firstDelivery)
            {
                AudioResponseAccepted?.Invoke(response, json);
                Debug.Log($"[AudioUploader] accepted requestId={response.requestId} duplicate={response.duplicate} state={response.ResolvedKidEmotionState}");
            }
            else
            {
                Debug.Log($"[AudioUploader] duplicate delivery suppressed requestId={response.requestId}");
            }

            CompleteRequest(item, string.Empty, false);
        }
    }

    IEnumerator RetrySameRequest(AudioUploadWorkItem item, string failure)
    {
        item.retryCount++;
        bool retryAllowed = maxUploadRetries <= 0 || item.retryCount <= maxUploadRetries;
        if (!retryAllowed)
        {
            CompleteRequest(item, failure + "; automatic retry limit reached.", true);
            yield break;
        }

        lastUploadError = failure;
        Debug.LogWarning($"[AudioUploader] transient /audio failure requestId={item.requestId}; retry={item.retryCount} with the same requestId. {failure}");
        NotifyProcessingStateChanged();
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, uploadRetryDelaySeconds));

        if (!IsCurrentStudentRun(item))
        {
            CompleteRequest(item, "Student Run changed before /audio retry.", false);
            yield break;
        }

        pendingUploads.Enqueue(item);
    }

    static bool IsTransientFailure(long responseCode)
    {
        return responseCode == 0 || responseCode == 408 || responseCode == 429 || responseCode >= 500;
    }

    static bool IsCurrentStudentRun(AudioUploadWorkItem item)
    {
        StudentRunContext context = StudentRunContext.Current;
        return item != null && context.HasStudentRun && context.StudentRunAccepted &&
            string.Equals(context.ResultId, item.studentRunId, StringComparison.Ordinal);
    }

    bool RememberDeliveredRequestId(string requestId)
    {
        return !string.IsNullOrEmpty(requestId) && deliveredRequestIds.Add(requestId);
    }

    void CompleteRequest(AudioUploadWorkItem item, string error, bool blocking)
    {
        outstandingRequestCount = Mathf.Max(0, outstandingRequestCount - 1);
        if (!string.IsNullOrEmpty(error))
        {
            lastUploadError = error;
            if (blocking)
                blockingUploadFailure = true;
            Debug.LogError($"[AudioUploader] requestId={item?.requestId} failed: {error}");
        }
        NotifyProcessingStateChanged();
    }

    void NotifyProcessingStateChanged()
    {
        AudioProcessingStateChanged?.Invoke();
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

    static float[] ConvertToMonoAndResample(
        float[] interleaved, int channels, int sourceRate, int targetRate)
    {
        if (interleaved == null || interleaved.Length == 0)
            return Array.Empty<float>();

        channels = Mathf.Max(1, channels);
        sourceRate = Mathf.Max(1, sourceRate);
        targetRate = Mathf.Max(1, targetRate);
        int sourceFrames = interleaved.Length / channels;
        if (sourceFrames <= 0)
            return Array.Empty<float>();

        var mono = new float[sourceFrames];
        for (int frame = 0; frame < sourceFrames; frame++)
        {
            float sum = 0f;
            int offset = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                sum += interleaved[offset + channel];
            mono[frame] = sum / channels;
        }

        if (sourceRate == targetRate || sourceFrames == 1)
            return mono;

        int targetFrames = Mathf.Max(1,
            Mathf.RoundToInt(sourceFrames * (targetRate / (float)sourceRate)));
        var result = new float[targetFrames];
        float scale = (sourceFrames - 1f) / Mathf.Max(1, targetFrames - 1);
        for (int frame = 0; frame < targetFrames; frame++)
        {
            float sourcePosition = frame * scale;
            int left = Mathf.FloorToInt(sourcePosition);
            int right = Mathf.Min(left + 1, sourceFrames - 1);
            result[frame] = Mathf.Lerp(mono[left], mono[right], sourcePosition - left);
        }
        return result;
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

[Serializable]
sealed class AudioUploadWorkItem
{
    public byte[] wav;
    public string requestId;
    public string scenarioStepId;
    public string studentRunId;
    public int retryCount;
}
