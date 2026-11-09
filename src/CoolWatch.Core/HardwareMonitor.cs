using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;

namespace CoolWatch.Core;

public record FanInfo(string Name, float Rpm, bool Controllable);

public record DiskInfo(string Name, float? UsedPercent, float? TempC);

public record Snapshot(float? CpuTempC, float? CpuLoad, float? GpuTempC, IReadOnlyList<FanInfo> Fans)
{
    public float? RamUsedPercent { get; init; }
    public float? RamUsedGb { get; init; }
    public float? RamTotalGb { get; init; }
    public IReadOnlyList<DiskInfo> Disks { get; init; } = Array.Empty<DiskInfo>();
    public float? NetDownKBps { get; init; }
    public float? NetUpKBps { get; init; }
}

/// <summary>Odczyt temperatur, obciążenia i wentylatorów przez LibreHardwareMonitor (wymaga uprawnień administratora).</summary>
public sealed class HardwareMonitor : IDisposable
{
    readonly Computer _pc = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsMotherboardEnabled = true,
        IsMemoryEnabled = true, IsStorageEnabled = true, IsNetworkEnabled = true };

    public HardwareMonitor() => _pc.Open();

    public Snapshot Read()
    {
        var all = new List<IHardware>();
        foreach (var h in _pc.Hardware) Collect(h, all);

        var cpu = all.Where(h => h.HardwareType == HardwareType.Cpu).ToList();
        var gpu = all.Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel).ToList();

        return new Snapshot(
            CpuTemp(cpu),
            cpu.SelectMany(h => h.Sensors).FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == "CPU Total")?.Value,
            gpu.SelectMany(h => h.Sensors).FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)?.Value,
            Fans(all))
        {
            RamUsedPercent = Sensor(all, HardwareType.Memory, SensorType.Load, "Memory"),
            RamUsedGb = Sensor(all, HardwareType.Memory, SensorType.Data, "Memory Used"),
            RamTotalGb = SumRam(all),
            Disks = Disks(all),
            NetDownKBps = NetSum(all, "Download Speed"),
            NetUpKBps = NetSum(all, "Upload Speed"),
        };
    }

    static float? Sensor(List<IHardware> all, HardwareType type, SensorType st, string name) =>
        all.Where(h => h.HardwareType == type).SelectMany(h => h.Sensors)
           .FirstOrDefault(s => s.SensorType == st && s.Name == name)?.Value;

    static float? SumRam(List<IHardware> all)
    {
        var used = Sensor(all, HardwareType.Memory, SensorType.Data, "Memory Used");
        var free = Sensor(all, HardwareType.Memory, SensorType.Data, "Memory Available");
        return used.HasValue && free.HasValue ? used + free : null;
    }

    static List<DiskInfo> Disks(List<IHardware> all) =>
        all.Where(h => h.HardwareType == HardwareType.Storage).Select(h => new DiskInfo(
            h.Name,
            h.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == "Used Space")?.Value,
            h.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)?.Value)).ToList();

    // Throughput LibreHardwareMonitor jest w B/s; sumujemy wszystkie karty sieciowe.
    static float? NetSum(List<IHardware> all, string name)
    {
        var vals = all.Where(h => h.HardwareType == HardwareType.Network).SelectMany(h => h.Sensors)
            .Where(s => s.SensorType == SensorType.Throughput && s.Name == name && s.Value.HasValue).Select(s => s.Value!.Value).ToList();
        return vals.Count == 0 ? null : vals.Sum() / 1024f;
    }

    static void Collect(IHardware h, List<IHardware> acc)
    {
        h.Update();
        acc.Add(h);
        foreach (var s in h.SubHardware) Collect(s, acc);
    }

    static float? CpuTemp(List<IHardware> cpu)
    {
        var temps = cpu.SelectMany(h => h.Sensors)
            .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue).ToList();
        var pkg = temps.FirstOrDefault(s => s.Name.Contains("Package") || s.Name.Contains("Tctl") || s.Name.Contains("Tdie"));
        return pkg?.Value ?? (temps.Count > 0 ? temps.Max(s => s.Value) : null);
    }

    static List<FanInfo> Fans(List<IHardware> all)
    {
        var list = new List<FanInfo>();
        foreach (var h in all)
        {
            foreach (var fan in h.Sensors.Where(s => s.SensorType == SensorType.Fan))
                list.Add(new FanInfo(fan.Name, fan.Value ?? 0, FindControl(h, fan) != null));
        }
        return list;
    }

    static ISensor? FindControl(IHardware h, ISensor fan)
    {
        var idx = Regex.Match(fan.Name, @"\d+$").Value;
        return h.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Control && s.Control != null
            && idx.Length > 0 && Regex.Match(s.Name, @"\d+$").Value == idx);
    }

    /// <summary>Ustawia ręcznie obroty wentylatora (procent) albo przywraca sterowanie automatyczne (null).</summary>
    public bool SetFan(string fanName, int? percent)
    {
        var all = new List<IHardware>();
        foreach (var h in _pc.Hardware) Collect(h, all);
        foreach (var h in all)
        {
            var fan = h.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Fan && s.Name == fanName);
            var ctl = fan == null ? null : FindControl(h, fan)?.Control;
            if (ctl == null) continue;
            if (percent is int p) ctl.SetSoftware(Math.Clamp(p, 20, 100));
            else ctl.SetDefault();
            return true;
        }
        return false;
    }

    /// <summary>Ustawia wszystkie sterowalne wentylatory na 100% albo oddaje je systemowi. Zwraca liczbę zmienionych.</summary>
    public int SetAllFans(bool max)
    {
        var all = new List<IHardware>();
        foreach (var h in _pc.Hardware) Collect(h, all);
        int n = 0;
        foreach (var h in all)
            foreach (var fan in h.Sensors.Where(s => s.SensorType == SensorType.Fan))
            {
                var ctl = FindControl(h, fan)?.Control;
                if (ctl == null) continue;
                if (max) ctl.SetSoftware(100); else ctl.SetDefault();
                n++;
            }
        return n;
    }

    public void Dispose() => _pc.Close();
}
