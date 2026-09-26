#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class AudioDirectIntegrationTests
{
    const string ValidJson =
        "{\"ok\":true,\"ignored\":false,\"duplicate\":false," +
        "\"requestId\":\"request-1\",\"utteranceId\":\"utterance-1\"," +
        "\"scenarioStepId\":\"step-1\",\"studentRunId\":\"run-1\"," +
        "\"source\":\"student_speech\"," +
        "\"text\":\"不用怕\",\"tension\":0,\"patienceScore\":0," +
        "\"previousKidEmotionState\":\"Calm\",\"emotionState\":\"Uneasy\"," +
        "\"intent\":\"reassure\",\"actionTag\":\"comfort\"," +
        "\"confidence\":0,\"coercion\":0,\"processingMs\":45.3}";

    [Test]
    public void ResponseContract_AcceptsRequiredZeroValues()
    {
        AudioAnalysisResponse response;
        string error;
        Assert.IsTrue(AudioAnalysisResponseContract.TryParseAndValidate(
            ValidJson, "request-1", "step-1", "run-1", out response, out error), error);
        Assert.AreEqual("Uneasy", response.ResolvedKidEmotionState);
    }

    [Test]
    public void ResponseContract_RejectsMissingPropertyAndWrongRequestId()
    {
        AudioAnalysisResponse response;
        string error;
        string missing = ValidJson.Replace("\"processingMs\":45.3", "\"other\":0");
        Assert.IsFalse(AudioAnalysisResponseContract.TryParseAndValidate(
            missing, "request-1", "step-1", "run-1", out response, out error));
        Assert.IsFalse(AudioAnalysisResponseContract.TryParseAndValidate(
            ValidJson, "request-other", "step-1", "run-1", out response, out error));
        Assert.IsFalse(AudioAnalysisResponseContract.TryParseAndValidate(
            ValidJson, "request-1", "step-1", "run-other", out response, out error));
    }

    [Test]
    public void DirectResponse_UpdatesExistingToneScoreOnlyOnce()
    {
        var go = new GameObject("AudioDirectIntegrationTest");
        try
        {
            var audio = go.AddComponent<AudioUploader>();
            var emotion = go.AddComponent<EmotionStateManager>();
            var scenario = go.AddComponent<ScenarioController>();
            var score = go.AddComponent<PerformanceScoreManager>();
            score.controller = scenario;
            score.emotionState = emotion;
            score.enabled = false;
            score.enabled = true;
            score.ResetScores();

            MethodInfo scoreHandler = typeof(PerformanceScoreManager).GetMethod(
                "OnEmotionChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(scoreHandler);
            emotion.OnEmotionChanged += snapshot => scoreHandler.Invoke(score, new object[] { snapshot });

            MethodInfo handler = typeof(EmotionStateManager).GetMethod(
                "HandleAudioResponse", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler);

            var response = new AudioAnalysisResponse
            {
                ok = true,
                requestId = "request-1",
                utteranceId = "utterance-1",
                scenarioStepId = "step-1",
                source = "student_speech",
                text = "不用怕",
                previousKidEmotionState = "Calm",
                emotionState = "Uneasy"
            };

            handler.Invoke(emotion, new object[] { response, ValidJson });
            handler.Invoke(emotion, new object[] { response, ValidJson });

            Assert.AreEqual(19, score.ToneScore);
            Assert.AreEqual(1, score.EmotionPenaltyTotal);
            Assert.IsTrue(audio.IsAudioProcessingIdle);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void IgnoredResponse_DoesNotUpdateEmotionOrToneScore()
    {
        var go = new GameObject("AudioIgnoredResponseTest");
        try
        {
            go.AddComponent<AudioUploader>();
            var emotion = go.AddComponent<EmotionStateManager>();
            var score = go.AddComponent<PerformanceScoreManager>();
            score.enabled = false;
            score.emotionState = emotion;
            score.enabled = true;
            score.ResetScores();

            MethodInfo handler = typeof(EmotionStateManager).GetMethod(
                "HandleAudioResponse", BindingFlags.Instance | BindingFlags.NonPublic);
            handler.Invoke(emotion, new object[]
            {
                new AudioAnalysisResponse { ok = true, ignored = true, requestId = "ignored-1" },
                "{\"ok\":true,\"ignored\":true}"
            });

            Assert.IsNull(emotion.Current);
            Assert.AreEqual(20, score.ToneScore);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ScenarioCompletion_WaitsWhileAudioRequestIsOutstanding()
    {
        var go = new GameObject("AudioCompletionWaitTest");
        try
        {
            var audio = go.AddComponent<AudioUploader>();
            var score = go.AddComponent<PerformanceScoreManager>();
            var uploader = go.AddComponent<StudentResultUploader>();
            uploader.audioUploader = audio;
            uploader.scoreManager = score;

            StudentRunContext.Current.SetSession("session-1", "2026-09-25T00:00:00+08:00");
            StudentRunContext.Current.BeginStudentRun("student-1");
            StudentRunContext.Current.StudentRunAccepted = true;
            StudentRunContext.Current.ScenarioCompleted = true;

            FieldInfo outstanding = typeof(AudioUploader).GetField(
                "outstandingRequestCount", BindingFlags.Instance | BindingFlags.NonPublic);
            outstanding.SetValue(audio, 1);

            MethodInfo freeze = typeof(StudentResultUploader).GetMethod(
                "TryFreezeAndUpload", BindingFlags.Instance | BindingFlags.NonPublic);
            freeze.Invoke(uploader, null);

            Assert.IsNull(StudentRunContext.Current.PendingResult);
            StringAssert.Contains("waiting for 1 /audio request", uploader.LastStatusMessage);
        }
        finally
        {
            StudentRunContext.Current.ClearSession();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void PushToTalk_WithoutServer_IsSafeAndDoesNotStartRecording()
    {
        var go = new GameObject("PushToTalkNoServerTest");
        try
        {
            var audio = go.AddComponent<AudioUploader>();
            StudentRunContext.Current.SetSession("session-ptt", "2026-09-25T00:00:00+08:00");
            StudentRunContext.Current.BeginStudentRun("student-ptt");
            StudentRunContext.Current.StudentRunAccepted = true;

            LogAssert.Expect(LogType.Warning,
                "[AudioUploader] Push-to-Talk start ignored: server is not connected.");
            audio.StartPushToTalk();
            audio.StopPushToTalkAndUpload();

            Assert.IsFalse(audio.IsPushToTalkRecording);
            Assert.IsTrue(audio.IsAudioProcessingIdle);
        }
        finally
        {
            StudentRunContext.Current.ClearSession();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void PushToTalkAudio_IsConvertedTo16KhzMonoPcm16Wav()
    {
        const int sourceFrames = 4800;
        var stereo48Khz = new float[sourceFrames * 2];
        for (int frame = 0; frame < sourceFrames; frame++)
        {
            stereo48Khz[frame * 2] = 0.25f;
            stereo48Khz[frame * 2 + 1] = -0.25f;
        }

        MethodInfo convert = typeof(AudioUploader).GetMethod(
            "ConvertToMonoAndResample", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(convert);
        var mono16Khz = (float[])convert.Invoke(null,
            new object[] { stereo48Khz, 2, 48000, 16000 });
        Assert.AreEqual(1600, mono16Khz.Length);

        byte[] wav = WavUtility.FromAudioFloat(mono16Khz, 1, 16000);
        Assert.AreEqual(1, System.BitConverter.ToInt16(wav, 22));
        Assert.AreEqual(16000, System.BitConverter.ToInt32(wav, 24));
        Assert.AreEqual(16, System.BitConverter.ToInt16(wav, 34));
    }
}
#endif
