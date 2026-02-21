using System.Diagnostics;
using System.Management;

namespace core.Helpers;
// get cpu usage by each core
public class CpuHelper
{
    public static string GetProcessorCoreName()
    {
        string ComputerName = "localhost";
        string? cpuName = String.Empty;
        ManagementScope Scope = new(String.Format("\\\\{0}\\root\\CIMV2", ComputerName), null);
        Scope.Connect();
        ObjectQuery Query = new("SELECT Name FROM Win32_Processor");
        ManagementObjectSearcher Searcher = new(Scope, Query);
        foreach (ManagementObject WmiObject in Searcher.Get())
        {
            cpuName = WmiObject["Name"].ToString();
        }
        return cpuName ?? "Unknown CPU";
    }

    public static float GetCpuUsage()
    {
        // Source - https://stackoverflow.com/a/51194100
        // Posted by L_J
        // Retrieved 2026-02-21, License - CC BY-SA 4.0

        var cpuUsage = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
        cpuUsage.NextValue();
        Thread.Sleep(500);
        return Math.Min(cpuUsage.NextValue(), 100f);
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
