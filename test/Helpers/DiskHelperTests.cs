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

        Assert.True(metrics.ReadMbps >= 0);
        Assert.True(metrics.WriteMbps >= 0);
        Assert.True(metrics.Iops >= 0);
        Assert.True(metrics.LatencyMs >= 0);
    }

    [Fact]
    public void GetAllDrivesUsage_DriveMetrics_ShouldHaveValidProperties()
    {
        using var helper = new DiskHelper();
        var drives = helper.GetAllDrivesUsage();

        Assert.All(drives, d =>
        {
            // Drive name should not be empty (e.g. "C:\")
            Assert.False(string.IsNullOrWhiteSpace(d.Name), "Drive name must not be empty");

            // Drive format should not be empty (e.g. "NTFS")
            Assert.False(string.IsNullOrWhiteSpace(d.Format), "Drive format must not be empty");

            // Used percentage should be between 0 and 100
            Assert.InRange(d.UsedPct, 0.0, 100.0);

            // UsedGB should be non-negative and not exceed TotalGB
            Assert.True(d.UsedGB >= 0, "UsedGB must be non-negative");
            Assert.True(d.UsedGB <= d.TotalGB, "UsedGB must not exceed TotalGB");
        });
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var helper = new DiskHelper();
        var ex = Record.Exception(() => helper.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        var helper = new DiskHelper();
        helper.Dispose();
        var ex = Record.Exception(() => helper.Dispose());
        Assert.Null(ex);
    }
}