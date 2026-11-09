using System.Diagnostics;

namespace CoolWatch.Core;

/// <summary>Ogranicza maksymalne obciążenie procesora przez aktywny plan zasilania (działa na każdym laptopie).</summary>
public static class CpuLimiter
{
    public const int Min = 50, Max = 100;

    public static int Clamp(int percent) => Math.Clamp(percent, Min, Max);

    public static IReadOnlyList<string> BuildCommands(int percent)
    {
        var p = Clamp(percent);
        return new[]
        {
            $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {p}",
            $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX {p}",
            "/setactive SCHEME_CURRENT",
        };
    }

    // SYSCOOLPOL: 1 = aktywne chłodzenie (najpierw wentylator), 0 = pasywne (najpierw zwolnienie procesora).
    public static IReadOnlyList<string> BuildCoolingPolicyCommands(bool active)
    {
        var v = active ? 1 : 0;
        return new[]
        {
            $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL {v}",
            $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL {v}",
            "/setactive SCHEME_CURRENT",
        };
    }

    public static bool Apply(int percent) => Run(BuildCommands(percent));

    public static bool SetActiveCooling(bool active) => Run(BuildCoolingPolicyCommands(active));

    static bool Run(IEnumerable<string> commands)
    {
        foreach (var args in commands)
        {
            using var proc = Process.Start(new ProcessStartInfo("powercfg", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            proc!.WaitForExit(5000);
            if (proc.ExitCode != 0) return false;
        }
        return true;
    }
}
