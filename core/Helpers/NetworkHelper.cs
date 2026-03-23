using System.Net.NetworkInformation;

namespace core.Helpers;

public class NetworkHelper : IDisposable
{
    private bool _disposed;

    static long GetTotalBytesReceived(NetworkInterface[] interfaces)
    {
        return interfaces.Sum(nic => nic.GetIPStatistics().BytesReceived);
    }

    static long GetTotalBytesSent(NetworkInterface[] interfaces)
    {
        return interfaces.Sum(nic => nic.GetIPStatistics().BytesSent);
    }

    public (double, double) NetworkSpeed()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .ToArray();

        long lastBytesReceived = GetTotalBytesReceived(interfaces);
        long lastBytesSent = GetTotalBytesSent(interfaces);
        long currentBytesReceived = GetTotalBytesReceived(interfaces);
        long currentBytesSent = GetTotalBytesSent(interfaces);

        long receiveSpeed = currentBytesReceived - lastBytesReceived;
        long sendSpeed = currentBytesSent - lastBytesSent;
        return (receiveSpeed / 1024.0, sendSpeed / 1024.0);
    }

    public (long, int) GetPingAndPacketLoss()
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
            {
                received++;
            }
        }
        int lost = sent - received;
        return (p.Send("www.google.com").RoundtripTime, (lost * 100 / sent));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
    }
}
