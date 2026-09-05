using System;

[Serializable]
public sealed class ServerDiscoveryResponse
{
    public string serverName;
    public string ip;
    public int port;
    public int protocolVersion;

    public string GetBaseUrl()
    {
        return $"http://{ip}:{port}";
    }
}
