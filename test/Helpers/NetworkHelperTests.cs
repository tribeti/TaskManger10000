using core.Helpers;

namespace test.Helpers;

public class NetworkHelperTests
{
    [Fact]
    public void NetworkSpeed_ShouldReturnNonNegativeSpeed()
    {
        using var helper = new NetworkHelper();
        Thread.Sleep(1000);
        var (downloadKBps, uploadKBps) = helper.NetworkSpeed();
        Assert.True(downloadKBps >= 0);
        Assert.True(uploadKBps >= 0);
    }

    [Fact]
    public void GetPingAndPacketLoss_ShouldReturnValidPing()
    {
        using var helper = new NetworkHelper();
        var (roundtripMs, packetLossPct) = helper.GetPingAndPacketLoss();
        Assert.InRange(packetLossPct, 0, 100);
        Assert.True(roundtripMs >= 0);
    }
}