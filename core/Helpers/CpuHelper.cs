using Microsoft.Win32;
using System.Diagnostics;

namespace core.Helpers;
// get cpu usage by each core
public class CpuHelper
{
    public static string GetProcessorCoreName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim()
                   ?? "Unknown CPU";
        }
        catch
        {
            return "Unknown CPU";
        }
    }

    public static double GetCpuUsage()
    {
        // Source - https://stackoverflow.com/a/51194100
        // Posted by L_J
        // Retrieved 2026-02-21, License - CC BY-SA 4.0

        var cpuUsage = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
        cpuUsage.NextValue();
        Thread.Sleep(500);
        return Math.Round(cpuUsage.NextValue(), 1);
    }

    public static float GetCpuUsageByCore(int coreIndex)
    {
        var category = new PerformanceCounterCategory("Processor Information");
        var instances = category.GetInstanceNames();
        var coreCounters = instances
            .Where(i => !i.Contains("_Total"))
            .Select(i => new PerformanceCounter("Processor Information", "% Processor Utility", i))
            .OrderBy(c => int.Parse(c.InstanceName.Split(',')[^1]))
            .ToList();
        coreCounters.ForEach(c => c.NextValue());
        if (coreIndex < 0 || coreIndex >= coreCounters.Count)
            throw new ArgumentOutOfRangeException(nameof(coreIndex), "Invalid core index.");
        var coreCounter = coreCounters[coreIndex];
        coreCounter.NextValue();
        Thread.Sleep(500);
        return coreCounter.NextValue();
    }
}
