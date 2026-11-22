using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace Tweaker.Services;

public sealed class WindowsProcessSource : IProcessSource
{
    private const uint ProcessQueryLimitedInformation = 0x1000, ProcessTerminate = 0x0001, TokenQuery = 0x0008, GwOwner = 4;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<(int, long), string> _owners = new();
    private readonly SecurityIdentifier? _currentUser = CurrentUser();

    public ProcessSnapshot Read()
    {
        try
        {
            HashSet<int> windowed = WindowOwners();
            var samples = new List<ProcessSample>();
            foreach (Process p in Process.GetProcesses())
            {
                using (p)
                {
                    try { samples.Add(Sample(p, windowed)); }
                    catch { }
                }
            }

            var alive = new HashSet<(int, long)>(samples.Select(s => (s.Pid, s.CreatedTicks)));
            foreach ((int, long) gone in _owners.Keys.Where(k => !alive.Contains(k)).ToList()) _owners.Remove(gone);

            return new ProcessSnapshot(_clock.Elapsed, samples, TotalMemory(), Environment.ProcessorCount);
        }
        catch
        {
            return ProcessSnapshot.Empty;
        }
    }

    public string? Kill(int pid, long createdTicks)
    {
        try
        {
            IntPtr handle = OpenProcess(ProcessTerminate | ProcessQueryLimitedInformation, false, pid);
            if (handle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                return error == 5 ? "Windows denied access" : "it has already exited";
            }
            try
            {
                if (!GetProcessTimes(handle, out long created, out _, out _, out _) || created != createdTicks)
                    return "it has already exited";
                return TerminateProcess(handle, 1)
                    ? null
                    : "Windows refused (error " + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + ")";
            }
            finally
            {
                CloseHandle(handle);
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private ProcessSample Sample(Process p, HashSet<int> windowed)
    {
        int pid = p.Id;
        string name = ProcessNames.Display(pid, p.ProcessName);
        long workingSet = 0;
        try { workingSet = p.WorkingSet64; } catch { }

        long created = 0;
        TimeSpan? cpu = null;
        long? io = null;
        string? path = null;
        bool critical = false;
        string owner = "—";

        IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle != IntPtr.Zero)
        {
            try
            {
                if (GetProcessTimes(handle, out long creation, out _, out long kernel, out long user))
                {
                    created = creation;
                    cpu = TimeSpan.FromTicks(kernel + user);
                }
                if (GetProcessIoCounters(handle, out IoCounters counters))
                    io = (long)(counters.ReadTransferCount + counters.WriteTransferCount);
                path = ImagePath(handle);
                if (IsProcessCritical(handle, out bool isCritical)) critical = isCritical;
                owner = Owner(handle, pid, created);
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        return new ProcessSample(pid, created, name, path, cpu, workingSet, io, owner, windowed.Contains(pid), critical);
    }

    private string Owner(IntPtr process, int pid, long created)
    {
        if (_owners.TryGetValue((pid, created), out string? cached)) return cached;
        string owner = "—";
        if (OpenProcessToken(process, TokenQuery, out IntPtr token))
        {
            try
            {
                using var identity = new WindowsIdentity(token);
                owner = OwnerNames.Display(identity.Name, _currentUser is not null && identity.User is not null && _currentUser.Equals(identity.User));
            }
            catch { }
            finally
            {
                CloseHandle(token);
            }
        }
        _owners[(pid, created)] = owner;
        return owner;
    }

    private static SecurityIdentifier? CurrentUser()
    {
        try
        {
            using WindowsIdentity me = WindowsIdentity.GetCurrent();
            return me.User;
        }
        catch { return null; }
    }

    private static string? ImagePath(IntPtr process)
    {
        var buffer = new StringBuilder(1024);
        int size = buffer.Capacity;
        return QueryFullProcessImageNameW(process, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
    }

    private static HashSet<int> WindowOwners()
    {
        var pids = new HashSet<int>();
        try
        {
            EnumWindows((window, _) =>
            {
                if (IsWindowVisible(window) && GetWindowTextLengthW(window) > 0 && GetWindow(window, GwOwner) == IntPtr.Zero)
                {
                    GetWindowThreadProcessId(window, out uint pid);
                    pids.Add((int)pid);
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        return pids;
    }

    private static long TotalMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? (long)status.TotalPhys : 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessCritical(IntPtr process, [MarshalAs(UnmanagedType.Bool)] out bool critical);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
}
