using UnityEngine;
using UnityEngine.Networking;
using System;               // ← Action<>
using System.Collections;   // ← IEnumerator / Coroutine

public class HttpEmotionFeed : IEmotionFeed
{
    MonoBehaviour _host;
    string _url;
    bool _running;
    public float intervalSec = 1f;
    public bool logResponses = false;

    public void Start(MonoBehaviour host, string serverUrl, Action<string> onJson)
    {
        _host = host;
        _url = serverBase(serverUrl) + "/last";
        _running = true;
        _host.StartCoroutine(Poll(onJson));
    }

    static string serverBase(string s) => s.TrimEnd('/');

    IEnumerator Poll(Action<string> onJson)
    {
        while (_running)
        {
            using (var req = UnityWebRequest.Get(_url))
            {
                req.timeout = 2;
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result == UnityWebRequest.Result.Success)
#else
                if (!req.isNetworkError && !req.isHttpError)
#endif
                {
                    if (logResponses)
                        RuntimeLog.Info("[HTTP] Response: " + req.downloadHandler.text);
                    onJson?.Invoke(req.downloadHandler.text);
                }
                else
                {
                    // ✅ 印出錯誤訊息
                    Debug.LogError("[HTTP] Error: " + req.error);
                }
            }
            yield return new WaitForSeconds(intervalSec);
        }
    }

    public void Stop() => _running = false;
}
