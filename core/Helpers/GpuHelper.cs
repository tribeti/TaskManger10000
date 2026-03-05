using Microsoft.Win32;
using System.Diagnostics;

namespace core.Helpers;

// Source - https://stackoverflow.com/a/71481615
// Posted by Fidel, modified by community. See post 'Timeline' for change history
// Retrieved 2026-02-18, License - CC BY-SA 4.0

public class GpuHelper
{
    public static List<PerformanceCounter> GetGPUCounters()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var counterNames = category.GetInstanceNames();

        var gpuCounters = counterNames
                            .Where(counterName => counterName.EndsWith("engtype_3D"))
                            .SelectMany(counterName => category.GetCounters(counterName))
                            .Where(counter => counter.CounterName.Equals("Utilization Percentage"))
                            .ToList();

        return gpuCounters;
    }

    public static double GetGPUUsage(List<PerformanceCounter> gpuCounters)
    {
        gpuCounters.ForEach(x => x.NextValue());
        Thread.Sleep(1000);
        return Math.Round(gpuCounters.Sum(x => x.NextValue()), 1);
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
}