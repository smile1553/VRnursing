using UnityEngine;   // ← 要這個才認得 MonoBehaviour
using System;        // ← 要這個才認得 Action<>

public interface IEmotionFeed
{
    void Start(MonoBehaviour host, string serverUrl, Action<string> onJson);
    void Stop();
}
