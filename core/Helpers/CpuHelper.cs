using Microsoft.Win32;
using System.Diagnostics;

namespace core.Helpers;

public class CpuHelper : IDisposable
{
    private readonly PerformanceCounter _totalCounter;
    private bool _disposed;

    public CpuHelper()
    {
        _totalCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
    }

    public void WarmUp() => _totalCounter.NextValue();

    public double GetUsage() => Math.Round(_totalCounter.NextValue(), 0);

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

    public static string GetOSName()
    {
        string? productName = "";
        string? displayVersion = "";

        using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
        {
            productName = key?.GetValue("ProductName")?.ToString();
            displayVersion = key?.GetValue("DisplayVersion")?.ToString();
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
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
