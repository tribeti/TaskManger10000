using System.Net.NetworkInformation;

namespace core.Helpers;

public class NetworkHelper : IDisposable
{
    private bool _disposed;

    public void GetNetworkUsage()
    {
        NetworkInterface? activeCard = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                 n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 n.GetIPv4Statistics().BytesReceived > 0);

        if (activeCard is null)
        {
            Console.WriteLine("N/A");
            return;
        }


        long maxBandwidthBps = activeCard.Speed;
        long prevBytesReceived = activeCard.GetIPv4Statistics().BytesReceived;
        long prevBytesSent = activeCard.GetIPv4Statistics().BytesSent;

        using Ping pingSender = new Ping();
        string hostToPing = "8.8.8.8";
        int timeout = 1000;

        // VÒNG LẶP VÔ HẠN - Cập nhật mỗi giây
        while (true)
        {
            Console.WriteLine($"Card mạng: {activeCard.Description}");
            Console.WriteLine($"Băng thông tối đa: {maxBandwidthBps / 1000000.0:F2} Mbps\n");

            // ---------------------------------------------------------
            // 1. TỐC ĐỘ (SEND/RECEIVE) & % BĂNG THÔNG
            // ---------------------------------------------------------
            long currentBytesReceived = activeCard.GetIPv4Statistics().BytesReceived;
            long currentBytesSent = activeCard.GetIPv4Statistics().BytesSent;

            long downloadBytesPerSec = currentBytesReceived - prevBytesReceived;
            long uploadBytesPerSec = currentBytesSent - prevBytesSent;

            prevBytesReceived = currentBytesReceived;
            prevBytesSent = currentBytesSent;

            // Tính % Băng thông (Công thức: Tổng bit / Tốc độ card * 100)
            double bandwidthUtilization = 0;
            if (maxBandwidthBps > 0)
            {
                bandwidthUtilization = ((downloadBytesPerSec + uploadBytesPerSec) * 8.0 / maxBandwidthBps) * 100;
            }

            Console.WriteLine("[1] TỐC ĐỘ TRUYỀN TẢI & BĂNG THÔNG");
            Console.WriteLine($"    Tải xuống (Receive):  {downloadBytesPerSec / 1024.0:F2} KB/s");
            Console.WriteLine($"    Tải lên (Send):       {uploadBytesPerSec / 1024.0:F2} KB/s");
            Console.WriteLine($"    Sử dụng Băng thông:   {bandwidthUtilization:F4} %");

            // ---------------------------------------------------------
            // 2. ĐỘ TRỄ (PING) & PACKET LOSS
            // ---------------------------------------------------------
            long latency = 0;
            int packetLoss = 100;

            try
            {
                // Ping 1 gói tin mỗi giây để kiểm tra đường truyền
                PingReply reply = pingSender.Send(hostToPing, timeout);
                if (reply.Status == IPStatus.Success)
                {
                    latency = reply.RoundtripTime;
                    packetLoss = 0; // Thành công nghĩa là không mất gói tin
                }
            }
            catch { }

            Console.WriteLine($"\n[2] CHẤT LƯỢNG ĐƯỜNG TRUYỀN (Ping tới {hostToPing})");
            Console.WriteLine($"    Độ trễ (Ping):        {latency} ms");
            Console.WriteLine($"    Mất gói tin (Loss):   {packetLoss} %");

            // ---------------------------------------------------------
            // 3. KẾT NỐI TCP ĐANG HOẠT ĐỘNG
            // ---------------------------------------------------------
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            int activeConnections = properties.GetActiveTcpConnections()
                                              .Count(c => c.State == TcpState.Established);

            Console.WriteLine($"    Kết nối đang mở:      {activeConnections} connections");
            Thread.Sleep(1000);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
    }
}
