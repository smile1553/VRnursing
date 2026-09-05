using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class RunAI_Network : MonoBehaviour
{
    const string LastServerUrlKey = "emo_server_url";
    const int AvailabilityTimeoutSeconds = 2;

    [Header("Auto Discovery")]
    [Tooltip("Leave empty for cached-server validation and UDP discovery. A value here is treated as a manual override candidate.")]
    public string serverBaseUrl = "";
    public float intervalSec = 1f;
    public bool autoUploadOnConnect = true;
    public bool useWebSocketFeed = true;
    public bool logIncomingJson = false;

    [Header("Components")]
    public RunAI runAi;
    public AudioUploader audioUploader;

    IEmotionFeed feed;
    ServerDiscovery _serverDiscovery;
    CancellationTokenSource _initializationCts;
    string _configuredServerBaseUrl;
    string _lastLoggedJson;
    int _initializationVersion;
    bool _shuttingDown;

    public event Action<string> EmotionJsonReceived;

    void Awake()
    {
        _configuredServerBaseUrl = NormalizeBaseUrl(serverBaseUrl);
        _serverDiscovery = new ServerDiscovery();
    }

    void Start()
    {
        _lastLoggedJson = null;
        ResolveComponents();
        StartInitialization();
    }

    void ResolveComponents()
    {
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
    }

    void StartInitialization()
    {
        if (_shuttingDown)
            return;

        CancelInitialization();
        var cts = new CancellationTokenSource();
        _initializationCts = cts;
        int version = ++_initializationVersion;
        _ = InitializeConnectionSafelyAsync(version, cts);
    }

    async Task InitializeConnectionSafelyAsync(int version, CancellationTokenSource cts)
    {
        try
        {
            await InitializeConnectionAsync(version, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected during rescan, scene unload, application quit, or destroy.
        }
        catch (Exception exception)
        {
            if (IsCurrentInitialization(version))
                Debug.LogError("[RunAI_Network] Server initialization failed: " + exception);
        }
        finally
        {
            if (ReferenceEquals(_initializationCts, cts))
                _initializationCts = null;
            cts.Dispose();
        }
    }

    async Task InitializeConnectionAsync(int version, CancellationToken cancellationToken)
    {
        string candidate = _configuredServerBaseUrl;
        bool candidateIsCached = string.IsNullOrEmpty(candidate);

        if (candidateIsCached)
            candidate = NormalizeBaseUrl(PlayerPrefs.GetString(LastServerUrlKey, ""));

        if (!string.IsNullOrEmpty(candidate))
        {
            Debug.Log(candidateIsCached
                ? "[DISCOVERY] checking cached server " + candidate
                : "[DISCOVERY] checking configured server " + candidate);

            if (await IsServerAvailableAsync(candidate, cancellationToken))
            {
                if (IsCurrentInitialization(version))
                    SetServerEndpointAndConnect(candidate);
                return;
            }

            if (candidateIsCached)
            {
                PlayerPrefs.DeleteKey(LastServerUrlKey);
                PlayerPrefs.Save();
                Debug.LogWarning("[DISCOVERY] cached server is unavailable; starting UDP discovery.");
            }
            else
            {
                Debug.LogWarning("[DISCOVERY] configured server is unavailable; starting UDP discovery.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        Debug.Log($"[DISCOVERY] broadcasting on UDP {ServerDiscovery.DiscoveryPort}...");

        ServerDiscoveryResponse response = await _serverDiscovery.DiscoverAsync(cancellationToken);
        if (response == null)
        {
            if (IsCurrentInitialization(version))
                Debug.LogWarning("[DISCOVERY] no compatible NursingVRServer was found on the LAN.");
            return;
        }

        string discoveredBaseUrl = response.GetBaseUrl();
        Debug.Log($"[DISCOVERY] found {response.serverName} at {discoveredBaseUrl}; checking availability.");

        if (!await IsServerAvailableAsync(discoveredBaseUrl, cancellationToken))
        {
            if (IsCurrentInitialization(version))
                Debug.LogWarning("[DISCOVERY] discovered server did not pass the existing /last availability check.");
            return;
        }

        if (IsCurrentInitialization(version))
            SetServerEndpointAndConnect(discoveredBaseUrl);
    }

    async Task<bool> IsServerAvailableAsync(string baseUrl, CancellationToken cancellationToken)
    {
        string normalized = NormalizeBaseUrl(baseUrl);
        if (string.IsNullOrEmpty(normalized))
            return false;

        using (var request = UnityWebRequest.Get(normalized + "/last"))
        {
            request.timeout = AvailabilityTimeoutSeconds;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            try
            {
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }

#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.Success;
#else
            return !request.isNetworkError && !request.isHttpError;
#endif
        }
    }

    void SetServerEndpointAndConnect(string baseUrl)
    {
        string normalized = NormalizeBaseUrl(baseUrl);
        if (string.IsNullOrEmpty(normalized))
        {
            Debug.LogError("[RunAI_Network] Refusing to use an invalid server Base URL.");
            return;
        }

        StopFormalConnections();
        serverBaseUrl = normalized;

        // A successful /last response makes this a known-good endpoint.
        PlayerPrefs.SetString(LastServerUrlKey, serverBaseUrl);
        PlayerPrefs.Save();
        Debug.Log("[DISCOVERY] using server " + serverBaseUrl);

        if (audioUploader != null)
        {
            audioUploader.serverUrl = serverBaseUrl + "/audio";
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

        feed = useWebSocketFeed ? (IEmotionFeed)new WsEmotionFeed() : new HttpEmotionFeed();
        if (!useWebSocketFeed && feed is HttpEmotionFeed http)
            http.intervalSec = intervalSec;

        feed.Start(this, serverBaseUrl, OnJson);
    }

    static string NormalizeBaseUrl(string value)
    {
        string normalized = (value ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(normalized) ||
            !Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return string.Empty;
        }

        return normalized;
    }

    bool IsCurrentInitialization(int version)
    {
        return !_shuttingDown && version == _initializationVersion;
    }

    void CancelInitialization()
    {
        var cts = _initializationCts;
        _initializationCts = null;
        if (cts != null && !cts.IsCancellationRequested)
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    void StopFormalConnections()
    {
        feed?.Stop();
        feed = null;
        audioUploader?.StopLoop();
    }

    void OnJson(string json)
    {
        if (logIncomingJson && !string.IsNullOrEmpty(json) && !string.Equals(json, _lastLoggedJson))
        {
            Debug.Log("[EmotionFeed] " + json);
            _lastLoggedJson = json;
        }

        if (runAi != null)
            runAi.ApplyJson(json);
        EmotionJsonReceived?.Invoke(json);
    }

    public void RescanServer()
    {
        if (_shuttingDown)
            return;

        ++_initializationVersion;
        CancelInitialization();
        StopFormalConnections();

        PlayerPrefs.DeleteKey(LastServerUrlKey);
        PlayerPrefs.Save();
        _configuredServerBaseUrl = string.Empty;
        serverBaseUrl = string.Empty;
        StartInitialization();
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        Shutdown();
    }

    void Shutdown()
    {
        if (_shuttingDown)
            return;

        _shuttingDown = true;
        ++_initializationVersion;
        CancelInitialization();
        _serverDiscovery?.Dispose();
        _serverDiscovery = null;
        StopFormalConnections();
    }
}
