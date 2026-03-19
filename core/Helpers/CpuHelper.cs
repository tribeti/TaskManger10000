using Microsoft.Win32;
using System.Diagnostics;

namespace core.Helpers;
// get cpu usage by each core
public class CpuHelper : IDisposable
{
    private readonly PerformanceCounter _totalCounter;
    private readonly List<PerformanceCounter> _coreCounters;
    private bool _disposed;

    public CpuHelper()
    {
        _totalCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
        var category = new PerformanceCounterCategory("Processor Information");
        _coreCounters = [];
        var instances = category.GetInstanceNames()
            .Where(i => !i.Contains("_Total"))
            .OrderBy(i => int.Parse(i.Split(',')[^1]));
        foreach (var instance in instances)
        {
            _coreCounters.Add(new PerformanceCounter("Processor Information", "% Processor Utility", instance));
        }
    }

    public void WarmUp()
    {
        _totalCounter.NextValue();
        _coreCounters.ForEach(c => c.NextValue());
    }

    public double GetUsage() => Math.Round(_totalCounter.NextValue(), 1);

    public IReadOnlyList<float> GetPerCoreUsage() => _coreCounters.Select(c => c.NextValue()).ToList();


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

    public static string GetOSName()
    {
        string productName = "";
        string displayVersion = "";

        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            productName = key.GetValue("ProductName")?.ToString();
            displayVersion = key.GetValue("DisplayVersion")?.ToString();
        }

        return productName + " " + displayVersion;
    }

    // Source - https://stackoverflow.com/a/66459322
    // Posted by Steven Rands
    // Retrieved 2026-02-28, License - CC BY-SA 4.0
    public static TimeSpan GetUptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public void Dispose()
    {
        if (_disposed)
            return;
        _totalCounter.Dispose();
        _coreCounters.ForEach(c => c.Dispose());
        _coreCounters.Clear();
        _disposed = true;
    }
}
