using core.Models;
using System.Diagnostics;

namespace core.Monitor;

public class ProcessMonitor
{
    private volatile List<ProcessInfo> _cache = [];
    private Dictionary<int, (TimeSpan cpu, DateTime time)> _prev = [];

    public void Refresh()
    {
        var now = DateTime.UtcNow;
        var procs = Process.GetProcesses();
        var list = new List<ProcessInfo>(procs.Length);
        var current = new Dictionary<int, (TimeSpan, DateTime)>();

        foreach (var p in procs)
        {
            try
            {
                var cpuTime = p.TotalProcessorTime;
                double cpuPercent = 0;

                if (_prev.TryGetValue(p.Id, out var old))
                {
                    double usedMs = (cpuTime - old.cpu).TotalMilliseconds;
                    double elapsedMs = (now - old.time).TotalMilliseconds;
                    cpuPercent = usedMs / (Environment.ProcessorCount * elapsedMs) * 100;
                }

                current[p.Id] = (cpuTime, now);

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

    public void Kill(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
        }
        catch { }
    }

    public void KillAllByName(string name)
    {
        foreach (var p in Process.GetProcessesByName(name))
        {
            try
            { p.Kill(entireProcessTree: true); }
            catch { }
            finally { p.Dispose(); }
        }
    }

    public int CountByName(string name) => Process.GetProcessesByName(name).Length;
}