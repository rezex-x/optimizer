using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public readonly record struct ProcessRate(double? CpuPercent, double? IoBytesPerSec);

public static class ProcessRates
{
    public static IReadOnlyDictionary<(int Pid, long Created), ProcessRate> Compute(ProcessSnapshot previous, ProcessSnapshot current)
    {
        var rates = new Dictionary<(int Pid, long Created), ProcessRate>();
        double seconds = (current.At - previous.At).TotalSeconds;
        if (seconds <= 0) return rates;
        int cores = Math.Max(1, current.ProcessorCount);

        var before = new Dictionary<(int, long), ProcessSample>();
        foreach (ProcessSample p in previous.Processes) before.TryAdd((p.Pid, p.CreatedTicks), p);

        foreach (ProcessSample p in current.Processes)
        {
            if (!before.TryGetValue((p.Pid, p.CreatedTicks), out ProcessSample? b)) continue;
            double? cpu = p.CpuTime is TimeSpan now && b.CpuTime is TimeSpan then
                ? Math.Clamp((now - then).TotalSeconds / seconds / cores * 100, 0, 100)
                : null;
            double? io = p.IoBytes is long ioNow && b.IoBytes is long ioThen ? Math.Max(0, ioNow - ioThen) / seconds : null;
            rates[(p.Pid, p.CreatedTicks)] = new ProcessRate(cpu, io);
        }
        return rates;
    }
}
