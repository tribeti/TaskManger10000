using System.Diagnostics;

namespace core.Helpers;

public class DiskHelper : IDisposable
{
    private bool _disposed;

    public void GetDiskUsage()
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();

        foreach (DriveInfo d in allDrives)
        {
            Console.WriteLine("Drive {0}", d.Name);
            Console.WriteLine("Drive type: {0}", d.DriveType);
            if (d.IsReady)
            {
                Console.WriteLine("File system: {0}", d.DriveFormat);
                Console.WriteLine("Total available space: {0} GB", d.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0);
                Console.WriteLine("Total size of drive: {0} GB", d.TotalSize / 1024.0 / 1024.0 / 1024.0);
            }
        }

    }

    public void GetDiskSpec()
    {
        PerformanceCounter readCounter = new("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        PerformanceCounter writeCounter = new("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
        PerformanceCounter iopsCounter = new("PhysicalDisk", "Disk Transfers/sec", "_Total");
        PerformanceCounter latencyCounter = new("PhysicalDisk", "Avg. Disk sec/Transfer", "_Total");

        readCounter.NextValue();
        writeCounter.NextValue();
        iopsCounter.NextValue();
        latencyCounter.NextValue();

        while (true)
        {
            float readBytesPerSec = readCounter.NextValue();
            float writeBytesPerSec = writeCounter.NextValue();
            float currentIops = iopsCounter.NextValue();
            float currentLatencyMs = latencyCounter.NextValue() * 1000;

            float readMbPerSec = readBytesPerSec / (1024 * 1024);
            float writeMbPerSec = writeBytesPerSec / (1024 * 1024);
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
