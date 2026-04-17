using Microsoft.Win32;
using System.Diagnostics;

namespace core.Helpers;

// Source - https://stackoverflow.com/a/71481615
// Posted by Fidel, modified by community. See post 'Timeline' for change history
// Retrieved 2026-02-18, License - CC BY-SA 4.0

public class GpuHelper : IDisposable
{
    private readonly List<PerformanceCounter> _counters;
    private bool _disposed;

    public GpuHelper()
    {
        _counters = InitCounters();
    }

    public static List<PerformanceCounter> InitCounters()
    {
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var counterNames = category.GetInstanceNames();

            var gpuCounters = counterNames
                                .Where(counterName => counterName.Contains("luid_"))
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

    public void WarmUp() => _counters.ForEach(c => { try { c.NextValue(); } catch { } });

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

    // not tested on multi-GPU systems, but should return the first GPU's info (maybe igpu idk)
    public static (string gpuName, string driverVer) GetGPUInfo()
    {
        string gpuName = "Unknown GPU";
        string driverVer = "Unknown Driver Version";
        const string basePath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        using var baseKey = Registry.LocalMachine.OpenSubKey(basePath);
        if (baseKey is null)
            return (gpuName, driverVer);

        foreach (var subKeyName in baseKey.GetSubKeyNames())
        {
            if (!int.TryParse(subKeyName, out _))
                continue;

            using var subKey = baseKey.OpenSubKey(subKeyName);
            if (subKey is null)
                continue;

            var name = subKey.GetValue("DriverDesc")?.ToString();
            var driver = subKey.GetValue("DriverVersion")?.ToString();
            driver = driver?.Replace(".", "")[^5..].Insert(3, ".");

            if (!string.IsNullOrEmpty(name))
            {
                gpuName = name;
                driverVer = driver ?? "Unknown Driver Version";
                break;
            }
        }
        return (gpuName, driverVer);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _counters.ForEach(c => c.Dispose());
        _counters.Clear();
        _disposed = true;
    }
}