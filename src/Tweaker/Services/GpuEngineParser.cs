using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tweaker.Services;

public static class GpuEngineParser
{
    public static int? Pid(string instance)
    {
        const string prefix = "pid_";
        int at = instance.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;
        int start = at + prefix.Length, end = start;
        while (end < instance.Length && char.IsAsciiDigit(instance[end])) end++;
        return end > start && int.TryParse(instance.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
            ? pid
            : null;
    }

    public static Dictionary<int, double> PerProcess(IEnumerable<(string Instance, double Utilization)> engines)
    {
        var result = new Dictionary<int, double>();
        foreach ((string instance, double value) in engines)
        {
            if (Pid(instance) is not int pid || !double.IsFinite(value)) continue;
            double v = Math.Clamp(value, 0, 100);
            result[pid] = result.TryGetValue(pid, out double current) ? Math.Max(current, v) : v;
        }
        return result;
    }
}
