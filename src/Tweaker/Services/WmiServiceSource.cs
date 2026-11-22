using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Threading;
using Tweaker.Core.Services;

namespace Tweaker.Services;

public sealed class WmiServiceSource : IServiceSource
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    private const string DelayedQuery =
        "SELECT Name, DisplayName, Description, State, StartMode, DelayedAutoStart, ProcessId, PathName, AcceptStop FROM Win32_Service";
    private const string FallbackQuery =
        "SELECT Name, DisplayName, Description, State, StartMode, ProcessId, PathName, AcceptStop FROM Win32_Service";

    private readonly IProcessRunner _runner;
    private readonly string _windowsDir;
    private readonly ConcurrentDictionary<string, string?> _companies = new(StringComparer.OrdinalIgnoreCase);
    private bool? _hasDelayed;

    public WmiServiceSource(IProcessRunner runner, string windowsDir)
    {
        _runner = runner;
        _windowsDir = windowsDir;
    }

    public ServiceSnapshot Read()
    {
        try
        {
            var services = new List<ServiceSample>();
            if (_hasDelayed == false) return RunFallback(services, setCacheOnSuccess: false);

            bool ok = Query(DelayedQuery, mo => services.Add(Sample(mo, hasDelayed: true)));
            if (ok)
            {
                _hasDelayed = true;
                return new ServiceSnapshot(services);
            }

            if (_hasDelayed is null)
            {
                services.Clear();
                return RunFallback(services, setCacheOnSuccess: true);
            }

            return ServiceSnapshot.Empty;
        }
        catch
        {
            return ServiceSnapshot.Empty;
        }
    }

    private ServiceSnapshot RunFallback(List<ServiceSample> services, bool setCacheOnSuccess)
    {
        bool ok = Query(FallbackQuery, mo => services.Add(Sample(mo, hasDelayed: false)));
        if (ok && setCacheOnSuccess) _hasDelayed = false;
        return ok ? new ServiceSnapshot(services) : ServiceSnapshot.Empty;
    }

    public string? Start(string name) => Control(name, "start", ServiceState.Running);

    public string? Stop(string name) => Control(name, "stop", ServiceState.Stopped);

    private ServiceSample Sample(ManagementObject mo, bool hasDelayed)
    {
        string name = mo["Name"]?.ToString() ?? "";
        string? image = ServiceParse.ImagePath(mo["PathName"]?.ToString(), _windowsDir);
        bool delayed = hasDelayed && mo["DelayedAutoStart"] is bool d && d;
        return new ServiceSample(
            name,
            mo["DisplayName"]?.ToString() is { Length: > 0 } display ? display : name,
            mo["Description"]?.ToString() ?? "",
            ServiceParse.State(mo["State"]?.ToString()),
            ServiceParse.Startup(mo["StartMode"]?.ToString(), delayed),
            ToInt(mo["ProcessId"]),
            ServiceRules.IsWindows(image, Company(image), _windowsDir),
            mo["AcceptStop"] is bool accepts && accepts);
    }

    private string? Company(string? image)
    {
        if (string.IsNullOrEmpty(image)) return null;
        if (_companies.TryGetValue(image, out string? cached)) return cached;
        string? company = null;
        try
        {
            if (File.Exists(image)) company = FileVersionInfo.GetVersionInfo(image).CompanyName;
        }
        catch { }
        _companies[image] = company;
        return company;
    }

    private string? Control(string name, string verb, ServiceState target)
    {
        try
        {
            if (name.Length == 0 || name.Contains('"')) return "invalid service name";
            string scExe = Path.Combine(Environment.SystemDirectory, "sc.exe");
            ProcessResult result = _runner.Run(scExe, verb + " \"" + name + "\"");
            if ((verb == "start" && result.ExitCode == 1056) || (verb == "stop" && result.ExitCode == 1062)) return null;
            string? error = ServiceParse.ScError(result);
            if (error is not null) return error;

            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < Wait)
            {
                if (StateOf(name) == target) return null;
                Thread.Sleep(500);
            }
            return verb == "start" ? "it didn't start within 15 s" : "it didn't stop within 15 s";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static ServiceState StateOf(string name)
    {
        ServiceState state = ServiceState.Unknown;
        string escaped = name.Replace("\\", "\\\\").Replace("'", "\\'");
        Query("SELECT State FROM Win32_Service WHERE Name='" + escaped + "'", mo => state = ServiceParse.State(mo["State"]?.ToString()));
        return state;
    }

    private static bool Query(string wql, Action<ManagementObject> each)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\cimv2"), new ObjectQuery(wql),
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(10), ReturnImmediately = true, Rewindable = false });
            using ManagementObjectCollection results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo) each(mo);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int ToInt(object? value)
    {
        try { return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }
}
