using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class WsEmotionFeed : IEmotionFeed
{
    MonoBehaviour _host;
    Action<string> _onJson;
    CancellationTokenSource _cts;
    Task _worker;
    SynchronizationContext _unityContext;

    public void Start(MonoBehaviour host, string serverUrl, Action<string> onJson)
    {
        Stop();

        _host = host;
        _onJson = onJson;
        _unityContext = SynchronizationContext.Current;

        if (_host == null)
        {
            Debug.LogError("[WsEmotionFeed] host is null, cannot start");
            return;
        }

        var wsUrl = BuildWsUrl(serverUrl);
        _cts = new CancellationTokenSource();
        var localCts = _cts;
        _worker = RunAsync(wsUrl, localCts.Token);
        Debug.Log("[WsEmotionFeed] connect " + wsUrl);
        _worker.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var ex = t.Exception?.GetBaseException();
                Debug.LogError("[WsEmotionFeed] worker faulted: " + ex);
            }
            localCts.Dispose();
        }, TaskScheduler.Default);
    }

    public void Stop()
    {
        var cts = _cts;
        _cts = null;
        if (cts != null && !cts.IsCancellationRequested)
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }
        _onJson = null;
        _host = null;
        _worker = null;
    }

    static string BuildWsUrl(string serverUrl)
    {
        if (string.IsNullOrEmpty(serverUrl)) return "ws://127.0.0.1/ws";

        var trimmed = serverUrl.TrimEnd('/');

        if (trimmed.EndsWith("/ws", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed + "/ws";
        }

        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "wss://" + trimmed.Substring("https://".Length) + "/ws";
        }

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return "ws://" + trimmed.Substring("http://".Length) + "/ws";
        }

        return "ws://" + trimmed + "/ws";
    }

    async Task RunAsync(string wsUrl, CancellationToken token)
    {
        try
        {
            using (var socket = new ClientWebSocket())
            {
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

                await socket.ConnectAsync(new Uri(wsUrl), token).ConfigureAwait(false);
                Debug.Log("[WsEmotionFeed] connected " + wsUrl);

                var buffer = new byte[4096];

                while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var builder = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None).ConfigureAwait(false);
                            Debug.Log("[WsEmotionFeed] server closed connection");
                            return;
                        }

                        if (result.Count > 0)
                            builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    } while (!result.EndOfMessage && !token.IsCancellationRequested);

                    if (token.IsCancellationRequested) break;

                    if (builder.Length > 0)
                    {
                        var payload = builder.ToString();
                        var handler = _onJson;
                        if (handler != null)
                        {
                            if (_unityContext != null)
                            {
                                _unityContext.Post(_ =>
                                {
                                    try { handler(payload); }
                                    catch (Exception ex) { Debug.LogError("[WsEmotionFeed] callback error: " + ex); }
                                }, null);
                            }
                            else
                            {
                                try { handler(payload); }
                                catch (Exception ex) { Debug.LogError("[WsEmotionFeed] callback error: " + ex); }
                            }
                        }
                    }
                }

                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
        catch (Exception ex)
        {
            Debug.LogError("[WsEmotionFeed] exception: " + ex);
        }
    }
}
