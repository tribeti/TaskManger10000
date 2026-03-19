using core.Helpers;
using Spectre.Console;

namespace src.Views;

public static class CpuPanel
{
    public static Panel Build(double cpuPct, string cpuName, string osName)
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
        grid.AddRow("CPU", $"[bold]{cpuName}[/]");
        // get os version
        grid.AddRow("OS Version", $"[bold]{osName}[/]");
        // get uptime
        grid.AddRow("Uptime", $"[bold]{CpuHelper.GetUptime():dd\\.hh\\:mm\\:ss}[/]");

        return new Panel(grid)
            .Header("[bold cyan]CPU[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Cyan1);
    }
}