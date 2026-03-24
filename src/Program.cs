using core.Helpers;
using core.Models;
using core.Monitor;
using Spectre.Console;
using src.Views;

namespace src;

class Program
{
    static double _currentCpu = 0;
    static double _currentGpuUsage = 0;
    static (double send, double recieve) _currentNetwork;
    static (long ping, int loss) _currentNetwork1;

    static (double totalGB, double freeGB, double usedPct) _currentRam;
    static readonly object _ramLock = new();
    static readonly object _networkLock = new();
    static readonly object _networkLock1 = new();

    static volatile bool _statDirty = true;
    static volatile bool _procDirty = true;
    static volatile bool _dataChanged = true;

    static void Main()
    {
        // ── Init ─────────────────────────────────────────────
        using var cpu = new CpuHelper();
        using var gpu = new GpuHelper();
        using var network = new NetworkHelper();
        var procMonitor = new ProcessMonitor();

        string cpuName = CpuHelper.GetProcessorCoreName();
        string osName = CpuHelper.GetOSName();
        var (gpuName, gpuDriver) = GpuHelper.GetGPUInfo();

        cpu.WarmUp();
        gpu.WarmUp();
        Thread.Sleep(1000);

        // ── Layout ───────────────────────────────────────────
        var procTable = new Table().NoBorder().Expand();
        procTable.AddColumn(new TableColumn("[bold]PID[/]"));
        procTable.AddColumn(new TableColumn("[bold]Name[/]"));
        procTable.AddColumn(new TableColumn("[bold]Memory (MB)[/]").Alignment(Justify.Center));
        procTable.AddColumn(new TableColumn("[bold]Cpu Usage[/]").RightAligned());

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Stat").Ratio(2),
                new Layout("Process").Ratio(3),
                new Layout("Intro").Ratio(1));

        layout["Stat"].SplitColumns(
            new Layout("CPU").Ratio(1),
            new Layout("RAM").Ratio(1),
            new Layout("GPU").Ratio(1));

        layout["Process"].SplitColumns(
            new Layout("Table").Ratio(7),
            new Layout("Info").Ratio(3));

        layout["Info"].SplitRows(
            new Layout("Network").Ratio(1),
            new Layout("Disk").Ratio(1));

        layout["Intro"].Update(
            new Panel("[green]K[/]: Kill | [green]Shift+K[/]: Kill All | [blue]S[/]: Sort | [yellow]F[/]: Find | [red]ESC[/]: Clear | [red]Q[/]: Quit")
                .Border(BoxBorder.None).Collapse());

        const int pageSize = 10;
        int selectedIndex = 0, scrollOffset = 0;
        var sortMode = SortMode.MemoryDesc;
        string searchQuery = "";
        bool searchMode = false;
        List<ProcessInfo> filtered = [];

        _ = Task.Run(async () =>
        {
            while (true)
            {
                _currentCpu = cpu.GetUsage();
                _currentGpuUsage = gpu.GetGPUUsage();

                var ram = RamHelper.GetMemoryStatus();
                lock (_ramLock)
                { _currentRam = ram; }

                var net = network.NetworkSpeed();
                lock (_networkLock)
                { _currentNetwork = net; }

                var net1 = network.GetPingAndPacketLoss();
                lock (_networkLock1)
                { _currentNetwork1 = net1; }

                _statDirty = true;
                await Task.Delay(1000);
            }
        });

        // ── Background task: process list ────────────────────
        _ = Task.Run(async () =>
        {
            while (true)
            {
                procMonitor.Refresh();
                _dataChanged = true;
                await Task.Delay(1000);
            }
        });

