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

    [Fact]
    public void GetOSName_ShouldReturnValidWindowsName()
    {
        string osName = CpuHelper.GetOSName();
        Assert.False(string.IsNullOrWhiteSpace(osName));
        Assert.Contains("Windows", osName);
    }

    [Fact]
    public void GetUptime_ShouldReturnPositiveTimeSpan()
    {
        TimeSpan uptime = CpuHelper.GetUptime();
        Assert.True(uptime > TimeSpan.Zero, "System uptime must be greater than zero");
        Assert.True(uptime.TotalSeconds >= 1, "System must have been running for at least 1 second");
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var helper = new CpuHelper();
        helper.WarmUp();
        var ex = Record.Exception(() => helper.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        var helper = new CpuHelper();
        helper.WarmUp();
        helper.Dispose();
        var ex = Record.Exception(() => helper.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void GetUsage_CalledMultipleTimes_ShouldReturnValidValues()
    {
        using var helper = new CpuHelper();
        helper.WarmUp();

        for (int i = 0; i < 3; i++)
        {
            double usage = helper.GetUsage();
            Assert.InRange(usage, 0.0, 100.0);
        }
    }
}