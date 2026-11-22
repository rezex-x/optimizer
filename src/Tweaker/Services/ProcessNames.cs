using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tweaker.Services;

public static class ProcessNames
{
    private static readonly HashSet<string> NoImage = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Secure System", "Memory Compression",
    };

    public static string Display(int pid, string? processName)
    {
        if (pid == 0) return "System Idle Process";
        if (pid == 4) return "System";
        if (string.IsNullOrWhiteSpace(processName)) return "PID " + pid.ToString(CultureInfo.InvariantCulture);
        return NoImage.Contains(processName) ? processName : processName + ".exe";
    }
}
