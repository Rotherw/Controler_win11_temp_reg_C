using CoolWatch.Core;

public class CoreTests
{
    [Theory]
    [InlineData(30, 50)]
    [InlineData(80, 80)]
    [InlineData(150, 100)]
    public void Clamp(int input, int expected) => Assert.Equal(expected, CpuLimiter.Clamp(input));

    [Fact]
    public void BuildsPowercfgCommands()
    {
        var c = CpuLimiter.BuildCommands(75);
        Assert.Equal(3, c.Count);
        Assert.Contains("PROCTHROTTLEMAX 75", c[0]);
        Assert.Contains("setdcvalueindex", c[1]);
        Assert.Equal("/setactive SCHEME_CURRENT", c[2]);
    }

    [Fact]
    public void AutoCooler_LimitsWhenHot_AndRestoresWithHysteresis()
    {
        var a = new AutoCooler(thresholdC: 85, hysteresisC: 8, coolLimit: 70);
        Assert.Null(a.Evaluate(70, 100));
        Assert.Equal(70, a.Evaluate(86, 100));
        Assert.True(a.Active);
        Assert.Null(a.Evaluate(80, 70));
        Assert.Equal(100, a.Evaluate(77, 70));
        Assert.False(a.Active);
    }

    [Fact]
    public void AutoCooler_DoesNotRaiseLimitWhenAlreadyLower()
    {
        var a = new AutoCooler(85, 8, 70);
        Assert.Equal(60, a.Evaluate(90, 60));
    }

    [Theory]
    [InlineData(true, "SYSCOOLPOL 1")]
    [InlineData(false, "SYSCOOLPOL 0")]
    public void CoolingPolicyCommands(bool active, string expected)
    {
        var c = CpuLimiter.BuildCoolingPolicyCommands(active);
        Assert.Contains(expected, c[0]);
        Assert.Contains(expected, c[1]);
        Assert.Equal("/setactive SCHEME_CURRENT", c[2]);
    }
}
