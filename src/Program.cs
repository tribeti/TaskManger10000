using core.Helpers;
using Spectre.Console;
using System.Diagnostics;

namespace src;

class Program
{
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

    static Panel MakeGpuPanel()
    {
        var grid = new Grid().AddColumn().AddColumn();
        var gpuUsage = GpuHelper.GetGPUUsage(GpuHelper.GetGPUCounters());
        // draw gpu usage
        grid.AddRow(
            new Markup("Usage"),
            new BreakdownChart()
            .ShowPercentage()
            .Compact()
            .AddItem("Used", gpuUsage, Color.Red)
            .AddItem("Free", 100 - gpuUsage, Color.Green)
        );

        var (gpuName, driverVer) = GpuHelper.GetGPUInfo();
        grid.AddRow("Name", gpuName);
        grid.AddRow("DriverVersion", driverVer);

        return new Panel(grid)
            .Header("[bold yellow]GPU[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Yellow);
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
        layout["GPU"].Update(MakeGpuPanel());

        // ── Bảng processes (tái sử dụng object, chỉ xóa rows) ─────────────
        var procTable = new Table().NoBorder().Expand();
        procTable.AddColumn(new TableColumn("[bold]PID[/]"));
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
                    bool needRefresh = (DateTime.Now - lastRefresh).TotalMilliseconds >= 1500;

                    if (needRefresh)
                    {
                        procs = [.. Process.GetProcesses()];
                        lastRefresh = DateTime.Now;

                        // update stat panels
                        layout["CPU"].Update(MakeCpuPanel(CpuHelper.GetCpuUsage()));
                        var (totalGB, _, usedPct) = RamHelper.GetMemoryStatus();
                        double usedGB = totalGB * usedPct / 100.0;
                        layout["RAM"].Update(MakeRamPanel(usedPct, usedGB, totalGB));
                        layout["GPU"].Update(MakeGpuPanel());
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

                        string pid = p.Id.ToString();
                        string name = p.ProcessName;
                        double mb = p.WorkingSet64 / 1024.0 / 1024.0;
                        string memColored = mb > 500
                            ? $"[red]{mb:N2}[/]"
                            : mb > 300
                                ? $"[yellow]{mb:N2}[/]"
                                : $"[green]{mb:N2}[/]";

                        if (sel)
                            procTable.AddRow($"[black on white]{pid}[/]",
                                             $"[black on white]{name}[/]",
                                             $"[black on white]{mb:N2}[/]");
                        else
                            procTable.AddRow(pid, name, memColored);
                    }

                    ctx.Refresh();
                    Thread.Sleep(200);
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