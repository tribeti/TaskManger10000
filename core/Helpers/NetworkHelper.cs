using System.Net.NetworkInformation;

namespace core.Helpers;

public class NetworkHelper : IDisposable
{
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastMeasuredAt;

    public NetworkHelper()
    {
        var interfaces = GetActiveInterfaces();
        _lastBytesReceived = GetTotalBytesReceived(interfaces);
        _lastBytesSent = GetTotalBytesSent(interfaces);
        _lastMeasuredAt = DateTime.UtcNow;
    }

    private static NetworkInterface[] GetActiveInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .ToArray();
    }

    private static long GetTotalBytesReceived(NetworkInterface[] interfaces)
    {
        return interfaces.Sum(nic => nic.GetIPStatistics().BytesReceived);
    }

    private static long GetTotalBytesSent(NetworkInterface[] interfaces)
    {
        return interfaces.Sum(nic => nic.GetIPStatistics().BytesSent);
    }

    public (double downloadKBps, double uploadKBps) NetworkSpeed()
    {
        var interfaces = GetActiveInterfaces();
        long currentBytesReceived = GetTotalBytesReceived(interfaces);
        long currentBytesSent = GetTotalBytesSent(interfaces);
        DateTime now = DateTime.UtcNow;

        double elapsedSeconds = (now - _lastMeasuredAt).TotalSeconds;

        double downloadKBps = 0;
        double uploadKBps = 0;

        if (elapsedSeconds > 0)
        {
            downloadKBps = Math.Round((currentBytesReceived - _lastBytesReceived) / 1024.0 / elapsedSeconds, 2);
            uploadKBps = Math.Round((currentBytesSent - _lastBytesSent) / 1024.0 / elapsedSeconds, 2);
        }

        _lastBytesReceived = currentBytesReceived;
        _lastBytesSent = currentBytesSent;
        _lastMeasuredAt = now;

        return (downloadKBps, uploadKBps);
    }

    public (long roundtripMs, int packetLossPct) GetPingAndPacketLoss()
    {
        using Ping p = new();
        var options = new PingOptions { DontFragment = true };
        byte[] buffer = new byte[32];
        int sent = 0, received = 0;
        long totalRtt = 0;

        for (int i = 0; i < 4; i++)
        {
            sent++;
            PingReply reply = p.Send("8.8.8.8", 1000, buffer, options);

            if (reply.Status == IPStatus.Success)
            {
                received++;
                totalRtt += reply.RoundtripTime;
            }
        }

        int packetLossPct = sent > 0 ? ((sent - received) * 100 / sent) : 100;
        long avgRtt = received > 0 ? totalRtt / received : 0;

        return (avgRtt, packetLossPct);
    }

    public void Dispose() { }
}