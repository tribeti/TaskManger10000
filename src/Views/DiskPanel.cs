using core.Helpers;
using Spectre.Console;

namespace src.Views;

public static class DiskPanel
{
    public static Panel Build(List<DriveMetrics> drives, DiskMetrics metrics)
    {
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn(new TableColumn("[bold cyan]Drive[/]").Centered());
        table.AddColumn(new TableColumn("[bold cyan]Used / Total[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold cyan]Usage %[/]").Centered());

        foreach (var drive in drives)
        {
            string usageColor = drive.UsedPct > 90 ? "red"
                              : drive.UsedPct > 75 ? "yellow"
                              : "green";

            table.AddRow(
                $"[bold]{drive.Name}[/] ({drive.Format})",
                $"{drive.UsedGB:F1} / {drive.TotalGB:F1} GB",
                $"[{usageColor}]{drive.UsedPct:F1}%[/]"
            );
        }

        var grid = new Grid().AddColumn().AddColumn();

        // Read/Write performance
        grid.AddRow(
            new BarChart()
            .AddItem("Read (MB/s)", metrics.ReadMbps, Color.Blue)
            .AddItem("Write (MB/s)", metrics.WriteMbps, Color.Cyan)
        );

        // IOPS and latency
        grid.AddRow("IOPS", $"[bold]{metrics.Iops:F0}[/]");
        grid.AddRow("Latency", $"[bold]{metrics.LatencyMs:F2} ms[/]");

        var combined = new Rows(table, new Markup(""), grid);

        return new Panel(combined)
            .Header("[bold]Disk[/]", Justify.Center)
            .Border(BoxBorder.Rounded)
            .Expand();
    }
}
