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
    int _initGeneration;
    bool _destroyed;

    public event Action<string> EmotionJsonReceived;

    void Start()
    {
        _ = InitializeAsync(false);
    }

    async Task InitializeAsync(bool forceRescan)
    {
        int generation = ++_initGeneration;
        _lastLoggedJson = null;
        StopRuntime();

        if (audioUploader == null)
        {
            audioUploader = FindObjectOfType<AudioUploader>();
            if (audioUploader == null)
                Debug.LogError("[RunAI_Network] AudioUploader is missing. /audio POST will never start.");
            else
                RuntimeLog.Info("[RunAI_Network] Auto-linked AudioUploader from scene.");
        }

        if (runAi == null)
        {
            runAi = FindObjectOfType<RunAI>();
            if (runAi == null)
                RuntimeLog.Warning("[RunAI_Network] RunAI not found. Feed JSON will not be applied to avatar.");
        }

        // 1) Resolve the server URL. Prefer the Inspector value, then cached value, then UDP discovery.
        if (forceRescan)
        {
            PlayerPrefs.DeleteKey("emo_server_url");
            serverBaseUrl = "";
        }

        string resolvedUrl = serverBaseUrl;
        if (string.IsNullOrEmpty(resolvedUrl))
        {
            // Use cached discovery result first.
            resolvedUrl = PlayerPrefs.GetString("emo_server_url", "");
            if (string.IsNullOrEmpty(resolvedUrl))
            {
                RuntimeLog.Info("[DISCOVERY] scanning...");
                var url = await ServerDiscovery.FindServerUrlAsync();
                if (_destroyed || generation != _initGeneration)
                    return;

                if (!string.IsNullOrEmpty(url))
                {
                    resolvedUrl = url; // e.g. http://192.168.0.50:8000
                    PlayerPrefs.SetString("emo_server_url", resolvedUrl);
                    PlayerPrefs.Save();
                    RuntimeLog.Info("[DISCOVERY] found " + resolvedUrl);
                }
                else
                {
                    // Fallback for local editor testing.
                    resolvedUrl = "http://127.0.0.1:8000";
                    RuntimeLog.Warning("[DISCOVERY] not found, fallback " + resolvedUrl);
                }
            }
            else RuntimeLog.Info("[DISCOVERY] use cached " + resolvedUrl);
        }

        serverBaseUrl = resolvedUrl;

        // 2) Configure the /audio endpoint for AudioUploader.
        if (audioUploader != null)
        {
            audioUploader.serverUrl = serverBaseUrl.TrimEnd('/') + "/audio";
            RuntimeLog.Info($"[RunAI_Network] audio url = {audioUploader.serverUrl}");
            RuntimeLog.Info($"[RunAI_Network] autoUploadOnConnect = {autoUploadOnConnect}");
            if (autoUploadOnConnect)
            {
                audioUploader.StartLoop();
                RuntimeLog.Info("[RunAI_Network] StartLoop() called.");
            }
            else
            {
                RuntimeLog.Warning("[RunAI_Network] autoUploadOnConnect is false, so /audio POST will not run automatically.");
            }
        }
        else
        {
            Debug.LogError("[RunAI_Network] audioUploader is null. Please bind it in Inspector.");
        }

        // 3) Start the selected emotion feed.
        feed = useWebSocketFeed ? (IEmotionFeed)new WsEmotionFeed() : new HttpEmotionFeed();

        if (!useWebSocketFeed && feed is HttpEmotionFeed http)
        {
            http.intervalSec = intervalSec;
        }

        feed.Start(this, serverBaseUrl, OnJson);
    }

    void OnDestroy()
    {
        _destroyed = true;
        _initGeneration++;
        StopRuntime();
    }

    void StopRuntime()
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
                RuntimeLog.Info("[EmotionFeed] " + json);
                _lastLoggedJson = json;
            }
        }

        if (runAi != null) runAi.ApplyJson(json);
        EmotionJsonReceived?.Invoke(json);
    }

    // Optional entry point for a UI button to rescan the server.
    public void RescanServer()
    {
        _ = InitializeAsync(true);
    }
}
