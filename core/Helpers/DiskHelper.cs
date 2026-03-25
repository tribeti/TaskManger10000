using System.Diagnostics;

namespace core.Helpers;

public record DiskMetrics(double ReadMbps, double WriteMbps, double Iops, double LatencyMs);

public record DriveMetrics(string Name, string Format, double UsedGB, double TotalGB, double UsedPct);

public class DiskHelper : IDisposable
{
    private readonly PerformanceCounter _readCounter;
    private readonly PerformanceCounter _writeCounter;
    private readonly PerformanceCounter _iopsCounter;
    private readonly PerformanceCounter _latencyCounter;
    private bool _disposed;

    public DiskHelper()
    {
        _readCounter = new("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        _writeCounter = new("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
        _iopsCounter = new("PhysicalDisk", "Disk Transfers/sec", "_Total");
        _latencyCounter = new("PhysicalDisk", "Avg. Disk sec/Transfer", "_Total");

        // Warm up to get initial values
        _readCounter.NextValue();
        _writeCounter.NextValue();
        _iopsCounter.NextValue();
        _latencyCounter.NextValue();
    }

    public DiskMetrics GetDiskMetrics()
    {
        double readBytesPerSec = _readCounter.NextValue();
        double writeBytesPerSec = _writeCounter.NextValue();
        double currentIops = _iopsCounter.NextValue();
        double currentLatencyMs = _latencyCounter.NextValue() * 1000;

        double readMbPerSec = Math.Round(readBytesPerSec / (1000 * 1024), 2);
        double writeMbPerSec = Math.Round(writeBytesPerSec / (1000 * 1024), 2);

        return new DiskMetrics(readMbPerSec, writeMbPerSec, currentIops, currentLatencyMs);
    }

    public List<DriveMetrics> GetAllDrivesUsage()
    {
        DriveInfo[] drives = DriveInfo.GetDrives();
        var result = new List<DriveMetrics>();

        foreach (DriveInfo drive in drives)
        {
            if (!drive.IsReady)
                continue;

            double totalGB = drive.TotalSize / (1000.0 * 1024.0 * 1024.0);
            double freeGB = drive.TotalFreeSpace / (1000.0 * 1024.0 * 1024.0);
            double usedGB = totalGB - freeGB;
            double usedPct = totalGB > 0 ? (usedGB / totalGB) * 100 : 0;

            result.Add(new DriveMetrics(drive.Name, drive.DriveFormat, usedGB, totalGB, usedPct));
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _readCounter?.Dispose();
        _writeCounter?.Dispose();
        _iopsCounter?.Dispose();
        _latencyCounter?.Dispose();

        _disposed = true;
    }
}
