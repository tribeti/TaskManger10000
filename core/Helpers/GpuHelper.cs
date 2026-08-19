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

    public void WarmUp()
    {
        _counters.ForEach(c => { try { c.NextValue(); } catch { } });
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

    // not tested on multi-GPU systems, but should return the first GPU's info (maybe igpu idk)
    public static (string gpuName, string driverVer, double totalVramMB) GetGPUInfo()
    {
        const string unknownName = "Unknown GPU";
        const string unknownDriver = "Unknown Driver Version";
        const string basePath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        using var baseKey = Registry.LocalMachine.OpenSubKey(basePath);
        if (baseKey is null)
            return (unknownName, unknownDriver, 0);

        foreach (var subKeyName in baseKey.GetSubKeyNames())
        {
            if (!int.TryParse(subKeyName, out _))
                continue;

            using var subKey = baseKey.OpenSubKey(subKeyName);
            var name = subKey?.GetValue("DriverDesc")?.ToString();
            if (string.IsNullOrEmpty(name))
                continue;

            var driverVer = subKey!.GetValue("DriverVersion")?.ToString() ?? unknownDriver;
            var totalVramMB = GetVideoMem(subKey);

            return (name, driverVer, totalVramMB);
        }

        return (unknownName, unknownDriver, 0);
    }

    // Source - https://stackoverflow.com/a/75205056
    // Posted by colin lamarre
    // Retrieved 2026-08-15, License - CC BY-SA 4.0
    private static double GetVideoMem(RegistryKey subKey)
    {
        try
        {
            object? vram = subKey.GetValue("HardwareInformation.qwMemorySize");
            if (vram is not null)
                // byte to MB
                return (double) (long) vram / 1024.0 / 1024.0;
        }
        catch { }
        return 0;
    }

    // Source - https://stackoverflow.com/a/79422972
    // Posted by BrainSlugs83, modified by community. See post 'Timeline' for change history
    // Retrieved 2026-08-15, License - CC BY-SA 4.0
    private static readonly Lazy<List<Func<long>>> TotalVramUsageCounters = new
    (
        () =>
        {
            try
            {
                var cat = new PerformanceCounterCategory("GPU Adapter Memory");
                return cat.GetInstanceNames().SelectMany(cat.GetCounters)
                        .Where(static c => c?.CounterName?.EndsWith("Usage") == true)
                        .Select(static c => new Func<long>(() => c.NextSample().RawValue))
                        .ToList();
            }
            catch
            {
                return [];
            }
        },
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    public static double GetTotalVRamUsage() => TotalVramUsageCounters.Value.Select(x => x()).Sum() / 1024.0 / 1024.0;

    public void Dispose()
    {
        if (_disposed)
            return;
        _counters.ForEach(c => c.Dispose());
        _counters.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}