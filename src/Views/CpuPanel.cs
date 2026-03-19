using core.Helpers;
using Spectre.Console;

namespace src.Views;

public static class CpuPanel
{
    public static Panel Build(double cpuPct, string cpuName, string osName, IReadOnlyList<float> coreUsages)
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
        grid.AddRow("CPU", $"[dim]{cpuName}[/]");
        // get os version
        grid.AddRow("OS Version", $"[dim]{osName}[/]");

        for (int i = 0; i < coreUsages.Count; i++)
        {
            var pct = Math.Clamp(coreUsages[i], 0f, 100f);
            var color = pct > 80 ? "red" : pct > 50 ? "yellow" : "green";
            var bar = BuildMiniBar(pct);
            grid.AddRow($"[dim]Core {i,2}[/]", $"[{color}]{bar}[/] [dim]{pct,5:F1}%[/]");
        }

        // get uptime
        grid.AddRow("Uptime", $"[dim]{CpuHelper.GetUptime():dd\\.hh\\:mm\\:ss}[/]");

        return new Panel(grid)
            .Header("[bold cyan]CPU[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Cyan1);
    }
    private static string BuildMiniBar(float pct)
    {
        const int barWidth = 10;
        int filled = (int) Math.Round(pct / 100f * barWidth);
        return new string('█', filled) + new string('░', barWidth - filled);
    }
}

