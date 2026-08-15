using core.Helpers;

namespace test.Helpers;

public class GpuHelperTests
{
    [Fact]
    public void GetGPUUsage_ShouldReturnValidPercentage()
    {
        using var helper = new GpuHelper();
        helper.WarmUp();
        double usage = helper.GetGPUUsage();
        Assert.InRange(usage, 0.0, 100.0);
    }

    [Fact]
    public void GetGPUInfo_ShouldReturnValidGpuInfo()
    {
        var (name, driverVer, totalVramMB) = GpuHelper.GetGPUInfo();

        // Either valid GPU info or fallback values — both are acceptable
        if (name != "Unknown GPU")
        {
            Assert.NotEmpty(name?.Trim() ?? string.Empty);
            Assert.NotEmpty(driverVer?.Trim() ?? string.Empty);
            Assert.NotEqual("Unknown Driver Version", driverVer);
            Assert.True(totalVramMB > 0, "GPU with a valid name should report VRAM > 0");
        }
        else
        {
            // Fallback case: no GPU detected
            Assert.Equal("Unknown GPU", name);
            Assert.Equal("Unknown Driver Version", driverVer);
            Assert.Equal(0, totalVramMB);
        }
    }

    [Fact]
    public void InitCounters_ShouldReturnNonNullList()
    {
        var counters = GpuHelper.InitCounters();
        Assert.NotNull(counters);

        // Clean up performance counters
        counters.ForEach(c => c.Dispose());
    }

    [Fact]
    public void GetTotalVRamUsage_ShouldReturnNonNegativeValue()
    {
        double vramUsage = GpuHelper.GetTotalVRamUsage();
        Assert.True(vramUsage >= 0, $"VRAM usage should be non-negative, got {vramUsage}");
    }

    [Fact]
    public void GetGPUUsage_CalledMultipleTimes_ShouldReturnValidValues()
    {
        using var helper = new GpuHelper();
        helper.WarmUp();

        for (int i = 0; i < 3; i++)
        {
            double usage = helper.GetGPUUsage();
            Assert.InRange(usage, 0.0, 100.0);
        }
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var helper = new GpuHelper();
        helper.WarmUp();
        var ex = Record.Exception(() => helper.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        var helper = new GpuHelper();
        helper.Dispose();
        var ex = Record.Exception(() => helper.Dispose());
        Assert.Null(ex);
    }
}