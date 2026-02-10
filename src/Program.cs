using Spectre.Console;
using System.Diagnostics;

namespace src;
class Program
{
    static void Main()
    {
        int pageSize = 15;
        int selectedIndex = 0;
        int scrollOffset = 0;
        DateTime lastDataRefresh = DateTime.MinValue;
        List<Process> currentProcessList = [];

        // header
        var title = new FigletText("Task Manager")
        {
            Color = Color.Blue,
            Justification = Justify.Center
        };

        AnsiConsole.Write(title);
        AnsiConsole.WriteLine();

        var table = new Table()
            .RoundedBorder()
            .Expand()
            .ShowRowSeparators();

        table.AddColumn("[grey]PID[/]");
        table.AddColumn("[bold]Name[/]");
        table.AddColumn(new TableColumn("[bold]Memory (MB)[/]").RightAligned());

        AnsiConsole.Live(table)
          .AutoClear(false)
          .Overflow(VerticalOverflow.Ellipsis)
          .Cropping(VerticalOverflowCropping.Bottom)
          .Start(ctx =>
          {
              while (true)
              {
                  if ((DateTime.Now - lastDataRefresh).TotalSeconds > 1)
                  {
                      currentProcessList = [.. Process.GetProcesses().OrderByDescending(p => p.WorkingSet64)];
                      lastDataRefresh = DateTime.Now;
                  }

                  if (selectedIndex >= currentProcessList.Count)
                      selectedIndex = currentProcessList.Count - 1;
                  if (selectedIndex < 0)
                      selectedIndex = 0;

                  if (Console.KeyAvailable)
                  {
                      var key = Console.ReadKey(true).Key;

                      if (key == ConsoleKey.UpArrow)
                      {
                          selectedIndex--;
                          if (selectedIndex < 0)
                              selectedIndex = 0;
                          if (selectedIndex < scrollOffset)
                              scrollOffset = selectedIndex;
                      }
                      else if (key == ConsoleKey.DownArrow)
                      {
                          selectedIndex++;
                          if (selectedIndex >= currentProcessList.Count)
                              selectedIndex = currentProcessList.Count - 1;
                          if (selectedIndex >= scrollOffset + pageSize)
                              scrollOffset = selectedIndex - pageSize + 1;
                      }
                      else if (key == ConsoleKey.K)
                      {
                          KillProcess(currentProcessList[selectedIndex]);
                          Task.Delay(100);
                          currentProcessList = [.. Process.GetProcesses().OrderByDescending(p => p.WorkingSet64)];
                      }
                  }

                  table.Rows.Clear();
                  var visibleProcesses = currentProcessList.Skip(scrollOffset).Take(pageSize).ToList();

                  for (int i = 0; i < visibleProcesses.Count; i++)
                  {
                      var p = visibleProcesses[i];
                      int realIndex = scrollOffset + i;
                      bool isSelected = (realIndex == selectedIndex);

                      string pid = p.Id.ToString();
                      string name = p.ProcessName;
                      double memVal = p.WorkingSet64 / 1024.0 / 1024.0;
                      string mem = memVal > 500 ? $"[red]{memVal:N2}[/]" : memVal > 300 ? $"[yellow]{memVal:N2}[/]" : $"[green]{memVal:N2}[/]";

                      if (isSelected)
                      {
                          table.AddRow(
                              $"[black on white]{pid}[/]",
                              $"[black on white]{name}[/]",
                              $"[black on white]{memVal:N2}[/]"
                          );
                      }
                      else
                      {
                          table.AddRow(pid, name, mem);
                      }
                  }

                  ctx.UpdateTarget(table);
                  Task.Delay(100);
              }
          });
    }

    public static void KillProcess(Process p)
    {
        try
        {
            p.Kill();
            p.WaitForExit(1000);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks);
        }
    }
}