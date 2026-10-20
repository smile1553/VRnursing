using System;
using UnityEngine;
using UnityEngine.Events;

public class PediatricVitalSignsPart0Flow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MedicalRecordCloseFlow medicalRecordCloseFlow;
    [SerializeField] private DoorSlideOpener doorOpener;
    [SerializeField] private TrackPointGuide doorGreetingGuide;
    [SerializeField] private RunAI_Network network;
    [SerializeField] private AudioUploader audioUploader;

    [Header("Backend")]
    [SerializeField] private bool advanceFromBackendJson = true;
    [Tooltip("Concept groups use ';'. Variants inside one group use '|'.")]
    [SerializeField] private string greetingKeywords = "你好|您好|哈囉|哈啰|嗨|早安|午安|你們好|大家好;媽媽|芽芽|小朋友|家屬";
    [SerializeField] private string greetingActionTags = "introduce_bp_exam";
    [Range(0f, 1f)] [SerializeField] private float minSemanticConfidence = 0.65f;
    [SerializeField] private bool ignoreFirstBackendJson = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onGreetingVoiceGateStarted;
    [SerializeField] private UnityEvent onDoorOpened;

    private bool waitingForGuideArrival;
    private bool waitingForGreeting;
    private bool completed;
    private string lastProcessedBackendJson;
    private string initialBackendSpeechToIgnore;

    private void Awake()
    {
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (audioUploader == null)
            audioUploader = FindObjectOfType<AudioUploader>();

        if (medicalRecordCloseFlow == null)
            medicalRecordCloseFlow = FindObjectOfType<MedicalRecordCloseFlow>(true);

        doorGreetingGuide?.Hide();
    }

    private void OnEnable()
    {
        SubscribeToAudioResponse();
    }

    private void OnDisable()
    {
        if (audioUploader != null)
            audioUploader.AudioResponseAccepted -= HandleAudioResponse;
    }

    private void SubscribeToAudioResponse()
    {
        if (audioUploader == null)
            audioUploader = FindObjectOfType<AudioUploader>();
        if (audioUploader == null)
            return;

        audioUploader.AudioResponseAccepted -= HandleAudioResponse;
        audioUploader.AudioResponseAccepted += HandleAudioResponse;
    }

    private void HandleAudioResponse(AudioAnalysisResponse response, string rawJson)
    {
        if (!advanceFromBackendJson || !waitingForGreeting || completed ||
            response == null || response.ignored ||
            !string.Equals(response.source, "student_speech", StringComparison.OrdinalIgnoreCase))
            return;

        string reason;
        if (SpeechGateMatcher.IsMatch(
            response, greetingKeywords, greetingActionTags, out reason, minSemanticConfidence))
        {
            Debug.Log($"[Part0] Greeting matched ({reason}). Opening door. text={response.text}", this);
            CompleteGreetingAndOpenDoor();
        }
        else
        {
            Debug.Log($"[Part0] Greeting did not match ({reason}). text={response.text}", this);
        }
    }

    private void Update()
    {
        if (!advanceFromBackendJson || !waitingForGreeting || completed)
            return;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (network == null || string.IsNullOrWhiteSpace(network.LastJson))
            return;

        HandleBackendJson(network.LastJson);
    }

    public void StartPart0()
    {
        StartPart0(null);
    }

    public void StartPart0(MedicalRecordCloseFlow sourceCloseFlow)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        enabled = true;

        if (sourceCloseFlow != null)
            medicalRecordCloseFlow = sourceCloseFlow;

        completed = false;
        waitingForGreeting = false;
        waitingForGuideArrival = true;
        ResetBackendGate("part0 start");

        if (doorGreetingGuide != null)
        {
            doorGreetingGuide.Show();
            Debug.Log("[Part0] Door greeting guide shown. Waiting for player arrival.", this);
        }
        else
        {
            Debug.LogWarning("[Part0] Door greeting guide is missing, starting greeting voice gate immediately.", this);
            EnableGreetingVoiceGate();
        }
    }

    public void EnableGreetingVoiceGate()
    {
        if (completed)
            return;

        waitingForGuideArrival = false;
        waitingForGreeting = true;
        ResetBackendGate("door guide arrival");
        onGreetingVoiceGateStarted?.Invoke();
        Debug.Log("[Part0] Player arrived at the door. Waiting for greeting speech.", this);
    }

    public void DebugSkipGreeting()
    {
        CompleteGreetingAndOpenDoor();
    }

    private void HandleBackendJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || string.Equals(json, lastProcessedBackendJson, StringComparison.Ordinal))
            return;

        lastProcessedBackendJson = json;
        AudioAnalysisResponse response;
        string reason;
        if (SpeechGateMatcher.TryParseAndMatch(
            json, greetingKeywords, greetingActionTags, out response, out reason, minSemanticConfidence))
        {
            Debug.Log($"[Part0] Greeting matched ({reason}). Opening door. text={response.text}", this);
            CompleteGreetingAndOpenDoor();
        }
        else if (response != null)
        {
            Debug.Log($"[Part0] Greeting did not match ({reason}). text={response.text}", this);
        }
    }

    private void CompleteGreetingAndOpenDoor()
    {
        if (completed)
            return;

        completed = true;
        waitingForGuideArrival = false;
        waitingForGreeting = false;
        doorGreetingGuide?.Hide();

        if (medicalRecordCloseFlow != null)
        {
            medicalRecordCloseFlow.OpenDoorAfterPart0();
        }
        else if (doorOpener != null)
        {
            doorOpener.Open();
            onDoorOpened?.Invoke();
        }
        else
        {
            Debug.LogWarning("[Part0] No MedicalRecordCloseFlow or DoorSlideOpener is assigned, so the door cannot open.", this);
        }
    }

    private void ResetBackendGate(string reason)
    {
        lastProcessedBackendJson = null;
        initialBackendSpeechToIgnore = null;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();

        if (network != null && ignoreFirstBackendJson && !string.IsNullOrWhiteSpace(network.LastJson))
        {
            lastProcessedBackendJson = network.LastJson;
            initialBackendSpeechToIgnore = ExtractBackendSpeechText(network.LastJson);
            Debug.Log("[Part0] Cached backend speech ignored on gate release (" + reason + "): " + initialBackendSpeechToIgnore, this);
        }
    }

    private static string ExtractBackendSpeechText(string json)
    {
        try
        {
            BackendSpeechResponse response = JsonUtility.FromJson<BackendSpeechResponse>(json);
            if (response != null)
            {
                string combinedText = string.Empty;

                if (!string.IsNullOrWhiteSpace(response.llm_window_text))
                    combinedText += response.llm_window_text + " ";

                if (!string.IsNullOrWhiteSpace(response.text))
                    combinedText += response.text + " ";

                if (!string.IsNullOrWhiteSpace(response.raw_text))
                    combinedText += response.raw_text;

                return combinedText.Trim();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Part0] Failed to parse backend json: " + e.Message);
        }

        return string.Empty;
    }

    [Serializable]
    private class BackendSpeechResponse
    {
        public string text;
        public string raw_text;
        public string llm_window_text;
    }
}
