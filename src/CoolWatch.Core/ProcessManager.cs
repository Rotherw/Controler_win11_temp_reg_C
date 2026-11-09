using System.Diagnostics;

namespace CoolWatch.Core;

public record ProcInfo(int Id, string Name, double CpuPercent, double MemoryMb, bool Protected);

/// <summary>Lista procesów z obciążeniem CPU i bezpieczne kończenie programów działających w tle.</summary>
public class ProcessManager
{
    // Procesy, których zakończenie zawiesza system lub pulpit.
    static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost",
        "dwm", "explorer", "fontdrvhost", "sihost", "ctfmon", "taskhostw", "RuntimeBroker", "ShellExperienceHost",
        "StartMenuExperienceHost", "SearchHost", "SecurityHealthService", "MsMpEng", "NisSrv", "audiodg", "spoolsv",
        "Memory Compression", "conhost", "WmiPrvSE", "dllhost", "LogonUI", "CoolWatch.App",
    };

    readonly Dictionary<int, (TimeSpan Cpu, DateTime At)> _last = new();

    public static bool IsProtected(string name) => ProtectedNames.Contains(name);

    public static double CpuPercent(TimeSpan cpuDelta, TimeSpan wallDelta, int cores) =>
        wallDelta <= TimeSpan.Zero || cores <= 0
            ? 0
            : Math.Clamp(cpuDelta.TotalMilliseconds / (wallDelta.TotalMilliseconds * cores) * 100, 0, 100);

    /// <summary>Procesy bieżącego użytkownika (sesja interaktywna), posortowane malejąco wg CPU. Pierwszy odczyt daje 0% CPU.</summary>
    public List<ProcInfo> List()
    {
        var now = DateTime.UtcNow;
        var self = Environment.ProcessId;
        var mySession = Process.GetCurrentProcess().SessionId;
        var result = new List<ProcInfo>();
        var seen = new HashSet<int>();

        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (p.Id == self || p.Id <= 4 || p.SessionId != mySession) continue;
                    var cpu = p.TotalProcessorTime;
                    double pct = 0;
                    if (_last.TryGetValue(p.Id, out var prev))
                        pct = CpuPercent(cpu - prev.Cpu, now - prev.At, Environment.ProcessorCount);
                    _last[p.Id] = (cpu, now);
                    seen.Add(p.Id);
                    result.Add(new ProcInfo(p.Id, p.ProcessName, pct, p.WorkingSet64 / 1048576.0, IsProtected(p.ProcessName)));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            }
        }
        foreach (var id in _last.Keys.Where(k => !seen.Contains(k)).ToList()) _last.Remove(id);
        return result.OrderByDescending(r => r.CpuPercent).ThenByDescending(r => r.MemoryMb).ToList();
    }

    /// <summary>Kończy proces wraz z potomnymi. Zwraca false dla chronionych procesów lub gdy się nie udało.</summary>
    public static bool Kill(int id)
    {
        try
        {
            using var p = Process.GetProcessById(id);
            if (IsProtected(p.ProcessName) || id == Environment.ProcessId) return false;
            p.Kill(entireProcessTree: true);
            return p.WaitForExit(3000);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
