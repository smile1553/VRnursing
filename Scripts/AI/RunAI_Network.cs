using System;
using UnityEngine;
using System.Threading.Tasks;

public class RunAI_Network : MonoBehaviour
{
    [Header("Auto Discovery")]
    public string serverBaseUrl = "";   // 留空→啟動時自動探測；或填固定值覆蓋
    public float intervalSec = 1f;
    public bool autoUploadOnConnect = true;
    public bool useWebSocketFeed = true;
    public bool logIncomingJson = false;

    [Header("Components")]
    public RunAI runAi;                 // 角色上的 RunAI（拖進來）
    public AudioUploader audioUploader; // 場景上的 AudioUploader（拖進來）

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

        // 1) 找伺服器 URL（用你之前做的 UDP 探測；這裡給最簡單流程）
        if (string.IsNullOrEmpty(serverBaseUrl))
        {
            // 先讀快取
            serverBaseUrl = PlayerPrefs.GetString("emo_server_url", "");
            if (string.IsNullOrEmpty(serverBaseUrl))
            {
                Debug.Log("[DISCOVERY] scanning...");
                var url = await ServerDiscovery.FindServerUrlAsync(); // 你前面做的 B-1
                if (!string.IsNullOrEmpty(url))
                {
                    serverBaseUrl = url; // e.g. http://192.168.0.50:8000
                    PlayerPrefs.SetString("emo_server_url", serverBaseUrl);
                    PlayerPrefs.Save();
                    Debug.Log("[DISCOVERY] found " + serverBaseUrl);
                }
                else
                {
                    // 找不到就用本機，方便在 Editor 測
                    serverBaseUrl = "http://127.0.0.1:8000";
                    Debug.LogWarning("[DISCOVERY] not found, fallback " + serverBaseUrl);
                }
            }
            else Debug.Log("[DISCOVERY] use cached " + serverBaseUrl);
        }

        // 2) 把 /audio URL 指給 AudioUploader（← 重點）
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

        // 3) 啟動情緒資料來源（HTTP 輪詢或 WebSocket 推播）
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

    // 可選：提供一個重新掃描方法，做成 UI 按鈕
    public void RescanServer()
    {
        PlayerPrefs.DeleteKey("emo_server_url");
        serverBaseUrl = "";
        Start(); // 簡單粗暴的重啟流程
    }
}
