using core.Helpers;
using Spectre.Console;
using System.Diagnostics;
using System.Management;

namespace src;

class SystemMetrics : IDisposable
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _ramCounter;
    private PerformanceCounter[]? _gpuCounters;

    public float CpuPct { get; private set; }
    public double RamUsedPct { get; private set; }
    public double RamTotalGB { get; private set; }
    public double RamUsedGB { get; private set; }
    public float GpuPct { get; private set; }
    public bool GpuAvailable { get; private set; }

    public SystemMetrics()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        _cpuCounter.NextValue();

        try
        {
            var cat = new PerformanceCounterCategory("GPU Engine");
            var instances = cat.GetInstanceNames()
                              .Where(n => n.Contains("engtype_3D",
                                          StringComparison.OrdinalIgnoreCase))
                              .ToArray();

            if (instances.Length > 0)
            {
                _gpuCounters = instances
                    .Select(i => new PerformanceCounter("GPU Engine", "Utilization Percentage", i))
                    .ToArray();
                // warmup
                foreach (var c in _gpuCounters)
                    c.NextValue();
                GpuAvailable = true;
            }
        }
        catch
        {
            GpuAvailable = false;
        }
    }

    public void Refresh()
    {
        CpuPct = _cpuCounter.NextValue();
        var (total, _, usedPct) = RamHelper.GetMemoryStatus();
        RamTotalGB = total;
        RamUsedPct = usedPct;
        RamUsedGB = total * usedPct / 100.0;

        if (GpuAvailable && _gpuCounters is { Length: > 0 })
        {
            GpuPct = _gpuCounters.Select(c =>
            {
                try
                { return c.NextValue(); }
                catch { return 0f; }
            }).DefaultIfEmpty(0f).Max();
        }
    }

    public void Dispose()
    {
        _cpuCounter.Dispose();
        _ramCounter.Dispose();
        if (_gpuCounters is not null)
            foreach (var c in _gpuCounters)
                c.Dispose();
    }
}

class Program
{
    // ── Helpers vẽ gauge / bar ─────────────────────────────────────────────
    static string ColorForPct(double pct) =>
        pct >= 80 ? "red" : pct >= 50 ? "yellow" : "green";

    static string BuildBar(double pct, int width = 20)
    {
        int filled = (int) (pct / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);
        string col = ColorForPct(pct);
        string bar = new string('█', filled) + new string('░', width - filled);
        return $"[{col}]{bar}[/]";
    }

