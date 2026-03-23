using Spectre.Console;

namespace src.Views;

public static class GpuPanel
{
    public static Panel Build(double gpuUsage, string gpuName, string driverVer)
    {
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow(
            new Markup("Usage"),
            new BreakdownChart()
            .ShowPercentage().Compact()
            .AddItem("Used", gpuUsage, Color.Red)
            .AddItem("Free", Math.Round(100 - gpuUsage, 1), Color.Green)
        );
        grid.AddRow("Name", gpuName);
        grid.AddRow("Driver Version", driverVer);

        return new Panel(grid)
            .Header("[bold yellow]GPU[/]", Justify.Center)
            .Border(BoxBorder.Rounded).Expand().BorderColor(Color.Yellow);
    }
}