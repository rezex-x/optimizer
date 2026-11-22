using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Tweaker.Core.Services;

namespace Tweaker.Services;

public static class ServiceParse
{
    public static ServiceState State(string? state) => state switch
    {
        "Running" => ServiceState.Running,
        "Stopped" => ServiceState.Stopped,
        "Start Pending" => ServiceState.StartPending,
        "Stop Pending" => ServiceState.StopPending,
        "Paused" or "Pause Pending" or "Continue Pending" => ServiceState.Paused,
        _ => ServiceState.Unknown,
    };

    public static ServiceStartup Startup(string? startMode, bool delayed) => startMode switch
    {
        "Auto" => delayed ? ServiceStartup.AutomaticDelayed : ServiceStartup.Automatic,
        "Manual" => ServiceStartup.Manual,
        "Disabled" => ServiceStartup.Disabled,
        "Boot" => ServiceStartup.Boot,
        "System" => ServiceStartup.System,
        _ => ServiceStartup.Unknown,
    };

    public static string? ImagePath(string? pathName, string windowsDir)
    {
        if (string.IsNullOrWhiteSpace(pathName)) return null;
        string path = pathName.Trim();
        string exe;
        if (path.StartsWith('"'))
        {
            int end = path.IndexOf('"', 1);
            exe = end > 1 ? path[1..end] : path.Trim('"');
        }
        else
        {
            int dotExe = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            exe = dotExe >= 0 ? path[..(dotExe + 4)] : path.Split(' ')[0];
        }

        string windows = windowsDir.TrimEnd('\\');
        if (exe.StartsWith(@"\??\", StringComparison.Ordinal)) exe = exe[4..];
        if (exe.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase)) exe = windows + exe[@"\SystemRoot".Length..];
        return Regex.Replace(exe, "%SystemRoot%|%windir%", windows.Replace("$", "$$"), RegexOptions.IgnoreCase);
    }

    public static string? ScError(ProcessResult result)
    {
        if (result.ExitCode == 0) return null;
        string message = new System.ComponentModel.Win32Exception(result.ExitCode).Message;
        if (!message.StartsWith("Unknown error", StringComparison.OrdinalIgnoreCase)) return message;

        string[] lines = (result.StdOut + "\n" + result.StdErr)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("FAILED", StringComparison.OrdinalIgnoreCase))
                return i + 1 < lines.Length ? lines[i + 1] : lines[i];
        }
        return "sc.exe exited with code " + result.ExitCode.ToString(CultureInfo.InvariantCulture);
    }
}
