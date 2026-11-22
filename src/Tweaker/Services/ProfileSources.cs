using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Tweaker.Services;

public sealed class WindowsProcessCloser : IProcessCloser
{
    public string? CloseWindow(int pid, long createdTicks)
    {
        try
        {
            using Process p = Process.GetProcessById(pid);
            if (Math.Abs(p.StartTime.ToFileTimeUtc() - createdTicks) > TimeSpan.TicksPerSecond) return "it has already exited";
            return p.CloseMainWindow() ? null : "it didn't accept the close request";
        }
        catch (ArgumentException)
        {
            return "it has already exited";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

public sealed class RunningExeSource : IRunningExeSource
{
    public IReadOnlySet<string>? Read()
    {
        Process[] all;
        try { all = Process.GetProcesses(); }
        catch { return null; }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Process p in all)
        {
            try { names.Add(p.ProcessName + ".exe"); }
            catch { }
            finally { p.Dispose(); }
        }
        return names;
    }
}

public sealed class FileCompanyLookup
{
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string? CompanyOf(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;
        return _cache.GetOrAdd(imagePath, path =>
        {
            try { return FileVersionInfo.GetVersionInfo(path).CompanyName; }
            catch { return null; }
        });
    }
}
