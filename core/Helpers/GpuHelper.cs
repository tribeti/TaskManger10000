using System.Diagnostics;

namespace core.Helpers;

// Source - https://stackoverflow.com/a/71481615
// Posted by Fidel, modified by community. See post 'Timeline' for change history
// Retrieved 2026-02-18, License - CC BY-SA 4.0

public class GpuHelper
{
    public static List<PerformanceCounter> GetGPUCounters()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var counterNames = category.GetInstanceNames();

        var gpuCounters = counterNames
                            .Where(counterName => counterName.EndsWith("engtype_3D"))
                            .SelectMany(counterName => category.GetCounters(counterName))
                            .Where(counter => counter.CounterName.Equals("Utilization Percentage"))
                            .ToList();

        return gpuCounters;
    }

    public static float GetGPUUsage(List<PerformanceCounter> gpuCounters)
    {
        gpuCounters.ForEach(x => x.NextValue());
        Thread.Sleep(1000);
        var result = gpuCounters.Sum(x => x.NextValue());
        return result;
    }
}