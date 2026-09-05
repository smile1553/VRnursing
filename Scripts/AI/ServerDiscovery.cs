using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Finds a compatible Nursing VR server on the local network.
/// This service only performs discovery; HTTP and WebSocket traffic remain owned
/// by RunAI_Network and the existing feed/uploader classes.
/// </summary>
public sealed class ServerDiscovery : IDisposable
{
    public const int DiscoveryPort = 25566;
    public const int SupportedProtocolVersion = 1;
    public const string DiscoveryRequest = "DISCOVER_MY_SERVER";
    public const string ExpectedServerName = "NursingVRServer";

    const int DefaultTimeoutMs = 1800;
    const int DefaultMaxAttempts = 2;
    const int DefaultRetryDelayMs = 500;
    const int MaxResponseBytes = 4096;

    readonly object _sync = new object();
    readonly HashSet<UdpClient> _activeClients = new HashSet<UdpClient>();
    bool _disposed;

    public async Task<ServerDiscoveryResponse> DiscoverAsync(
        CancellationToken cancellationToken,
        int timeoutMs = DefaultTimeoutMs,
        int maxAttempts = DefaultMaxAttempts,
        int retryDelayMs = DefaultRetryDelayMs)
    {
        if (timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        if (retryDelayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(retryDelayMs));

        ThrowIfDisposed();

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await DiscoverOnceAsync(timeoutMs, cancellationToken);
            if (response != null)
                return response;

            if (attempt < maxAttempts && retryDelayMs > 0)
                await Task.Delay(retryDelayMs, cancellationToken);
        }

        return null;
    }

    async Task<ServerDiscoveryResponse> DiscoverOnceAsync(
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        UdpClient client = null;
        CancellationTokenRegistration cancellationRegistration = default;

        try
        {
            client = new UdpClient(AddressFamily.InterNetwork);
            client.EnableBroadcast = true;
            RegisterClient(client);

            // UdpClient.ReceiveAsync has no CancellationToken overload in the
            // Unity 2022 API profile. Closing the socket safely interrupts it.
            cancellationRegistration = cancellationToken.Register(CloseClient, client);

            byte[] request = Encoding.UTF8.GetBytes(DiscoveryRequest);
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            await client.SendAsync(request, request.Length, broadcastEndpoint);

            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int remainingMs = Math.Max(1, timeoutMs - (int)timer.ElapsedMilliseconds);
                Task<UdpReceiveResult> receiveTask = client.ReceiveAsync();
                Task timeoutTask = Task.Delay(remainingMs, cancellationToken);
                Task completed = await Task.WhenAny(receiveTask, timeoutTask);

                if (completed != receiveTask)
                {
                    ObserveFailure(receiveTask);
                    cancellationToken.ThrowIfCancellationRequested();
                    return null;
                }

                UdpReceiveResult packet = await receiveTask;
                ServerDiscoveryResponse response = ParseAndValidate(packet);
                if (response != null)
                    return response;
            }
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested || IsDisposed)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested || IsDisposed)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (SocketException)
        {
            // No active network interface or broadcast route. Treat this attempt
            // as unavailable so the bounded retry policy can continue.
            return null;
        }
        finally
        {
            cancellationRegistration.Dispose();
            if (client != null)
            {
                UnregisterClient(client);
                CloseClient(client);
            }
        }

        return null;
    }

    static ServerDiscoveryResponse ParseAndValidate(UdpReceiveResult packet)
    {
        if (packet.Buffer == null || packet.Buffer.Length == 0 || packet.Buffer.Length > MaxResponseBytes)
            return null;

        ServerDiscoveryResponse response;
        try
        {
            string json = Encoding.UTF8.GetString(packet.Buffer).Trim();
            response = JsonUtility.FromJson<ServerDiscoveryResponse>(json);
        }
        catch (Exception)
        {
            return null;
        }

        if (response == null ||
            !string.Equals(response.serverName, ExpectedServerName, StringComparison.Ordinal) ||
            response.protocolVersion != SupportedProtocolVersion ||
            response.port < IPEndPoint.MinPort ||
            response.port > IPEndPoint.MaxPort ||
            !IPAddress.TryParse(response.ip, out IPAddress advertisedAddress) ||
            advertisedAddress.AddressFamily != AddressFamily.InterNetwork ||
            IPAddress.Any.Equals(advertisedAddress) ||
            IPAddress.None.Equals(advertisedAddress) ||
            IPAddress.Broadcast.Equals(advertisedAddress) ||
            IPAddress.IsLoopback(advertisedAddress) ||
            !advertisedAddress.Equals(packet.RemoteEndPoint.Address))
        {
            return null;
        }

        return response;
    }

    void RegisterClient(UdpClient client)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                CloseClient(client);
                throw new ObjectDisposedException(nameof(ServerDiscovery));
            }

            _activeClients.Add(client);
        }
    }

    void UnregisterClient(UdpClient client)
    {
        lock (_sync)
            _activeClients.Remove(client);
    }

    static void CloseClient(object state)
    {
        if (state is UdpClient client)
        {
            try { client.Close(); }
            catch (ObjectDisposedException) { }
        }
    }

    static void ObserveFailure(Task task)
    {
        task.ContinueWith(
            completed => { _ = completed.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    bool IsDisposed
    {
        get
        {
            lock (_sync)
                return _disposed;
        }
    }

    void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServerDiscovery));
    }

    public void Dispose()
    {
        UdpClient[] clients;
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            clients = new UdpClient[_activeClients.Count];
            _activeClients.CopyTo(clients);
            _activeClients.Clear();
        }

        foreach (UdpClient client in clients)
            CloseClient(client);
    }
}
