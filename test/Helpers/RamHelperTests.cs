using core.Helpers;

namespace test.Helpers;

public class RamHelperTests
{
    [Fact]
    public void GetMemoryStatus_ShouldReturnPositiveValues()
    {
        var (totalGB, _, usedPct) = RamHelper.GetMemoryStatus();
        Assert.True(totalGB > 0, "Total RAM must be greater than 0");
        Assert.InRange(usedPct, 0.0, 100.0);
    }
}