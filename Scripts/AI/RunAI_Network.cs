using System;
using UnityEngine;
using System.Threading.Tasks;

public class RunAI_Network : MonoBehaviour
{
    [Header("Auto Discovery")]
    public string serverBaseUrl = "";   // ?ôÁ©∫?íÂ??ïÊ??™Â??¢Ê∏¨ÔºõÊ?Â°´Âõ∫ÂÆöÂÄºË???
    public float intervalSec = 1f;
    public bool autoUploadOnConnect = true;
    public bool useWebSocketFeed = true;
    public bool logIncomingJson = false;

    [Header("Components")]
    public RunAI runAi;                 // ËßíËâ≤‰∏äÁ? RunAIÔºàÊ??≤‰?Ôº?
    public AudioUploader audioUploader; // ?¥ÊôØ‰∏äÁ? AudioUploaderÔºàÊ??≤‰?Ôº?

    IEmotionFeed feed;
    string _lastLoggedJson;

    public event Action<string> EmotionJsonReceived;

    async void Start()
    {
        _lastLoggedJson = null;
<<<<<<< HEAD
        // 1) ?æ‰º∫?çÂô® URLÔºàÁî®‰Ω†‰??çÂ???UDP ?¢Ê∏¨ÔºõÈÄôË£°Áµ¶Ê?Á∞°ÂñÆÊµÅÁ?Ôº?
=======

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

        // 1) Êâæ‰º∫ÊúçÂô® URLÔºàÁî®‰Ω†‰πãÂâçÂÅöÁöÑ UDP Êé¢Ê∏¨ÔºõÈÄôË£°Áµ¶ÊúÄÁ∞°ÂñÆÊµÅÁ®ãÔºâ
>>>>>>> 09a4a4177026960556b85e8f86fa2c437ce9e28f
        if (string.IsNullOrEmpty(serverBaseUrl))
        {
            // ?àË?Âø´Â?
            serverBaseUrl = PlayerPrefs.GetString("emo_server_url", "");
            if (string.IsNullOrEmpty(serverBaseUrl))
            {
                Debug.Log("[DISCOVERY] scanning...");
                var url = await ServerDiscovery.FindServerUrlAsync(); // ‰Ω†Â??¢Â???B-1
                if (!string.IsNullOrEmpty(url))
                {
                    serverBaseUrl = url; // e.g. http://192.168.0.50:8000
                    PlayerPrefs.SetString("emo_server_url", serverBaseUrl);
                    PlayerPrefs.Save();
                    Debug.Log("[DISCOVERY] found " + serverBaseUrl);
                }
                else
                {
                    // ?æ‰??∞Â∞±?®Êú¨Ê©üÔ??π‰æø??Editor Ê∏?
                    serverBaseUrl = "http://127.0.0.1:8000";
                    Debug.LogWarning("[DISCOVERY] not found, fallback " + serverBaseUrl);
                }
            }
            else Debug.Log("[DISCOVERY] use cached " + serverBaseUrl);
        }

        // 2) ??/audio URL ?áÁµ¶ AudioUploaderÔºà‚? ?çÈ?Ôº?
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

        // 3) ?üÂ??ÖÁ?Ë≥áÊ?‰æÜÊ?ÔºàHTTP Ëº™Ë©¢??WebSocket ?®Êí≠Ôº?
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

<<<<<<< HEAD
    // ?ØÈÅ∏ÔºöÊ?‰æõ‰??ãÈ??∞Ê??èÊñπÊ≥ïÔ??öÊ? UI ?âÈ?
=======
    // ÂèØÈÅ∏ÔºöÊèê‰æõ‰∏ÄÂÄãÈáçÊñ∞ÊéÉÊèèÊñπÊ≥ïÔºåÂÅöÊàê UI ÊåâÈàï
>>>>>>> 09a4a4177026960556b85e8f86fa2c437ce9e28f
    public void RescanServer()
    {
        PlayerPrefs.DeleteKey("emo_server_url");
        serverBaseUrl = "";
        Start(); // Á∞°ÂñÆÁ≤óÊö¥?ÑÈ??üÊ?Á®?
    }
}