    static Panel MakeCpuPanel(float cpuPct)
    {
        var grid = new Grid().AddColumn().AddColumn();
        // draw cpu usage bar
        grid.AddRow("Usage", $"{BuildBar(cpuPct)} [{ColorForPct(cpuPct)}]{cpuPct,5:F1}%[/]");
        // get cpu name
        grid.AddRow("CPU", $"[dim]{CpuHelper.GetProcessorCoreName()}[/]");
        // get os version
        grid.AddRow("OS Version", $"[dim]{Environment.OSVersion}[/]");

        return new Panel(grid)
            .Header("[bold cyan]CPU[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Cyan1);
    }

    static Panel MakeRamPanel(double usedPct, double usedGB, double totalGB)
    {
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("Usage", $"{BuildBar(usedPct)} [{ColorForPct(usedPct)}]{usedPct,5:F1}%[/]");
        grid.AddRow("Used", $"[white]{usedGB:F2} / {totalGB:F2} GB[/]");
        var (usedSlots, totalSlots, confSpeed) = RamHelper.GetMemoryInfo();
        grid.AddRow("Slot", $"{usedSlots}/{totalSlots}");
        grid.AddRow("Speed", $"{confSpeed} MHz");
        return new Panel(grid)
            .Header("[bold green]RAM[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Green);
    }

    static Panel MakeGpuPanel(float gpuPct, bool available)
    {
        Panel p;
        if (!available)
        {
            p = new Panel(new Markup("[dim]GPU Engine counter\nnot available[/]"))
                .Header("[bold yellow]GPU[/]", Justify.Center)
                .Border(BoxBorder.Rounded)
                .Expand()
                .BorderColor(Color.Yellow);
        }
        else
        {
            var grid = new Grid().AddColumn().AddColumn();
            grid.AddRow("3D Usage", $"{BuildBar(gpuPct)} [{ColorForPct(gpuPct)}]{gpuPct,5:F1}%[/]");

            string? gpuName = String.Empty;
            string? driverVer = String.Empty;
            using var searcher = new ManagementObjectSearcher("select * from Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                gpuName = obj["Name"]?.ToString();
                driverVer = obj["DriverVersion"].ToString();
            }
            grid.AddRow("Name", gpuName);
            grid.AddRow("DriverVersion", driverVer);
            var gpuCounters = GpuHelper.GetGPUCounters();
            var gpuUsage = GpuHelper.GetGPUUsage(gpuCounters);
            grid.AddRow("Total Usage", gpuUsage.ToString());

            p = new Panel(grid)
                .Header("[bold yellow]GPU[/]", Justify.Center)
                .Border(BoxBorder.Rounded)
                .Expand()
                .BorderColor(Color.Yellow);
        }
        return p;
    }

    static Panel MakeProcessPanel(Table tbl) =>
        new Panel(tbl)
            .Header("[bold white]Processes[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Grey);

    static void Main()
    {
        const int pageSize = 10;

        // ── Build layout (một lần duy nhất) ───────────────────────────────
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Stat").Ratio(2),
                new Layout("Process").Ratio(3));

        layout["Stat"].SplitColumns(
            new Layout("CPU").Ratio(1),
            new Layout("RAM").Ratio(1),
            new Layout("GPU").Ratio(1));

        // Placeholder ban đầu
        layout["CPU"].Update(MakeCpuPanel(0));
        layout["RAM"].Update(MakeRamPanel(0, 0, 0));
        layout["GPU"].Update(MakeGpuPanel(0, false));

        // ── Bảng processes (tái sử dụng object, chỉ xóa rows) ─────────────
        var procTable = new Table().NoBorder().Expand();
        procTable.AddColumn(new TableColumn("[bold]Name[/]"));
        procTable.AddColumn(new TableColumn("[bold]Memory (MB)[/]").RightAligned());
        layout["Process"].Update(MakeProcessPanel(procTable));

        // ── Trạng thái ────────────────────────────────────────────────────
        int selectedIndex = 0;
        int scrollOffset = 0;
        SortMode sortMode = SortMode.MemoryDesc;
        string searchQuery = "";
        bool searchMode = false;
        DateTime lastRefresh = DateTime.MinValue;
        List<Process> procs = [];

        using var metrics = new SystemMetrics();

        AnsiConsole.Write(
            new Panel("[green]K[/]: Kill | [blue]S[/]: Sort | [yellow]F[/]: Find | [red]ESC[/]: Clear search | [red]Q[/]: Quit")
                .Header("Instructions", Justify.Center)
                .Border(BoxBorder.Rounded));
        AnsiConsole.WriteLine();

        // ── Live render trực tiếp vào layout ──────────────────────────────
        AnsiConsole.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .Start(ctx =>
            {
                while (true)
                {
                    bool needRefresh = (DateTime.Now - lastRefresh).TotalSeconds >= 1;

                    if (needRefresh)
                    {
                        procs = [.. Process.GetProcesses()];
                        lastRefresh = DateTime.Now;
                        metrics.Refresh();

                        // Cập nhật panels stat
                        layout["CPU"].Update(MakeCpuPanel(metrics.CpuPct));
                        layout["RAM"].Update(MakeRamPanel(metrics.RamUsedPct,
                                                          metrics.RamUsedGB,
                                                          metrics.RamTotalGB));
                        layout["GPU"].Update(MakeGpuPanel(metrics.GpuPct,
                                                          metrics.GpuAvailable));
                    }

                    // ── Lọc danh sách ─────────────────────────────────────
                    var filtered = string.IsNullOrEmpty(searchQuery)
                        ? procs
                        : [.. procs.Where(p =>
                              p.ProcessName.Contains(searchQuery,
                                  StringComparison.OrdinalIgnoreCase))];

                    // ── Sắp xếp ───────────────────────────────────────────
                    filtered = sortMode switch
                    {
                        SortMode.NameAsc => [.. filtered.OrderBy(p => p.ProcessName)],
                        SortMode.MemoryDesc => [.. filtered.OrderByDescending(p => p.WorkingSet64)],
                        _ => filtered
                    };

                    selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, filtered.Count - 1));

                    // ── Đọc phím ──────────────────────────────────────────
                    if (Console.KeyAvailable)
                    {
                        var ki = Console.ReadKey(true);
                        var key = ki.Key;

                        if (searchMode)
                        {
                            if (key == ConsoleKey.Escape)
                            {
                                searchMode = false;
                                searchQuery = "";
                            }
                            else if (key == ConsoleKey.Backspace && searchQuery.Length > 0)
                                searchQuery = searchQuery[..^1];
                            else if (key == ConsoleKey.Enter)
                                searchMode = false;
                            else if (!char.IsControl(ki.KeyChar))
                                searchQuery += ki.KeyChar;
                        }
                        else
                        {
                            switch (key)
                            {
                                case ConsoleKey.UpArrow:
                                selectedIndex = Math.Max(0, selectedIndex - 1);
                                if (selectedIndex < scrollOffset)
                                    scrollOffset = selectedIndex;
                                break;

                                case ConsoleKey.DownArrow:
                                selectedIndex = Math.Min(filtered.Count - 1, selectedIndex + 1);
                                if (selectedIndex >= scrollOffset + pageSize)
                                    scrollOffset = selectedIndex - pageSize + 1;
                                break;

                                case ConsoleKey.K:
                                if (filtered.Count > 0)
                                {
                                    KillProcess(filtered[selectedIndex]);
                                    Task.Delay(100).Wait();
                                    procs = [.. Process.GetProcesses()];
                                    lastRefresh = DateTime.Now;
                                }
                                break;

                                case ConsoleKey.S:
                                sortMode = sortMode == SortMode.MemoryDesc
                                           ? SortMode.NameAsc
                                           : SortMode.MemoryDesc;
                                break;

                                case ConsoleKey.F:
                                searchMode = true;
                                searchQuery = "";
                                break;

                                case ConsoleKey.Q:
                                return;
                            }
                        }
                    }

                    // ── Cập nhật bảng processes ───────────────────────────
                    procTable.Rows.Clear();

                    if (searchMode || !string.IsNullOrEmpty(searchQuery))
                        procTable.Caption = new TableTitle(
                            $"[yellow]Search: {searchQuery}{(searchMode ? "_" : "")}[/]" +
                            $" ([dim]{filtered.Count} results[/])");
                    else
                        procTable.Caption = null;

                    var visible = filtered.Skip(scrollOffset).Take(pageSize).ToList();
                    for (int i = 0; i < visible.Count; i++)
                    {
                        var p = visible[i];
                        int realIdx = scrollOffset + i;
                        bool sel = realIdx == selectedIndex;

                        string name = p.ProcessName;
                        double mb = p.WorkingSet64 / 1024.0 / 1024.0;
                        string memColored = mb > 500
                            ? $"[red]{mb:N2}[/]"
                            : mb > 300
                                ? $"[yellow]{mb:N2}[/]"
                                : $"[green]{mb:N2}[/]";

                        if (sel)
                            procTable.AddRow($"[black on white]{name}[/]",
                                             $"[black on white]{mb:N2}[/]");
                        else
                            procTable.AddRow(name, memColored);
                    }

                    ctx.Refresh();
                    Task.Delay(100).Wait();
                }
            });
    }

    static void KillProcess(Process p)
    {
        try
        { p.Kill(); p.WaitForExit(1000); }
        catch { }
    }
}