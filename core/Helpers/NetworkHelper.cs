using System.Net.NetworkInformation;

namespace core.Helpers;

public class NetworkHelper : IDisposable
{
    private bool _disposed;

    private NetworkInterface[] _interfaces;
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastMeasuredAt;

    public NetworkHelper()
    {
        _interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .ToArray();

        _lastBytesReceived = GetTotalBytesReceived(_interfaces);
        _lastBytesSent = GetTotalBytesSent(_interfaces);
        _lastMeasuredAt = DateTime.UtcNow;
    }

    static long GetTotalBytesReceived(NetworkInterface[] interfaces)
    {
        return interfaces.Sum(nic => nic.GetIPStatistics().BytesReceived);
    }

    static long GetTotalBytesSent(NetworkInterface[] interfaces)
    {
        return interfaces.Sum(nic => nic.GetIPStatistics().BytesSent);
    }

    public (double downloadKBps, double uploadKBps) NetworkSpeed()
    {
        long currentBytesReceived = GetTotalBytesReceived(_interfaces);
        long currentBytesSent = GetTotalBytesSent(_interfaces);
        DateTime now = DateTime.UtcNow;

        double elapsedSeconds = (now - _lastMeasuredAt).TotalSeconds;

        double downloadKBps = 0;
        double uploadKBps = 0;

        if (elapsedSeconds > 0)
        {

            downloadKBps = Math.Round((currentBytesReceived - _lastBytesReceived) / 1000.0 / elapsedSeconds, 2);
            uploadKBps = Math.Round((currentBytesSent - _lastBytesSent) / 1000.0 / elapsedSeconds, 2);
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

        for (int i = 0; i < 4; i++)
        {
            sent++;
            PingReply reply = p.Send("8.8.8.8", 1000, buffer, options);
            if (reply.Status == IPStatus.Success)
                received++;
        }

        int lost = sent - received;
        long rtt = p.Send("8.8.8.8").RoundtripTime;
        return (rtt, lost * 100 / sent);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
    }
}