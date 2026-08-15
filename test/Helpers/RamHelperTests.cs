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

    [Fact]
    public void GetMemoryStatus_AvailableGB_ShouldBeValidAndWithinTotal()
    {
        var (totalGB, availGB, _) = RamHelper.GetMemoryStatus();
        Assert.True(availGB > 0, $"Available RAM must be greater than 0, got {availGB}");
        Assert.True(availGB <= totalGB, $"Available RAM ({availGB} GB) must not exceed total RAM ({totalGB} GB)");
    }

    [Fact]
    public void GetMemoryStatus_ValuesShouldBeConsistent()
    {
        var (totalGB, availGB, usedPct) = RamHelper.GetMemoryStatus();

        // Used GB derived from total - available
        double usedGB = totalGB - availGB;
        Assert.True(usedGB >= 0, $"Used RAM must be non-negative, got {usedGB}");

        // usedPct should roughly correspond to (usedGB / totalGB * 100)
        // Allow a tolerance of 5% since values are captured at slightly different moments
        if (totalGB > 0)
        {
            double expectedPct = (usedGB / totalGB) * 100;
            Assert.InRange(usedPct, expectedPct - 5, expectedPct + 5);
        }
    }

    [Fact]
    public void GetMemoryStatus_CalledTwice_ShouldReturnSimilarTotal()
    {
        var (totalGB1, _, _) = RamHelper.GetMemoryStatus();
        var (totalGB2, _, _) = RamHelper.GetMemoryStatus();

        // Total RAM should be identical between calls (hardware doesn't change)
        Assert.Equal(totalGB1, totalGB2);
    }
}