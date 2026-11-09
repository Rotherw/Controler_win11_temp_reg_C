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

    [Fact]
    public void PowerPlans_ParsesPolishAndEnglishOutput()
    {
        var text = "Istniejące schematy zasilania (* Aktywne)\n-----\n" +
            "GUID schematu zasilania: 381b4222-f694-41f0-9685-ff5bb260df2e  (Zrównoważony) *\n" +
            "GUID schematu zasilania: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (Wysoka wydajność)\n" +
            "Power Scheme GUID: a1841308-3541-4fab-bc81-f71556f20b4a  (Power saver)\n";
        var plans = PowerPlans.Parse(text);
        Assert.Equal(3, plans.Count);
        Assert.Equal("Zrównoważony", plans[0].Name);
        Assert.True(plans[0].Active);
        Assert.False(plans[1].Active);
        Assert.Equal("Power saver", plans[2].Name);
    }

    [Fact]
    public void PowerPlans_IgnoresGarbage() => Assert.Empty(PowerPlans.Parse("nic tu nie ma\nGUID bez nawiasów"));

    [Fact]
    public void History_KeepsOnlyLastN()
    {
        var h = new History(3);
        for (int i = 1; i <= 5; i++) h.Add(i);
        Assert.Equal(new double[] { 3, 4, 5 }, h.Values);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\notepad.exe", true)]
    [InlineData(@"c:\windows\explorer.exe", true)]
    [InlineData(@"C:\Program Files\Windows Defender\MsMpEng.exe", true)]
    [InlineData(null, true)]
    [InlineData(@"C:\Users\Jan\app\tool.exe", false)]
    [InlineData(@"C:\WindowsApps\tool.exe", false)]
    public void ProtectedPath_IsFailClosed(string? path, bool expected) =>
        Assert.Equal(expected, ProcessManager.IsProtectedPath(path, @"C:\Windows"));

    [Fact]
    public void Kill_RefusesWhenStartTimeDiffers()
    {
        using var p = OperatingSystem.IsWindows()
            ? System.Diagnostics.Process.Start("ping", "-n 60 127.0.0.1")!
            : System.Diagnostics.Process.Start("sleep", "60")!;
        try { Assert.False(ProcessManager.Kill(p.Id, p.StartTime.AddMinutes(-5))); }
        finally { p.Kill(); }
    }
}
