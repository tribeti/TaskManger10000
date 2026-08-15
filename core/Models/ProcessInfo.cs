namespace core.Models;

public record ProcessInfo(
    int Id,
    string Name,
    double MemoryUsage,
    double CpuUsage,
    bool IsSuspended = false
);