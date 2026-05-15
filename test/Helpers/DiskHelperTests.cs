using core.Helpers;

namespace test.Helpers;

public class DiskHelperTests
{
    [Fact]
    public void GetAllDrivesUsage_ShouldReturnAtLeastOneDrive()
    {
        using var helper = new DiskHelper();
        var drives = helper.GetAllDrivesUsage();
        Assert.NotEmpty(drives);
        Assert.All(drives, d => Assert.True(d.TotalGB > 0));
    }

    [Fact]
    public void GetDiskMetrics_ShouldReturnNonNegativeValues()
    {
        using var helper = new DiskHelper();
        var metrics = helper.GetDiskMetrics();

        // Assert
        Assert.True(metrics.ReadMbps >= 0);
        Assert.True(metrics.WriteMbps >= 0);
        Assert.True(metrics.Iops >= 0);
        Assert.True(metrics.LatencyMs >= 0);
    }
}