        // ── Render loop ───────────────────────────────────────
        AnsiConsole.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
                while (true)
                {
                    bool inputChanged = false;

                    while (Console.KeyAvailable)
                    {
                        var ki = Console.ReadKey(true);
                        var key = ki.Key;

                        if (searchMode)
                        {
                            if (key == ConsoleKey.Escape)
                            { searchMode = false; searchQuery = ""; inputChanged = true; }
                            else if (key == ConsoleKey.Backspace && searchQuery.Length > 0)
                            { searchQuery = searchQuery[..^1]; inputChanged = true; }
                            else if (key == ConsoleKey.Enter)
                            { searchMode = false; _procDirty = true; }
                            else if (!char.IsControl(ki.KeyChar))
                            { searchQuery += ki.KeyChar; inputChanged = true; }
                        }
                        else
                        {
                            switch (key)
                            {
                                case ConsoleKey.UpArrow:
                                selectedIndex = Math.Max(0, selectedIndex - 1);
                                if (selectedIndex < scrollOffset)
                                    scrollOffset = selectedIndex;
                                inputChanged = true;
                                break;

                                case ConsoleKey.DownArrow:
                                selectedIndex = Math.Min(Math.Max(0, filtered.Count - 1), selectedIndex + 1);
                                if (selectedIndex >= scrollOffset + pageSize)
                                    scrollOffset = selectedIndex - pageSize + 1;
                                inputChanged = true;
                                break;

                                case ConsoleKey.K:
                                if (filtered.Count > 0)
                                {
                                    var target = filtered[selectedIndex];
                                    var count = procMonitor.CountByName(target.Name);

                                    if (count > 1 && (ki.Modifiers & ConsoleModifiers.Shift) != 0)
                                        procMonitor.KillAllByName(target.Name);
                                    else
                                        procMonitor.Kill(target.Id);

                                    inputChanged = true;
                                }
                                break;

                                case ConsoleKey.S:
                                sortMode = sortMode == SortMode.MemoryDesc
                                    ? SortMode.NameAsc
                                    : SortMode.MemoryDesc;
                                inputChanged = true;
                                break;

                                case ConsoleKey.F:
                                searchMode = true;
                                searchQuery = "";
                                inputChanged = true;
                                break;

                                case ConsoleKey.Q:
                                return;
                            }
                        }
                    }

                    // Filter + sort
                    if (_dataChanged || inputChanged)
                    {
                        filtered = procMonitor.GetFiltered(searchQuery, sortMode);
                        selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, filtered.Count - 1));
                        _dataChanged = false;
                        _procDirty = true;
                    }

                    bool refreshed = false;

                    if (_statDirty)
                    {
                        (double totalGB, double freeGB, double usedPct) ram;
                        lock (_ramLock)
                        { ram = _currentRam; }

                        (double send, double receive) net;
                        lock (_networkLock)
                        { net = _currentNetwork; }

                        (long ping, int loss) net1;
                        lock (_networkLock1)
                        { net1 = _currentNetwork1; }

                        layout["CPU"].Update(CpuPanel.Build(_currentCpu, cpuName, osName));
                        layout["RAM"].Update(RamPanel.Build(
                            ram.usedPct,
                            ram.totalGB * ram.usedPct / 100.0,
                            ram.totalGB));
                        layout["GPU"].Update(GpuPanel.Build(_currentGpuUsage, gpuName, gpuDriver));
                        layout["Network"].Update(NetworkPanel.Build(net.receive, net.send, net1.ping, net1.loss));

                        _statDirty = false;
                        refreshed = true;
                    }

                    if (_procDirty || inputChanged)
                    {
                        procTable.Rows.Clear();

                        if (searchMode || !string.IsNullOrEmpty(searchQuery))
                            procTable.Caption = new TableTitle(
                                $"[yellow]Search: {searchQuery}{(searchMode ? "_" : "")}[/] ([dim]{filtered.Count} results[/])");
                        else
                            procTable.Caption = null;

                        var visible = filtered.Skip(scrollOffset).Take(pageSize).ToList();
                        for (int i = 0; i < visible.Count; i++)
                        {
                            var p = visible[i];
                            bool sel = (scrollOffset + i) == selectedIndex;

                            if (sel)
                            {
                                procTable.AddRow(
                                    $"[black on white]{p.Id}[/]",
                                    $"[black on white]{p.Name}[/]",
                                    $"[black on white]{p.MemoryUsage:N2}[/]",
                                    $"[black on white]{p.CpuUsage:N2}[/]"
                                );
                            }
                            else
                            {
                                string memColor = p.MemoryUsage > 500 ? "red"
                                               : p.MemoryUsage > 300 ? "yellow"
                                               : "green";
                                procTable.AddRow(
                                    p.Id.ToString(),
                                    Markup.Escape(p.Name),
                                    $"[{memColor}]{p.MemoryUsage:N2}[/]",
                                    $"[bold]{p.CpuUsage:N2}[/]"
                                );
                            }
                        }

                        layout["Table"].Update(new Panel(procTable).Expand());
                        _procDirty = false;
                        refreshed = true;
                    }

                    if (refreshed)
                        ctx.Refresh();
                    Thread.Sleep(16);
                }
            });
    }
}