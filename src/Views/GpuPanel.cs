using Spectre.Console;

namespace src.Views;

public static class GpuPanel
{
    public static Panel Build(double gpuUsage, string gpuName, string driverVer, double UsedVRAM, double TotalVRAM)
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
        grid.AddRow("VRAM", $"{UsedVRAM:N0} MB / {TotalVRAM:N0} MB");

        return new Panel(grid)
            .Header("[bold]GPU[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand();
    }
}