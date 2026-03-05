using core.Helpers;
using Spectre.Console;
using System.Diagnostics;

namespace src;
// TODO : add disk io, network, cpu temp (total & per core), gpu temp
class Program
{
    public record ProcessInfo(int Id, string Name, double MemoryMB);
    public enum SortMode { NameAsc, MemoryDesc }
    static Panel MakeCpuPanel(double cpuPct)
    {
        var grid = new Grid().AddColumn().AddColumn();
        // draw cpu usage bar
        grid.AddRow(
            new Markup("Usage"),
            new BreakdownChart()
            .ShowPercentage()
            .Compact()
            .AddItem("Used", cpuPct, Color.Red)
            .AddItem("Free", 100 - cpuPct, Color.Green)
        );
        // get cpu name
        grid.AddRow("CPU", $"[dim]{CpuHelper.GetProcessorCoreName()}[/]");
        // get os version
        grid.AddRow("OS Version", $"[dim]{CpuHelper.GetOSName()}[/]");
        // get uptime
        grid.AddRow("Uptime", $"[dim]{CpuHelper.GetUptime():dd\\.hh\\:mm\\:ss}[/]");

        return new Panel(grid)
            .Header("[bold cyan]CPU[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Cyan1);
    }

    static Panel MakeRamPanel(double usedPct, double usedGB, double totalGB)
    {
        var grid = new Grid().AddColumn().AddColumn();
        // draw ram usage
        grid.AddRow(
            new Markup("Usage"),
            new BreakdownChart()
            .ShowPercentage()
            .Compact()
            .AddItem("Used", usedPct, Color.Red)
            .AddItem("Free", 100 - usedPct, Color.Green)
        );
        // show used / total in GB
        grid.AddRow("Used", $"[white]{usedGB:F2} / {totalGB:F2} GB[/]");

        return new Panel(grid)
            .Header("[bold green]RAM[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Green);
    }

    static Panel MakeGpuPanel(double gpuUsage, string gpuName, string driverVer)
    {
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow(
            new Markup("Usage"),
            new BreakdownChart()
                .ShowPercentage().Compact()
                .AddItem("Used", gpuUsage, Color.Red)
                .AddItem("Free", 100 - gpuUsage, Color.Green)
        );
        grid.AddRow("Name", gpuName);
        grid.AddRow("Driver Version", driverVer);

        return new Panel(grid)
            .Header("[bold yellow]GPU[/]", Justify.Center)
            .Border(BoxBorder.Rounded).Expand().BorderColor(Color.Yellow);
    }

    static void Main()
    {
        var procTable = new Table().NoBorder().Expand();
        procTable.AddColumn(new TableColumn("[bold]PID[/]"));
        procTable.AddColumn(new TableColumn("[bold]Name[/]"));
        procTable.AddColumn(new TableColumn("[bold]Memory (MB)[/]").RightAligned());

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Stat").Ratio(2),
                new Layout("Process").Ratio(3),
                new Layout("Intro").Ratio(1));

        layout["Stat"].SplitColumns(
            new Layout("CPU").Ratio(1),
            new Layout("RAM").Ratio(1),
            new Layout("GPU").Ratio(1));

        layout["Intro"].Update(
            new Panel("[green]K[/]: Kill | [blue]S[/]: Sort | [yellow]F[/]: Find | [red]ESC[/]: Clear | [red]Q[/]: Quit")
                .Border(BoxBorder.None).Collapse());

        const int pageSize = 10;
        int selectedIndex = 0, scrollOffset = 0;
        SortMode sortMode = SortMode.MemoryDesc;
        string searchQuery = "";
        bool searchMode = false;

        List<ProcessInfo> cachedProcs = [];
        List<ProcessInfo> filtered = [];

        double currentCpu = 0;
        (double totalGB, double freeGB, double usedPct) currentRam = (0, 0, 0);
        double currentGpuUsage = 0;
        string gpuName = "", gpuDriver = "";

        bool statDirty = true;
        bool procDirty = true;
        bool dataChanged = true;

        (gpuName, gpuDriver) = GpuHelper.GetGPUInfo();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                currentCpu = CpuHelper.GetCpuUsage();
                currentRam = RamHelper.GetMemoryStatus();
                currentGpuUsage = GpuHelper.GetGPUUsage(GpuHelper.GetGPUCounters());
                statDirty = true;
                await Task.Delay(500);
            }
        });

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var procs = Process.GetProcesses();
                var newList = new List<ProcessInfo>(procs.Length);
                foreach (var p in procs)
                {
                    try
                    { newList.Add(new ProcessInfo(p.Id, p.ProcessName, p.WorkingSet64 / 1048576.0)); }
                    catch { }
                    finally { p.Dispose(); }
                }
                cachedProcs = newList;
                dataChanged = true;
                await Task.Delay(1000);
            }
        });

        // ── Main render loop ─────────────────────
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
                            { searchMode = false; procDirty = true; }
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
                                    try
                                    { Process.GetProcessById(filtered[selectedIndex].Id).Kill(); }
                                    catch { }
                                    inputChanged = true;
                                }
                                break;
                                case ConsoleKey.S:
                                sortMode = sortMode == SortMode.MemoryDesc ? SortMode.NameAsc : SortMode.MemoryDesc;
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

                    if (dataChanged || inputChanged)
                    {
                        var snapshot = cachedProcs;
                        var query = string.IsNullOrEmpty(searchQuery)
                            ? snapshot
                            : snapshot.Where(p => p.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));

                        filtered = sortMode switch
                        {
                            SortMode.NameAsc => query.OrderBy(p => p.Name).ToList(),
                            SortMode.MemoryDesc => query.OrderByDescending(p => p.MemoryMB).ToList(),
                            _ => query.ToList()
                        };

                        selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, filtered.Count - 1));
                        dataChanged = false;
                        procDirty = true;
                    }

                    bool refreshed = false;

                    if (statDirty)
                    {
                        layout["CPU"].Update(MakeCpuPanel(currentCpu));
                        layout["RAM"].Update(MakeRamPanel(
                            currentRam.usedPct,
                            currentRam.totalGB * currentRam.usedPct / 100.0,
                            currentRam.totalGB));
                        layout["GPU"].Update(MakeGpuPanel(currentGpuUsage, gpuName, gpuDriver));
                        statDirty = false;
                        refreshed = true;
                    }

                    if (procDirty || inputChanged)
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
                                procTable.AddRow(
                                    $"[black on white]{p.Id}[/]",
                                    $"[black on white]{p.Name}[/]",
                                    $"[black on white]{p.MemoryMB:N2}[/]");
                            else
                            {
                                string memColored = p.MemoryMB > 500
                                    ? $"[red]{p.MemoryMB:N2}[/]"
                                    : p.MemoryMB > 300
                                        ? $"[yellow]{p.MemoryMB:N2}[/]"
                                        : $"[green]{p.MemoryMB:N2}[/]";

                                procTable.AddRow(
                                    p.Id.ToString(),
                                    Markup.Escape(p.Name),
                                    memColored);
                            }
                        }

                        layout["Process"].Update(new Panel(procTable).Expand());
                        procDirty = false;
                        refreshed = true;
                    }

                    if (refreshed)
                        ctx.Refresh();
                    Thread.Sleep(16);
                }
            });
    }
}