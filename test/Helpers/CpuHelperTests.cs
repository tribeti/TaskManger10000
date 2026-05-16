using core.Helpers;

namespace test.Helpers;

public class CpuHelperTests
{
    [Fact]
    public void GetUsage_ShouldReturnValidPercentage()
    {
        using var helper = new CpuHelper();
        helper.WarmUp();
        double usage = helper.GetUsage();
        Assert.InRange(usage, 0.0, 100.0);
    }

    [Fact]
    public void GetProcessorCoreName_ShouldNotBeEmpty()
    {
        string name = CpuHelper.GetProcessorCoreName();
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.NotEqual("Unknown CPU", name);
    }
}