using System;
using System.Collections.Generic;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace Tweaker.Services;

public sealed class LibreHardwareMonitorSource : IHardwareMonitor
{
    private readonly object _lock = new();
    private readonly UpdateVisitor _visitor = new();
    private Computer? _computer;
    private bool _openAttempted;
    private volatile bool _disposed;

    private Computer? Ensure()
    {
        if (_disposed) return null;
        if (_computer != null || _openAttempted) return _computer;
        _openAttempted = true;
        try
        {
            var c = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true,
            };
            c.Open();
            _computer = c;
        }
        catch { _computer = null; }
        return _computer;
    }

    public HardwareSnapshot Read()
    {
        lock (_lock)
        {
            try
            {
                Computer? c = Ensure();
                if (c is null) return HardwareSnapshot.Empty;
                c.Accept(_visitor);

                var readings = new List<SensorReading>();
                int nextId = 0;
                foreach (IHardware hw in c.Hardware) Flatten(hw, ref nextId, readings);
                return HardwareSnapshotBuilder.Build(readings);
            }
            catch { return HardwareSnapshot.Empty; }
            finally
            {
                if (_disposed) Close();
            }
        }
    }

    private static void Flatten(IHardware hw, ref int nextId, List<SensorReading> into)
    {
        int id = nextId++;
        SensorHardware kind = Map(hw.HardwareType);
        foreach (ISensor s in hw.Sensors)
        {
            double? value = s.Value is float f && float.IsFinite(f) ? f : null;
            into.Add(new SensorReading(kind, id, hw.Name, Map(s.SensorType), s.Name, s.Index, value));
        }
        foreach (IHardware sub in hw.SubHardware) Flatten(sub, ref nextId, into);
    }

    private static SensorHardware Map(HardwareType t) => t switch
    {
        HardwareType.Cpu => SensorHardware.Cpu,
        HardwareType.Memory => SensorHardware.Memory,
        HardwareType.GpuNvidia => SensorHardware.GpuNvidia,
        HardwareType.GpuAmd => SensorHardware.GpuAmd,
        HardwareType.GpuIntel => SensorHardware.GpuIntel,
        HardwareType.Storage => SensorHardware.Storage,
        HardwareType.Network => SensorHardware.Network,
        _ => SensorHardware.Other,
    };

    private static SensorKind Map(SensorType t) => t switch
    {
        SensorType.Load => SensorKind.Load,
        SensorType.Temperature => SensorKind.Temperature,
        SensorType.Clock => SensorKind.Clock,
        SensorType.Data => SensorKind.Data,
        SensorType.SmallData => SensorKind.SmallData,
        SensorType.Throughput => SensorKind.Throughput,
        _ => SensorKind.Other,
    };

    public void Dispose()
    {
        _disposed = true;
        if (!Monitor.TryEnter(_lock)) return;
        try { Close(); }
        finally { Monitor.Exit(_lock); }
    }

    private void Close()
    {
        try { _computer?.Close(); } catch { }
        _computer = null;
    }
}

internal sealed class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware sub in hardware.SubHardware) sub.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}
