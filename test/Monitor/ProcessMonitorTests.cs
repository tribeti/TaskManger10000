using core.Models;
using core.Monitor;

namespace test.Monitor;

public class ProcessMonitorTests
{
    [Fact]
    public void Refresh_ShouldPopulateCache()
    {
        var monitor = new ProcessMonitor();
        monitor.Refresh();

        var processes = monitor.GetFiltered("", SortMode.NameAsc);
        Assert.NotEmpty(processes);
    }

    [Fact]
    public void GetFiltered_BeforeRefresh_ShouldReturnEmptyList()
    {
        var monitor = new ProcessMonitor();

        var processes = monitor.GetFiltered("", SortMode.NameAsc);
        Assert.Empty(processes);
    }

    [Fact]
    public void GetFiltered_WithEmptyQuery_ShouldReturnAllProcesses()
    {
        var monitor = new ProcessMonitor();
        monitor.Refresh();

        var all = monitor.GetFiltered("", SortMode.NameAsc);
        Assert.NotEmpty(all);

        // Should match roughly the number of system processes
        Assert.True(all.Count > 1, $"Expected more than 1 process, got {all.Count}");
    }

    [Fact]
    public void GetFiltered_WithQuery_ShouldFilterByName()
    {
        var monitor = new ProcessMonitor();
        monitor.Refresh();

        // Every Windows system has "System" process
        var filtered = monitor.GetFiltered("System", SortMode.NameAsc);
        Assert.NotEmpty(filtered);
        Assert.All(filtered, p =>
            Assert.Contains("System", p.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetFiltered_WithNonExistentQuery_ShouldReturnEmptyList()
    {
        var monitor = new ProcessMonitor();
        monitor.Refresh();

        var filtered = monitor.GetFiltered("ThisProcessShouldNeverExist_XYZ_12345", SortMode.NameAsc);
        Assert.Empty(filtered);
    }

    [Fact]
    public void GetFiltered_SortByNameAsc_ShouldReturnSortedList()
    {
        var monitor = new ProcessMonitor();
        monitor.Refresh();

        var sorted = monitor.GetFiltered("", SortMode.NameAsc);
        Assert.NotEmpty(sorted);

        // Verify the list is sorted the same way as source: LINQ OrderBy uses CurrentCulture by default
        var expected = sorted.OrderBy(p => p.Name).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            Assert.Equal(expected[i].Name, sorted[i].Name);
        }
    }

    [Fact]
    public void GetFiltered_SortByMemoryDesc_ShouldReturnSortedList()
    {
        var monitor = new ProcessMonitor();
        monitor.Refresh();

        var sorted = monitor.GetFiltered("", SortMode.MemoryDesc);
        Assert.NotEmpty(sorted);

        for (int i = 1; i < sorted.Count; i++)
        {
            Assert.True(
                sorted[i - 1].MemoryUsage >= sorted[i].MemoryUsage,
                $"Expected MemoryUsage {sorted[i - 1].MemoryUsage} >= {sorted[i].MemoryUsage} (Memory descending)");
        }
    }

    [Fact]
    public void GetFiltered_SortByCpuDesc_ShouldReturnSortedList()
    {
        var monitor = new ProcessMonitor();
        monitor.Refresh();

        var sorted = monitor.GetFiltered("", SortMode.CpuDesc);
        Assert.NotEmpty(sorted);

        for (int i = 1; i < sorted.Count; i++)
        {
            Assert.True(
                sorted[i - 1].CpuUsage >= sorted[i].CpuUsage,
                $"Expected CpuUsage {sorted[i - 1].CpuUsage} >= {sorted[i].CpuUsage} (CPU descending)");
        }
    }

    [Fact]
    public void Refresh_CalledMultipleTimes_ShouldUpdateCache()
    {
        var monitor = new ProcessMonitor();

        monitor.Refresh();
        var first = monitor.GetFiltered("", SortMode.NameAsc);
        Assert.NotEmpty(first);

        monitor.Refresh();
        var second = monitor.GetFiltered("", SortMode.NameAsc);
        Assert.NotEmpty(second);
    }

    [Fact]
    public void GetFiltered_ProcessInfo_ShouldHaveValidProperties()
    {
        var monitor = new ProcessMonitor();
        monitor.Refresh();

        var processes = monitor.GetFiltered("", SortMode.NameAsc);

        Assert.All(processes, p =>
        {
            Assert.True(p.Id > 0, $"Process ID must be positive, got {p.Id}");
            Assert.False(string.IsNullOrEmpty(p.Name), "Process name must not be empty");
            Assert.True(p.MemoryUsage >= 0, $"Memory usage must be non-negative, got {p.MemoryUsage}");
            Assert.True(p.CpuUsage >= 0, $"CPU usage must be non-negative, got {p.CpuUsage}");
        });
    }
}
