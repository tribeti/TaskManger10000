using core.Models;
using System.Diagnostics;

namespace core.Monitor;

public class ProcessMonitor
{
    private volatile List<ProcessInfo> _cache = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Dictionary<int, (TimeSpan Cpu, TimeSpan Timestamp, DateTime StartTime)> _prev = [];

    public void Refresh()
    {
        var now = _clock.Elapsed;
        var procs = Process.GetProcesses();
        var list = new List<ProcessInfo>(procs.Length);
        var current = new Dictionary<int, (TimeSpan, TimeSpan, DateTime)>();

        foreach (var p in procs)
        {
            try
            {
                var cpuTime = p.TotalProcessorTime;

                DateTime startTime;
                try
                { startTime = p.StartTime; }
                catch { startTime = DateTime.MinValue; }

                double cpuPercent = 0;

                if (_prev.TryGetValue(p.Id, out var old) && old.StartTime == startTime)
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

                current[p.Id] = (cpuTime, now, startTime);

                list.Add(new ProcessInfo(
                    Id: p.Id,
                    Name: p.ProcessName,
                    MemoryUsage: p.WorkingSet64 / 1_048_576.0,
                    CpuUsage: cpuPercent
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