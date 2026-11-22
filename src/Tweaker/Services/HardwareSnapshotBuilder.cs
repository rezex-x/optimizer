using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tweaker.Services;

public static class HardwareSnapshotBuilder
{
    private static readonly Regex PerCore = new(@"^(CPU Core|P-Core|E-Core|Core) #\d+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PerCoreLoad = new(@"^(CPU Core|P-Core|E-Core|Core) #\d+( Thread #\d+)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static HardwareSnapshot Build(IReadOnlyList<SensorReading> sensors)
    {
        List<SensorReading> cpu = sensors.Where(s => s.Hardware == SensorHardware.Cpu).ToList();

        List<double> coreLoads = cpu
            .Where(s => s.Kind == SensorKind.Load && PerCoreLoad.IsMatch(s.Name) && s.Value is not null)
            .OrderBy(s => s.HardwareId).ThenBy(s => s.Index)
            .Select(s => s.Value!.Value)
            .ToList();

        double? cpuLoad = Exact(cpu, SensorKind.Load, "CPU Total")
            ?? (coreLoads.Count > 0 ? coreLoads.Average() : (double?)null);

        double? cpuClock = Average(cpu.Where(s => s.Kind == SensorKind.Clock && PerCore.IsMatch(s.Name)));

        double? package = Exact(cpu, SensorKind.Temperature, "CPU Package")
            ?? Containing(cpu, SensorKind.Temperature, "Tctl")
            ?? Containing(cpu, SensorKind.Temperature, "Tdie")
            ?? First(cpu, SensorKind.Temperature);

        double? hottest = Exact(cpu, SensorKind.Temperature, "Core Max")
            ?? Max(cpu.Where(s => s.Kind == SensorKind.Temperature && PerCore.IsMatch(s.Name)));

        List<List<SensorReading>> memoryItems = Items(sensors, SensorHardware.Memory)
            .Where(g => Exact(g, SensorKind.Data, "Memory Used") is not null)
            .ToList();
        List<SensorReading> memory =
            memoryItems.FirstOrDefault(g => !g[0].HardwareName.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
            ?? memoryItems.FirstOrDefault()
            ?? new List<SensorReading>();
        double? memUsed = Exact(memory, SensorKind.Data, "Memory Used");
        double? memAvailable = Exact(memory, SensorKind.Data, "Memory Available");
        double? memTotal = memUsed is not null && memAvailable is not null ? memUsed + memAvailable : null;

        var gpus = new List<GpuReading>();
        foreach (List<SensorReading> g in Items(sensors, SensorHardware.GpuNvidia, SensorHardware.GpuAmd, SensorHardware.GpuIntel))
        {
            GpuVendor vendor = g[0].Hardware switch
            {
                SensorHardware.GpuNvidia => GpuVendor.Nvidia,
                SensorHardware.GpuAmd => GpuVendor.Amd,
                _ => GpuVendor.Intel,
            };
            gpus.Add(new GpuReading(g[0].HardwareName, vendor,
                Exact(g, SensorKind.Load, "GPU Core") ?? First(g, SensorKind.Load),
                Exact(g, SensorKind.Temperature, "GPU Core") ?? First(g, SensorKind.Temperature),
                Containing(g, SensorKind.Temperature, "Hot Spot") ?? Containing(g, SensorKind.Temperature, "Hotspot"),
                Exact(g, SensorKind.Clock, "GPU Core") ?? First(g, SensorKind.Clock),
                Exact(g, SensorKind.SmallData, "GPU Memory Used"),
                Exact(g, SensorKind.SmallData, "GPU Memory Total")));
        }

        double? disk = null;
        var driveTemps = new List<DriveTemp>();
        var disks = new List<DiskReading>();
        foreach (List<SensorReading> d in Items(sensors, SensorHardware.Storage))
        {
            double? read = Exact(d, SensorKind.Throughput, "Read Rate");
            double? write = Exact(d, SensorKind.Throughput, "Write Rate");
            if (read is not null || write is not null) disk = (disk ?? 0) + (read ?? 0) + (write ?? 0);

            double? temp =
                FirstPositive(d, SensorKind.Temperature, n => n.Equals("Composite Temperature", StringComparison.OrdinalIgnoreCase))
                ?? FirstPositive(d, SensorKind.Temperature, n => n.Equals("Temperature", StringComparison.OrdinalIgnoreCase))
                ?? FirstPositive(d, SensorKind.Temperature, n => n.StartsWith("Temperature #", StringComparison.OrdinalIgnoreCase));
            if (temp is double t) driveTemps.Add(new DriveTemp(d[0].HardwareName, t));

            double? activity = Exact(d, SensorKind.Load, "Total Activity");
            double? usedSpace = Exact(d, SensorKind.Load, "Used Space");
            disks.Add(new DiskReading(d[0].HardwareName, activity, read, write, usedSpace, temp));
        }

        string? netAdapter = null;
        double? netBytes = null;
        double bestMoved = -1;
        bool bestIsVirtual = false;
        foreach (List<SensorReading> n in Items(sensors, SensorHardware.Network))
        {
            double? down = Exact(n, SensorKind.Throughput, "Download Speed");
            double? up = Exact(n, SensorKind.Throughput, "Upload Speed");
            if (down is null && up is null) continue;

            double total = (down ?? 0) + (up ?? 0);
            double moved = (Exact(n, SensorKind.Data, "Data Downloaded") ?? 0) + (Exact(n, SensorKind.Data, "Data Uploaded") ?? 0);
            bool isVirtual = IsVirtualAdapter(n[0].HardwareName);
            bool better = netBytes is null
                || total > netBytes
                || (total == netBytes && (moved > bestMoved || (moved == bestMoved && bestIsVirtual && !isVirtual)));
            if (better)
            {
                netBytes = total;
                netAdapter = n[0].HardwareName;
                bestMoved = moved;
                bestIsVirtual = isVirtual;
            }
        }

        return new HardwareSnapshot(cpuLoad, coreLoads, cpuClock, package, hottest, memUsed, memTotal,
            disk, netBytes, netAdapter, gpus, driveTemps, disks);
    }

    private static IEnumerable<List<SensorReading>> Items(IReadOnlyList<SensorReading> sensors, params SensorHardware[] kinds) =>
        sensors.Where(s => kinds.Contains(s.Hardware)).GroupBy(s => s.HardwareId).Select(g => g.ToList());

    private static bool IsVirtualAdapter(string name) =>
        name.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Local Area Connection*", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)
        || name.Contains("VMware", StringComparison.OrdinalIgnoreCase)
        || name.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase);

    private static double? Exact(IEnumerable<SensorReading> s, SensorKind kind, string name) =>
        s.FirstOrDefault(x => x.Kind == kind && x.Value is not null
            && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static double? Containing(IEnumerable<SensorReading> s, SensorKind kind, string part) =>
        s.FirstOrDefault(x => x.Kind == kind && x.Value is not null
            && x.Name.Contains(part, StringComparison.OrdinalIgnoreCase))?.Value;

    private static double? First(IEnumerable<SensorReading> s, SensorKind kind) =>
        s.FirstOrDefault(x => x.Kind == kind && x.Value is not null)?.Value;

    private static double? FirstPositive(IEnumerable<SensorReading> s, SensorKind kind, Func<string, bool> name) =>
        s.FirstOrDefault(x => x.Kind == kind && x.Value is > 0 && name(x.Name))?.Value;

    private static double? Average(IEnumerable<SensorReading> s)
    {
        List<double> v = s.Where(x => x.Value is not null).Select(x => x.Value!.Value).ToList();
        return v.Count == 0 ? null : v.Average();
    }

    private static double? Max(IEnumerable<SensorReading> s)
    {
        List<double> v = s.Where(x => x.Value is not null).Select(x => x.Value!.Value).ToList();
        return v.Count == 0 ? null : v.Max();
    }
}
