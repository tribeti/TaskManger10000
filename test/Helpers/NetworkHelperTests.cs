using core.Helpers;

namespace test.Helpers;

public class NetworkHelperTests
{
    [Fact]
    public void NetworkSpeed_ShouldReturnNonNegativeSpeed()
    {
        using var helper = new NetworkHelper();
        // Allow time for byte counters to accumulate a delta
        Thread.Sleep(500);
        var (downloadKBps, uploadKBps) = helper.NetworkSpeed();
        Assert.True(downloadKBps >= 0, $"Download speed should be >= 0, got {downloadKBps}");
        Assert.True(uploadKBps >= 0, $"Upload speed should be >= 0, got {uploadKBps}");
    }

    [Fact]
    public void GetPingAndPacketLoss_ShouldReturnValidPing()
    {
        using var helper = new NetworkHelper();
        var (roundtripMs, packetLossPct) = NetworkHelper.GetPingAndPacketLoss();
        Assert.InRange(packetLossPct, 0, 100);
        Assert.True(roundtripMs >= 0, $"Roundtrip time must be non-negative, got {roundtripMs}");
    }

    [Fact]
    public void NetworkSpeed_CalledMultipleTimes_ShouldReturnValidValues()
    {
        using var helper = new NetworkHelper();
        Thread.Sleep(300);

        for (int i = 0; i < 3; i++)
        {
            var (downloadKBps, uploadKBps) = helper.NetworkSpeed();
            Assert.True(downloadKBps >= 0, $"Download speed should be >= 0 on call {i + 1}");
            Assert.True(uploadKBps >= 0, $"Upload speed should be >= 0 on call {i + 1}");
        }
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var helper = new NetworkHelper();
        var ex = Record.Exception(() => helper.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        var helper = new NetworkHelper();
        helper.Dispose();
        var ex = Record.Exception(() => helper.Dispose());
        Assert.Null(ex);
    }
}