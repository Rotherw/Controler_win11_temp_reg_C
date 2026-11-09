using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CoolWatch.Core;

public record PowerPlan(Guid Id, string Name, bool Active);

/// <summary>Odczyt i zmiana planu zasilania Windows (powercfg).</summary>
public static class PowerPlans
{
    static readonly string Exe = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
    static readonly Regex Line = new(@"(?<id>[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})\s+\((?<name>[^)]*)\)\s*(?<star>\*)?");

    // Działa niezależnie od języka systemu: opiera się na GUID, nawiasach i gwiazdce aktywnego planu.
    public static List<PowerPlan> Parse(string output) =>
        output.Split('\n')
            .Select(l => Line.Match(l))
            .Where(m => m.Success)
            .Select(m => new PowerPlan(Guid.Parse(m.Groups["id"].Value), m.Groups["name"].Value.Trim(), m.Groups["star"].Success))
            .ToList();

    public static List<PowerPlan> List()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Exe, "/list")
            {
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true,
            })!;
            var text = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return Parse(text);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new();
        }
    }

    public static bool SetActive(Guid id)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Exe, "/setactive " + id.ToString("D"))
            {
                CreateNoWindow = true, UseShellExecute = false,
            })!;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }
}
