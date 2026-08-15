using core.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace core.Monitor;

public class ProcessMonitor
{
    private volatile List<ProcessInfo> _cache = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Dictionary<int, (TimeSpan Cpu, TimeSpan Timestamp, DateTime StartTime)> _prev = [];
    private static readonly Dictionary<int, DateTime> _suspendedProcesses = [];

    [Flags]
    private enum ThreadAccess : int
    {
        SUSPEND_RESUME = 0x0002
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32.dll")]
    private static extern uint SuspendThread(IntPtr hThread);

    [DllImport("kernel32.dll")]
    private static extern int ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    public static bool IsSuspended(int pid, DateTime startTime) => _suspendedProcesses.TryGetValue(pid, out var suspendedStart) && suspendedStart == startTime;

    public static bool Suspend(int pid, DateTime startTime)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            bool success = false;

            foreach (ProcessThread thread in process.Threads)
            {
                IntPtr hThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint) thread.Id);
                if (hThread != IntPtr.Zero)
                {
                    try
                    {
                        if (SuspendThread(hThread) != unchecked((uint) -1))
                            success = true;
                    }
                    finally { CloseHandle(hThread); }
                }
            }

            if (success)
                _suspendedProcesses[pid] = startTime;
            return success;
        }
        catch { return false; }
    }

    public static bool Resume(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            bool success = false;

            foreach (ProcessThread thread in process.Threads)
            {
                IntPtr hThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint) thread.Id);
                if (hThread != IntPtr.Zero)
                {
                    try
                    {
                        int count;
                        do
                        { count = ResumeThread(hThread); } while (count > 0);
                        if (count != -1)
                            success = true;
                    }
                    finally { CloseHandle(hThread); }
                }
            }

            if (success)
                _suspendedProcesses.Remove(pid);
            return success;
        }
        catch { return false; }
    }

    public static void TogglePause(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (IsSuspended(pid, p.StartTime))
                Resume(pid);
            else
                Suspend(pid, p.StartTime);
        }
        catch { }
    }

    public void Refresh()
    {
        var now = _clock.Elapsed;
        var procs = Process.GetProcesses();
        var list = new List<ProcessInfo>(procs.Length);
        var current = new Dictionary<int, (TimeSpan, TimeSpan, DateTime)>();

        var activePids = new HashSet<int>(procs.Length);
        foreach (var p in procs)
            activePids.Add(p.Id);

        foreach (var key in _suspendedProcesses.Keys.ToList())
        {
            if (!activePids.Contains(key))
                _suspendedProcesses.Remove(key);
        }

        foreach (var p in procs)
        {
            try
            {
                var cpuTime = p.TotalProcessorTime;

                DateTime? startTime;
                try
                { startTime = p.StartTime; }
                catch { startTime = null; }

                double cpuPercent = 0;
                bool isSuspended = false;

                if (startTime is DateTime validStart)
                {
                    isSuspended = IsSuspended(p.Id, validStart);

                    if (_prev.TryGetValue(p.Id, out var old) && old.StartTime == validStart)
                    {
                        double elapsedMs = (now - old.Timestamp).TotalMilliseconds;
                        double usedMs = (cpuTime - old.Cpu).TotalMilliseconds;

                        if (elapsedMs > 0 && usedMs >= 0)
                        {
                            var raw = usedMs / (Environment.ProcessorCount * elapsedMs) * 100;
                            if (!double.IsNaN(raw) && !double.IsInfinity(raw) && raw >= 0)
                            {
                                cpuPercent = raw;
                            }
                        }
                    }

                    current[p.Id] = (cpuTime, now, validStart);
                }

                list.Add(new ProcessInfo(
                    Id: p.Id,
                    Name: p.ProcessName,
                    MemoryUsage: p.WorkingSet64 / 1_048_576.0,
                    CpuUsage: cpuPercent,
                    IsSuspended: isSuspended
                ));
            }
            catch { }
            finally { p.Dispose(); }
        }

        _prev = current;
        _cache = list;
    }

    public List<ProcessInfo> GetFiltered(string query, SortMode sort)
    {
        var snapshot = _cache;

        IEnumerable<ProcessInfo> result = string.IsNullOrEmpty(query)
            ? snapshot
            : snapshot.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        return sort switch
        {
            SortMode.NameAsc => result.OrderBy(p => p.Name).ToList(),
            SortMode.MemoryDesc => result.OrderByDescending(p => p.MemoryUsage).ToList(),
            SortMode.CpuDesc => result.OrderByDescending(p => p.CpuUsage).ToList(),
            _ => result.ToList()
        };
    }

    public static void Kill(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
        }
        catch { }
    }

    public static void KillAllByName(string name)
    {
        foreach (var p in Process.GetProcessesByName(name))
        {
            try
            { p.Kill(entireProcessTree: true); }
            catch { }
            finally { p.Dispose(); }
        }
    }
}