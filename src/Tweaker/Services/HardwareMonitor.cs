using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public enum GpuVendor { Nvidia, Amd, Intel }

public sealed record GpuReading(string Name, GpuVendor Vendor, double? Load, double? TempC, double? HotSpotC,
    double? CoreClockMhz, double? VramUsedMb, double? VramTotalMb);

public sealed record DriveTemp(string Name, double TempC);

public sealed record DiskReading(string Name, double? ActivityPct, double? ReadBps, double? WriteBps,
    double? UsedPct, double? TempC);

public sealed record HardwareSnapshot(
    double? CpuLoad, IReadOnlyList<double> CoreLoads, double? CpuClockMhz,
    double? CpuPackageC, double? CpuHottestCoreC,
    double? MemUsedGb, double? MemTotalGb,
    double? DiskBytesPerSec,
    double? NetBytesPerSec, string? NetAdapter,
    IReadOnlyList<GpuReading> Gpus,
    IReadOnlyList<DriveTemp> DriveTemps,
    IReadOnlyList<DiskReading> Disks)
{
    public static readonly HardwareSnapshot Empty = new(
        null, Array.Empty<double>(), null, null, null, null, null, null, null, null,
        Array.Empty<GpuReading>(), Array.Empty<DriveTemp>(), Array.Empty<DiskReading>());
}

public interface IHardwareMonitor : IDisposable
{
    HardwareSnapshot Read();
}

public enum SensorHardware { Cpu, Memory, GpuNvidia, GpuAmd, GpuIntel, Storage, Network, Other }

public enum SensorKind { Load, Temperature, Clock, Data, SmallData, Throughput, Other }

public sealed record SensorReading(SensorHardware Hardware, int HardwareId, string HardwareName,
    SensorKind Kind, string Name, int Index, double? Value);
