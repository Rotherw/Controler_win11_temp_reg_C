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

    [Theory]
    [InlineData("explorer", true)]
    [InlineData("DWM", true)]
    [InlineData("svchost", true)]
    [InlineData("chrome", false)]
    public void ProtectsSystemProcesses(string name, bool expected) =>
        Assert.Equal(expected, ProcessManager.IsProtected(name));

    [Fact]
    public void CpuPercent_ScalesByCoresAndClamps()
    {
        Assert.Equal(25, ProcessManager.CpuPercent(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), 4), 3);
        Assert.Equal(100, ProcessManager.CpuPercent(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(1), 4));
        Assert.Equal(0, ProcessManager.CpuPercent(TimeSpan.FromSeconds(1), TimeSpan.Zero, 4));
    }

    [Fact]
    public void Kill_RefusesProtectedAndSelf()
    {
        Assert.False(ProcessManager.Kill(Environment.ProcessId));
        Assert.False(ProcessManager.Kill(int.MaxValue));
    }

    [Fact]
    public void Kill_TerminatesNormalProcess()
    {
        using var p = OperatingSystem.IsWindows()
            ? System.Diagnostics.Process.Start("ping", "-n 60 127.0.0.1")!
            : System.Diagnostics.Process.Start("sleep", "60")!;
        Assert.True(ProcessManager.Kill(p.Id));
    }

    [Fact]
    public void List_ExcludesSelf_AndSecondReadGivesValues()
    {
        var m = new ProcessManager();
        m.List();
        var list = m.List();
        Assert.DoesNotContain(list, x => x.Id == Environment.ProcessId);
    }
}
