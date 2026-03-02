using System;
using UnityEngine;
using System.Threading.Tasks;

public class RunAI_Network : MonoBehaviour
{
    [Header("Auto Discovery")]
    public string serverBaseUrl = "";   // ?™ç©º?’å??•æ??ªå??¢æ¸¬ï¼›æ?å¡«å›ºå®šå€¼è???
    public float intervalSec = 1f;
    public bool autoUploadOnConnect = true;
    public bool useWebSocketFeed = true;
    public bool logIncomingJson = false;

    [Header("Components")]
    public RunAI runAi;                 // è§’è‰²ä¸Šç? RunAIï¼ˆæ??²ä?ï¼?
    public AudioUploader audioUploader; // ?´æ™¯ä¸Šç? AudioUploaderï¼ˆæ??²ä?ï¼?

    IEmotionFeed feed;
    string _lastLoggedJson;

    public event Action<string> EmotionJsonReceived;

    async void Start()
    {
        _lastLoggedJson = null;
        // 1) ?¾ä¼º?å™¨ URLï¼ˆç”¨ä½ ä??å???UDP ?¢æ¸¬ï¼›é€™è£¡çµ¦æ?ç°¡å–®æµç?ï¼?
        if (string.IsNullOrEmpty(serverBaseUrl))
        {
            // ?ˆè?å¿«å?
            serverBaseUrl = PlayerPrefs.GetString("emo_server_url", "");
            if (string.IsNullOrEmpty(serverBaseUrl))
            {
                Debug.Log("[DISCOVERY] scanning...");
                var url = await ServerDiscovery.FindServerUrlAsync(); // ä½ å??¢å???B-1
                if (!string.IsNullOrEmpty(url))
                {
                    serverBaseUrl = url; // e.g. http://192.168.0.50:8000
                    PlayerPrefs.SetString("emo_server_url", serverBaseUrl);
                    PlayerPrefs.Save();
                    Debug.Log("[DISCOVERY] found " + serverBaseUrl);
                }
                else
                {
                    // ?¾ä??°å°±?¨æœ¬æ©Ÿï??¹ä¾¿??Editor æ¸?
                    serverBaseUrl = "http://127.0.0.1:8000";
                    Debug.LogWarning("[DISCOVERY] not found, fallback " + serverBaseUrl);
                }
            }
            else Debug.Log("[DISCOVERY] use cached " + serverBaseUrl);
        }

        // 2) ??/audio URL ?‡çµ¦ AudioUploaderï¼ˆâ? ?é?ï¼?
        if (audioUploader != null)
        {
            audioUploader.serverUrl = serverBaseUrl.TrimEnd('/') + "/audio";
            if (autoUploadOnConnect)
                audioUploader.StartLoop();
        }

        // 3) ?Ÿå??…ç?è³‡æ?ä¾†æ?ï¼ˆHTTP è¼ªè©¢??WebSocket ?¨æ’­ï¼?
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

        runAi.ApplyJson(json);
        EmotionJsonReceived?.Invoke(json);
    }

    // ?¯é¸ï¼šæ?ä¾›ä??‹é??°æ??æ–¹æ³•ï??šæ? UI ?‰é?
    public void RescanServer()
    {
        PlayerPrefs.DeleteKey("emo_server_url");
        serverBaseUrl = "";
        Start(); // ç°¡å–®ç²—æš´?„é??Ÿæ?ç¨?
    }
}
