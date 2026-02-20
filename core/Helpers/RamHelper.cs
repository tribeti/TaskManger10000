using System.Management;
using System.Runtime.InteropServices;

namespace core.Helpers;

// Source - https://stackoverflow.com/a/105109
// Posted by Philip Rieck, modified by community. See post 'Timeline' for change history
// Retrieved 2026-02-20, License - CC BY-SA 4.0
public class RamHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX() => dwLength = (uint) Marshal.SizeOf(typeof(MEMORYSTATUSEX));
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    // gets total physical memory, available physical memory and percentage of used physical memory
    public static (double totalGB, double availGB, double usedPct) GetMemoryStatus()
    {
        var s = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(s))
            return (0, 0, 0);
        double total = s.ullTotalPhys / 1024.0 / 1024 / 1024;
        double avail = s.ullAvailPhys / 1024.0 / 1024 / 1024;
        double pct = s.dwMemoryLoad;
        return (total, avail, pct);
    }

    public static (int usedSlots, int totalSlots, int confSpeed) GetMemoryInfo()
    {
        int usedSlots = 0, totalSlots = 0, confSpeed = 0;

        // get used slots
        var query = "SELECT InterleavePosition FROM Win32_PhysicalMemory";
        using (var searcher = new ManagementObjectSearcher(query))
        {
            var results = searcher.Get();
            usedSlots += (from ManagementObject obj in results select obj).Count();
        }

        // get total slots
        var query2 = "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray";
        using (var searcher = new ManagementObjectSearcher(query2))
        {
            var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                totalSlots = Convert.ToInt32(obj["MemoryDevices"]);
            }
        }

        // get configured speed
        var query3 = "SELECT Speed FROM Win32_PhysicalMemory";
        using (var searcher = new ManagementObjectSearcher(query3))
        {
            var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                confSpeed = Convert.ToInt32(obj["Speed"]);
                break;
            }
        }
        return (usedSlots, totalSlots, confSpeed);
    }
}