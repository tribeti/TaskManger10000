using Spectre.Console;
using System.Diagnostics;

namespace src;

class Program
{
    static void Main()
    {
        int pageSize = 10;
        int selectedIndex = 0;
        int scrollOffset = 0;
        DateTime lastDataRefresh = DateTime.MinValue;
        List<Process> currentProcessList = [];
        SortMode currentSortMode = SortMode.MemoryDesc;
        string searchQuery = "";
        bool searchMode = false;

        // header
        var title = new FigletText("Task Manager")
        {
            Color = Color.Blue,
            Justification = Justify.Center
        };

        AnsiConsole.Write(title);
        AnsiConsole.WriteLine();

        var instructions = new Panel(
            "[green]K[/]: Kill Process | [blue]S[/]: Sort | [yellow]F[/]: Find | [red]ESC[/]: Escape | [red]Q[/]: Quit"
        )
            .Header("Instructions", Justify.Center)
            .HeaderAlignment(Justify.Center)
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(instructions);
        AnsiConsole.WriteLine();

        var table = new Table()
            .RoundedBorder()
            .Expand()
            .ShowRowSeparators();


        table.AddColumn(new TableColumn(new Header("Name", SortMode.None)));
        table.AddColumn(new TableColumn(new Header("Memory (MB)", SortMode.None)).RightAligned());

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
                      currentProcessList = [.. Process.GetProcesses()];
                      ApplySorting(ref currentProcessList, currentSortMode);
                      lastDataRefresh = DateTime.Now;
                  }

                  var filteredProcesses = string.IsNullOrEmpty(searchQuery)
                      ? currentProcessList
                      : [.. currentProcessList.Where(p => p.ProcessName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))];

                  if (selectedIndex >= filteredProcesses.Count)
                      selectedIndex = filteredProcesses.Count - 1;
                  if (selectedIndex < 0)
                      selectedIndex = 0;

                  if (Console.KeyAvailable)
                  {
                      var keyInfo = Console.ReadKey(true);
                      var key = keyInfo.Key;

                      if (searchMode)
                      {
                          if (key == ConsoleKey.Escape)
                          {
                              searchMode = false;
                              searchQuery = "";
                          }
                          else if (key == ConsoleKey.Backspace && searchQuery.Length > 0)
                          {
                              searchQuery = searchQuery[..^1];
                          }
                          else if (key == ConsoleKey.Enter)
                          {
                              searchMode = false;
                          }
                          else if (!char.IsControl(keyInfo.KeyChar))
                          {
                              searchQuery += keyInfo.KeyChar;
                          }
                      }
                      else
                      {
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
                              if (selectedIndex >= filteredProcesses.Count)
                                  selectedIndex = filteredProcesses.Count - 1;
                              if (selectedIndex >= scrollOffset + pageSize)
                                  scrollOffset = selectedIndex - pageSize + 1;
                          }
                          else if (key == ConsoleKey.K)
                          {
                              if (filteredProcesses.Count > 0)
                              {
                                  KillProcess(filteredProcesses[selectedIndex]);
                                  Task.Delay(100).Wait();
                                  currentProcessList = [.. Process.GetProcesses()];
                                  ApplySorting(ref currentProcessList, currentSortMode);
                              }
                          }
                          else if (key == ConsoleKey.S)
                          {
                              currentSortMode = currentSortMode switch
                              {
                                  SortMode.MemoryDesc => SortMode.NameAsc,
                                  SortMode.NameAsc => SortMode.MemoryDesc,
                                  _ => SortMode.MemoryDesc
                              };
                              ApplySorting(ref currentProcessList, currentSortMode);
                              scrollOffset = 0;
                              selectedIndex = 0;
                          }
                          else if (key == ConsoleKey.F)
                          {
                              searchMode = true;
                              searchQuery = "";
                          }
                          else if (key == ConsoleKey.Q)
                          {
                              return;
                          }
                      }
                  }

                  table = new Table()
                      .RoundedBorder()
                      .Expand()
                      .ShowRowSeparators();

                  table.AddColumn(new TableColumn(new Header("Name", currentSortMode == SortMode.NameAsc ? SortMode.NameAsc : SortMode.None)));
                  table.AddColumn(new TableColumn(new Header("Memory (MB)", currentSortMode == SortMode.MemoryDesc ? SortMode.MemoryDesc : SortMode.None)).RightAligned());

                  if (searchMode || !string.IsNullOrEmpty(searchQuery))
                  {
                      table.Caption = new TableTitle($"[yellow]Searching: {searchQuery}{(searchMode ? "_" : "")}[/] ([dim]{filteredProcesses.Count} results[/])");
                  }
                  else
                  {
                      table.Caption = null;
                  }

                  table.Rows.Clear();
                  var visibleProcesses = filteredProcesses.Skip(scrollOffset).Take(pageSize).ToList();

                  for (int i = 0; i < visibleProcesses.Count; i++)
                  {
                      var p = visibleProcesses[i];
                      int realIndex = scrollOffset + i;
                      bool isSelected = (realIndex == selectedIndex);
                      string name = p.ProcessName;
                      double memVal = p.WorkingSet64 / 1024.0 / 1024.0;
                      string mem = memVal > 500 ? $"[red]{memVal:N2}[/]" : memVal > 300 ? $"[yellow]{memVal:N2}[/]" : $"[green]{memVal:N2}[/]";

                      if (isSelected)
                      {
                          table.AddRow(
                              $"[black on white]{name}[/]",
                              $"[black on white]{mem}[/]"
                          );
                      }
                      else
                      {
                          table.AddRow(name, mem);
                      }
                  }

                  ctx.UpdateTarget(table);
                  Task.Delay(100).Wait();
              }
          });
    }

    private static void ApplySorting(ref List<Process> processes, SortMode sortMode)
    {
        processes = sortMode switch
        {
            SortMode.MemoryDesc => [.. processes.OrderByDescending(p => p.WorkingSet64)],
            SortMode.NameAsc => [.. processes.OrderBy(p => p.ProcessName)],
            _ => processes
        };
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