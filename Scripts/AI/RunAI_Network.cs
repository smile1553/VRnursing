using System;
using UnityEngine;
using System.Threading.Tasks;

public class RunAI_Network : MonoBehaviour
{
    [Header("Auto Discovery")]
    public string serverBaseUrl = "";   // Leave empty to auto-discover the emotion server.
    public float intervalSec = 1f;
    public bool autoUploadOnConnect = true;
    public bool useWebSocketFeed = true;
    public bool logIncomingJson = false;

    [Header("Components")]
    public RunAI runAi;                 // Optional; auto-linked from the scene if empty.
    public AudioUploader audioUploader; // Optional; auto-linked from the scene if empty.

    IEmotionFeed feed;
    string _lastLoggedJson;

    public event Action<string> EmotionJsonReceived;

    async void Start()
    {
        _lastLoggedJson = null;

        if (audioUploader == null)
        {
            audioUploader = FindObjectOfType<AudioUploader>();
            if (audioUploader == null)
                Debug.LogError("[RunAI_Network] AudioUploader is missing. /audio POST will never start.");
            else
                Debug.Log("[RunAI_Network] Auto-linked AudioUploader from scene.");
        }

        if (runAi == null)
        {
            runAi = FindObjectOfType<RunAI>();
            if (runAi == null)
                Debug.LogWarning("[RunAI_Network] RunAI not found. Feed JSON will not be applied to avatar.");
        }

        // 1) Resolve the server URL. Prefer the Inspector value, then cached value, then UDP discovery.
        if (string.IsNullOrEmpty(serverBaseUrl))
        {
            // Use cached discovery result first.
            serverBaseUrl = PlayerPrefs.GetString("emo_server_url", "");
            if (string.IsNullOrEmpty(serverBaseUrl))
            {
                Debug.Log("[DISCOVERY] scanning...");
                var url = await ServerDiscovery.FindServerUrlAsync();
                if (!string.IsNullOrEmpty(url))
                {
                    serverBaseUrl = url; // e.g. http://192.168.0.50:8000
                    PlayerPrefs.SetString("emo_server_url", serverBaseUrl);
                    PlayerPrefs.Save();
                    Debug.Log("[DISCOVERY] found " + serverBaseUrl);
                }
                else
                {
                    // Fallback for local editor testing.
                    serverBaseUrl = "http://127.0.0.1:8000";
                    Debug.LogWarning("[DISCOVERY] not found, fallback " + serverBaseUrl);
                }
            }
            else Debug.Log("[DISCOVERY] use cached " + serverBaseUrl);
        }

        // 2) Configure the /audio endpoint for AudioUploader.
        if (audioUploader != null)
        {
            audioUploader.serverUrl = serverBaseUrl.TrimEnd('/') + "/audio";
            Debug.Log($"[RunAI_Network] audio url = {audioUploader.serverUrl}");
            Debug.Log($"[RunAI_Network] autoUploadOnConnect = {autoUploadOnConnect}");
            if (autoUploadOnConnect)
            {
                audioUploader.StartLoop();
                Debug.Log("[RunAI_Network] StartLoop() called.");
            }
            else
            {
                Debug.LogWarning("[RunAI_Network] autoUploadOnConnect is false, so /audio POST will not run automatically.");
            }
        }
        else
        {
            Debug.LogError("[RunAI_Network] audioUploader is null. Please bind it in Inspector.");
        }

        // 3) Start the selected emotion feed.
        feed?.Stop();
        feed = useWebSocketFeed ? (IEmotionFeed)new WsEmotionFeed() : new HttpEmotionFeed();

        if (!useWebSocketFeed && feed is HttpEmotionFeed http)
        {
            http.intervalSec = intervalSec;
        }

        feed.Start(this, serverBaseUrl, OnJson);
    }

    void OnDestroy()
    {
        feed?.Stop();
        feed = null;
        audioUploader?.StopLoop();
    }

    void OnJson(string json)
    {
        if (logIncomingJson && !string.IsNullOrEmpty(json))
        {
            if (!string.Equals(json, _lastLoggedJson))
            {
                Debug.Log("[EmotionFeed] " + json);
                _lastLoggedJson = json;
            }
        }

        if (runAi != null) runAi.ApplyJson(json);
        EmotionJsonReceived?.Invoke(json);
    }

    // Optional entry point for a UI button to rescan the server.
    public void RescanServer()
    {
        PlayerPrefs.DeleteKey("emo_server_url");
        serverBaseUrl = "";
        Start(); // Simple restart path for discovery.
    }
}
