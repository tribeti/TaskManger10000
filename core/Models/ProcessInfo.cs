namespace core.Models;

public record ProcessInfo(
    int Id,
    string Name,
    double MemoryUsage,
    double CpuUsage,
    string DiskUsage,
    string NetworkUsage
);