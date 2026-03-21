using core.Models;
using System.Diagnostics;

namespace core.Monitor;

public class ProcessMonitor
{
    private volatile List<ProcessInfo> _cache = [];
    public void Refresh()
    {
        var procs = Process.GetProcesses();
        var list = new List<ProcessInfo>(procs.Length);

        foreach (var p in procs)
        {
            try
            {
                list.Add(new ProcessInfo(
                    Id: p.Id,
                    Name: p.ProcessName,
                    MemoryUsage: p.WorkingSet64 / 1_048_576.0,
                    CpuUsage: 0
                ));
            }
            catch { }
            finally { p.Dispose(); }
        }

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
        { Process.GetProcessById(pid).Kill(); }
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