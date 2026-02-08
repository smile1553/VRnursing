using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class ServerDiscovery
{
    const int Port = 9450;
    const string Query = "EMO_SERVER?";
    const int TimeoutMs = 2000;

    public static async Task<string> FindServerUrlAsync()
    {
        using (var client = new UdpClient())
        {
            client.EnableBroadcast = true;
            client.Client.ReceiveTimeout = TimeoutMs;

            // 廣播出去
            var query = Encoding.UTF8.GetBytes(Query);
            var ep = new IPEndPoint(IPAddress.Broadcast, Port);
            await client.SendAsync(query, query.Length, ep);

            // 等回覆（EMO_SERVER:http://<ip>:8000）
            var t = client.ReceiveAsync();
            var completed = await Task.WhenAny(t, Task.Delay(TimeoutMs));
            if (completed == t)
            {
                var resp = Encoding.UTF8.GetString(t.Result.Buffer).Trim();
                if (resp.StartsWith("EMO_SERVER:"))
                {
                    var url = resp.Substring("EMO_SERVER:".Length);
                    return url.TrimEnd('/'); // e.g. http://192.168.0.50:8000
                }
            }
        }
        return null;
    }
}
