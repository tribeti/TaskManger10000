using Spectre.Console;

namespace src.Views;

public static class NetworkPanel
{
    public static Panel Build(double sendSpeed, double receiveSpeed, long ping, int loss)
    {
        var grid = new Grid().AddColumn().AddColumn();

        grid.AddRow(
            new BarChart()
            .AddItem("Upload (KBps)", sendSpeed, Color.Blue)
            .AddItem("Download (KBps)", receiveSpeed, Color.Green)
        );

        grid.AddRow("Ping", $"[bold]{ping:F0} ms[/]");
        grid.AddRow("Packet loss", $"[bold]{loss:F2} %[/]");

        return new Panel(grid)
            .Header("[bold]Network[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand();
    }
}
