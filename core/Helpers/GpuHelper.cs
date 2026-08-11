using Microsoft.Win32;
using System.Diagnostics;

namespace core.Helpers;

// Source - https://stackoverflow.com/a/71481615
// Posted by Fidel, modified by community. See post 'Timeline' for change history
// Retrieved 2026-02-18, License - CC BY-SA 4.0

public class GpuHelper : IDisposable
{
    private readonly List<PerformanceCounter> _counters;
    private readonly List<PerformanceCounter> _vramCounters;
    private bool _disposed;

    public GpuHelper()
    {
        _counters = InitCounters();
        _vramCounters = InitVramCounters();
    }

    public static List<PerformanceCounter> InitCounters()
    {
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var counterNames = category.GetInstanceNames();

            var gpuCounters = counterNames
                                .Where(counterName => counterName.Contains("luid_") && counterName.Contains("engtype_3D"))
                                .SelectMany(counterName => category.GetCounters(counterName))
                                .Where(counter => counter.CounterName.Equals("Utilization Percentage"))
                                .ToList();

            return gpuCounters;
        }
        catch
        {
            return [];
        }
    }

    public static List<PerformanceCounter> InitVramCounters()
    {
        try
        {
            var category = new PerformanceCounterCategory("GPU Adapter Memory");
            var counterNames = category.GetInstanceNames();

            var vramCounters = counterNames
                                .Where(counterName => counterName.Contains("luid_"))
                                .SelectMany(counterName => category.GetCounters(counterName))
                                .Where(counter => counter.CounterName.Equals("Dedicated Usage"))
                                .ToList();

            return vramCounters;
        }
        catch
        {
            return [];
        }
    }

    public void WarmUp()
    {
        _counters.ForEach(c => { try { c.NextValue(); } catch { } });
        _vramCounters.ForEach(c => { try { c.NextValue(); } catch { } });
    }

    public double GetGPUUsage()
    {
        if (_counters.Count == 0)
            return 0;

        bool hasError = false;
        double total = 0;

        foreach (var c in _counters)
        {
            try
            { total += c.NextValue(); }
            catch (InvalidOperationException) { hasError = true; }
        }

        if (hasError)
            ReinitCounters();

        return Math.Round(total, 0);
    }

    private void ReinitCounters()
    {
        _counters.ForEach(c => c.Dispose());
        _counters.Clear();
        _counters.AddRange(InitCounters());
        _counters.ForEach(c => { try { c.NextValue(); } catch { } });
    }

    public double GetVramUsedMB()
    {
        if (_vramCounters.Count == 0)
            return 0;

        bool hasError = false;
        double totalBytes = 0;

        foreach (var c in _vramCounters)
        {
            try
            { totalBytes += c.NextValue(); }
            catch (InvalidOperationException) { hasError = true; }
        }

        if (hasError)
            ReinitVramCounters();

        return Math.Round(totalBytes / 1024.0 / 1024.0, 0);
    }

    private void ReinitVramCounters()
    {
        _vramCounters.ForEach(c => c.Dispose());
        _vramCounters.Clear();
        _vramCounters.AddRange(InitVramCounters());
        _vramCounters.ForEach(c => { try { c.NextValue(); } catch { } });
    }

    // not tested on multi-GPU systems, but should return the first GPU's info (maybe igpu idk)
    public static (string gpuName, string driverVer, double totalVramMB) GetGPUInfo()
    {
        string gpuName = "Unknown GPU";
        string driverVer = "Unknown Driver Version";
        double totalVramMB = 0;
        const string basePath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        using var baseKey = Registry.LocalMachine.OpenSubKey(basePath);
        if (baseKey is null)
            return (gpuName, driverVer, totalVramMB);

        foreach (var subKeyName in baseKey.GetSubKeyNames())
        {
            if (!int.TryParse(subKeyName, out _))
                continue;

            using var subKey = baseKey.OpenSubKey(subKeyName);
            if (subKey is null)
                continue;

            var name = subKey.GetValue("DriverDesc")?.ToString();
            var driver = subKey.GetValue("DriverVersion")?.ToString();

            if (!string.IsNullOrEmpty(name))
            {
                gpuName = name;
                driverVer = driver ?? "Unknown Driver Version";
                totalVramMB = subKey.GetValue("HardwareInformation.qwMemorySize") switch
                {
                    byte[] { Length: >= 8 } raw => BitConverter.ToUInt64(raw, 0) / 1024.0 / 1024.0,
                    long qword => qword / 1024.0 / 1024.0,
                    int dword => dword / 1024.0 / 1024.0,
                    _ => 0
                };

                break;
            }
        }
        return (gpuName, driverVer, totalVramMB);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _counters.ForEach(c => c.Dispose());
        _counters.Clear();
        _vramCounters.ForEach(c => c.Dispose());
        _vramCounters.Clear();
        _disposed = true;
    }
}