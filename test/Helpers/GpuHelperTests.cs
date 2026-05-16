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
    public void GetGPUInfo_ShouldReturnName()
    {
        var (name, _) = GpuHelper.GetGPUInfo();
        Assert.NotNull(name);
    }
}