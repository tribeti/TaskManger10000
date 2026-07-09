using core.Helpers;
using core.Models;
using core.Monitor;
using Spectre.Console;
using src.Views;

namespace src;

class Program
{
    private record SystemStats(
        double Cpu,
        double Gpu,
        (double TotalGB, double FreeGB, double UsedPct) Ram,
        (double Download, double Upload) Network,
        (long Ping, int Loss) PingInfo,
        List<DriveMetrics> Drives,
        DiskMetrics Disk
    );

    private class ViewState
    {
        public int SelectedIndex { get; set; }
        public int ScrollOffset { get; set; }
        public SortMode SortMode { get; set; } = SortMode.MemoryDesc;
        public string SearchQuery { get; set; } = "";
        public bool SearchMode { get; set; }
    }

    private static volatile SystemStats? _currentStats;
    private static volatile bool _statsDirty = true;
    private static volatile bool _procDirty = true;

    static void Main()
    {
        // ── Init Helpers ─────────────────────────────────────────
        using var cpu = new CpuHelper();
        using var gpu = new GpuHelper();
        using var network = new NetworkHelper();
        using var disk = new DiskHelper();
        var procMonitor = new ProcessMonitor();

        string cpuName = CpuHelper.GetProcessorCoreName();
        string osName = CpuHelper.GetOSName();
        var (gpuName, gpuDriver) = GpuHelper.GetGPUInfo();

        cpu.WarmUp();
        gpu.WarmUp();
        Thread.Sleep(1000);

        // ── Setup UI & State ─────────────────────────────────────
        var layout = CreateLayout();
        var viewState = new ViewState();

        var procTable = new Table().NoBorder().Expand();
        procTable.AddColumn(new TableColumn("[bold]PID[/]"));
        procTable.AddColumn(new TableColumn("[bold]Name[/]"));
        procTable.AddColumn(new TableColumn("[bold]Memory (MB)[/]").Alignment(Justify.Center));

        // ── Background Tasks ─────────────────────────────────────
        StartStatsCollector(cpu, gpu, network, disk);
        StartProcessCollector(procMonitor);

        // ── Render Loop ──────────────────────────────────────────
        AnsiConsole.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
                List<ProcessInfo> filtered = [];

                while (true)
                {
                    int pageSize = CalculatePageSize();
                    var (inputChanged, shouldExit) = HandleInput(viewState, filtered, procMonitor, pageSize);

                    if (shouldExit)
                        break;

                    bool refreshed = false;

                    if (_statsDirty && _currentStats is not null)
                    {
                        RenderStats(layout, _currentStats, cpuName, osName, gpuName, gpuDriver);
                        _statsDirty = false;
                        refreshed = true;
                    }

                    if (_procDirty || inputChanged)
                    {
                        filtered = procMonitor.GetFiltered(viewState.SearchQuery, viewState.SortMode);

                        if (filtered.Count == 0)
                        {
                            viewState.SelectedIndex = 0;
                            viewState.ScrollOffset = 0;
                        }
                        else
                        {
                            viewState.SelectedIndex = Math.Clamp(viewState.SelectedIndex, 0, filtered.Count - 1);
                        }

                        RenderProcesses(layout, procTable, filtered, viewState, pageSize);
                        _procDirty = false;
                        refreshed = true;
                    }

                    if (refreshed)
                        ctx.Refresh();
                    Thread.Sleep(16);
                }
            });
    }

    // ── 3. Helper Methods ────────────────────────────────────────

    private static Layout CreateLayout()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Stat").Ratio(2),
                new Layout("Process").Ratio(4),
                new Layout("Intro").Size(1));

        layout["Stat"].SplitColumns(
            new Layout("CPU").Ratio(2),
            new Layout("RAM").Ratio(1),
            new Layout("GPU").Ratio(2));

        layout["Process"].SplitColumns(
            new Layout("Table").Ratio(6),
            new Layout("Info").Ratio(4));

        layout["Info"].SplitRows(
            new Layout("Network").Ratio(1),
            new Layout("Disk").Ratio(2));

        layout["Intro"].Update(
            new Panel("[green]K[/]: Kill | [green]Shift+K[/]: Kill All | [blue]S[/]: Sort | [yellow]F[/]: Find | [red]ESC[/]: Clear | [red]Q[/]: Quit")
                .Border(BoxBorder.None).Collapse());

        return layout;
    }

    private static int CalculatePageSize()
    {
        const int IntroHeight = 1;
        const int TableVerticalPadding = 4;
        int termHeight = Console.WindowHeight;
        int remainingForRows = termHeight - IntroHeight;
        int processHeight = (int) (remainingForRows * 4.0 / 6.0);
        return Math.Max(3, processHeight - TableVerticalPadding);
    }

    private static (bool Changed, bool ShouldExit) HandleInput(ViewState state, List<ProcessInfo> processes, ProcessMonitor procMonitor, int pageSize)
    {
        bool changed = false;
        while (Console.KeyAvailable)
        {
            var ki = Console.ReadKey(true);
            var key = ki.Key;

            if (state.SearchMode)
            {
                switch (key)
                {
                    case ConsoleKey.Escape:
                    state.SearchMode = false;
                    state.SearchQuery = "";
                    changed = true;
                    break;
                    case ConsoleKey.Backspace when state.SearchQuery.Length > 0:
                    state.SearchQuery = state.SearchQuery[..^1];
                    changed = true;
                    break;
                    case ConsoleKey.Enter:
                    state.SearchMode = false;
                    changed = true;
                    break;
                    default:
                    if (!char.IsControl(ki.KeyChar))
                    {
                        state.SearchQuery += ki.KeyChar;
                        changed = true;
                    }
                    break;
                }
            }
            else
            {
                switch (key)
                {
                    case ConsoleKey.UpArrow:
                    state.SelectedIndex = Math.Max(0, state.SelectedIndex - 1);
                    if (state.SelectedIndex < state.ScrollOffset)
                        state.ScrollOffset = state.SelectedIndex;
                    changed = true;
                    break;

                    case ConsoleKey.DownArrow:
                    int maxIdx = Math.Max(0, processes.Count - 1);
                    state.SelectedIndex = Math.Min(maxIdx, state.SelectedIndex + 1);
                    if (state.SelectedIndex >= state.ScrollOffset + pageSize)
                        state.ScrollOffset = state.SelectedIndex - pageSize + 1;
                    changed = true;
                    break;

                    case ConsoleKey.K when processes.Count > 0:
                    var target = processes[Math.Min(state.SelectedIndex, processes.Count - 1)];
                    if (ki.Modifiers.HasFlag(ConsoleModifiers.Shift))
                        procMonitor.KillAllByName(target.Name);
                    else
                        procMonitor.Kill(target.Id);
                    changed = true;
                    break;

                    case ConsoleKey.S:
                    state.SortMode = state.SortMode switch
                    {
                        SortMode.MemoryDesc => SortMode.CpuDesc,
                        SortMode.CpuDesc => SortMode.NameAsc,
                        _ => SortMode.MemoryDesc
                    };
                    changed = true;
                    break;

                    case ConsoleKey.F:
                    state.SearchMode = true;
                    state.SearchQuery = "";
                    changed = true;
                    break;

                    case ConsoleKey.Escape:
                    if (!string.IsNullOrEmpty(state.SearchQuery))
                    {
                        state.SearchQuery = "";
                        changed = true;
                    }
                    break;

                    case ConsoleKey.Q:
                    return (false, true);
                }
            }
        }
        return (changed, false);
    }

    private static void RenderStats(Layout layout, SystemStats stats, string cpuName, string osName, string gpuName, string gpuDriver)
    {
        layout["CPU"].Update(CpuPanel.Build(stats.Cpu, cpuName, osName));
        double usedRamGB = stats.Ram.TotalGB * stats.Ram.UsedPct / 100.0;
        layout["RAM"].Update(RamPanel.Build(stats.Ram.UsedPct, usedRamGB, stats.Ram.TotalGB));
        layout["GPU"].Update(GpuPanel.Build(stats.Gpu, gpuName, gpuDriver));
        layout["Network"].Update(NetworkPanel.Build(stats.Network.Download, stats.Network.Upload, stats.PingInfo.Ping, stats.PingInfo.Loss));
        layout["Disk"].Update(DiskPanel.Build(stats.Drives, stats.Disk));
    }

    private static void RenderProcesses(Layout layout, Table procTable, List<ProcessInfo> processes, ViewState state, int pageSize)
    {
        procTable.Rows.Clear();

        if (state.SearchMode || !string.IsNullOrEmpty(state.SearchQuery))
        {
            procTable.Caption = new TableTitle(
                $"[yellow]Search: {Markup.Escape(state.SearchQuery)}{(state.SearchMode ? "_" : "")}[/] ([dim]{processes.Count} results[/])");
        }
        else
        {
            procTable.Caption = null;
        }

        var visible = processes.Skip(state.ScrollOffset).Take(pageSize).ToList();
        for (int i = 0; i < visible.Count; i++)
        {
            var p = visible[i];
            bool isSelected = (state.ScrollOffset + i) == state.SelectedIndex;

            string safeName = Markup.Escape(p.Name);

            if (isSelected)
            {
                procTable.AddRow(
                    $"[black on white]{p.Id}[/]",
                    $"[black on white]{safeName}[/]",
                    $"[black on white]{p.MemoryUsage:N2}[/]"
                );
            }
            else
            {
                string memColor = p.MemoryUsage > 500 ? "red" : p.MemoryUsage > 300 ? "yellow" : "green";
                procTable.AddRow(
                    p.Id.ToString(),
                    safeName,
                    $"[{memColor}]{p.MemoryUsage:N2}[/]"
                );
            }
        }

        layout["Table"].Update(new Panel(procTable).Expand());
    }

    private static void StartStatsCollector(CpuHelper cpu, GpuHelper gpu, NetworkHelper network, DiskHelper disk)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                _currentStats = new SystemStats(
                    cpu.GetUsage(),
                    gpu.GetGPUUsage(),
                    RamHelper.GetMemoryStatus(),
                    network.NetworkSpeed(),
                    network.GetPingAndPacketLoss(),
                    disk.GetAllDrivesUsage(),
                    disk.GetDiskMetrics()
                );

                _statsDirty = true;
                await Task.Delay(1000);
            }
        });
    }

    private static void StartProcessCollector(ProcessMonitor procMonitor)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                procMonitor.Refresh();
                _procDirty = true;
                await Task.Delay(1000);
            }
        });
    }
}