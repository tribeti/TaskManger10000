using Spectre.Console;

namespace src.Views;

public static class RamPanel
{
    public static Panel Build(double usedPct, double usedGB, double totalGB)
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
        grid.AddRow("Used", $"[bold]{usedGB:F2} / {totalGB:F2} GB[/]");

        return new Panel(grid)
            .Header("[bold green]RAM[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand()
            .BorderColor(Color.Green);
    }
}
