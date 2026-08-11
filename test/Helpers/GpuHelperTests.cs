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

        Assert.NotEmpty(name?.Trim() ?? string.Empty);
        Assert.NotEqual("Unknown GPU", name);
        Assert.NotEmpty(driverVer?.Trim() ?? string.Empty);
        Assert.NotEqual("Unknown Driver Version", driverVer);
        Assert.True(totalVramMB > 0);
    }

    [Fact]
    public void GetGPUInfo_WhenNoGpuFound_ShouldReturnFallbackValues()
    {
        var (name, driverVer, totalVramMB) = GpuHelper.GetGPUInfo();

        if (name == "Unknown GPU")
        {
            Assert.Equal("Unknown GPU", name);
            Assert.Equal("Unknown Driver Version", driverVer);
            Assert.Equal(0, totalVramMB);
        }
        else
        {
            Assert.NotEmpty(name?.Trim() ?? string.Empty);
            Assert.NotEmpty(driverVer?.Trim() ?? string.Empty);
            Assert.True(totalVramMB >= 0);
        }
    }
}