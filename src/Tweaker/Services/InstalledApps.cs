using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using Tweaker.Core.Catalog;
using Tweaker.Core.Services;

namespace Tweaker.Services;

public interface IInstalledAppDetector
{
    IReadOnlySet<string> Detect(IReadOnlyList<InstallApp> apps);
}

public sealed class InstalledAppDetector : IInstalledAppDetector
{
    private static readonly string[] UninstallPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    };

    private readonly IProcessRunner _runner;

    public InstalledAppDetector(IProcessRunner runner) => _runner = runner;

    public IReadOnlySet<string> Detect(IReadOnlyList<InstallApp> apps)
    {
        try
        {
            return InstalledMatcher.Match(apps, WingetList(), UninstallNames(), ProcessNames());
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    private string WingetList()
    {
        string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tweaker-winget-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            _runner.Run("winget", "export -o \"" + file + "\" --accept-source-agreements --disable-interactivity");
            if (System.IO.File.Exists(file))
            {
                string ids = ExportedIds(System.IO.File.ReadAllText(file));
                if (ids.Length > 0) return ids;
            }
        }
        catch { }
        finally
        {
            try { System.IO.File.Delete(file); } catch { }
        }
        try { return _runner.Run("winget", "list --accept-source-agreements --disable-interactivity").StdOut; }
        catch { return ""; }
    }

    public static string ExportedIds(string exportJson) =>
        string.Join("\n", System.Text.RegularExpressions.Regex.Matches(exportJson ?? "", "\"PackageIdentifier\"\\s*:\\s*\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value));

    private static IEnumerable<string> UninstallNames()
    {
        var names = new List<string>();
        foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (string path in UninstallPaths)
            {
                try
                {
                    using RegistryKey? key = root.OpenSubKey(path);
                    if (key is null) continue;
                    foreach (string sub in key.GetSubKeyNames())
                    {
                        try
                        {
                            using RegistryKey? app = key.OpenSubKey(sub);
                            if (app?.GetValue("DisplayName") is string name) names.Add(name);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        return names;
    }

    private static IEnumerable<string> ProcessNames()
    {
        var names = new List<string>();
        try
        {
            foreach (Process p in Process.GetProcesses())
                using (p) names.Add(p.ProcessName);
        }
        catch { }
        return names;
    }
}

public interface IInstalledStore
{
    IReadOnlyDictionary<string, DateTime> Installed { get; }
    void Record(string wingetId, DateTime when);
}

public sealed class JsonInstalledStore : IInstalledStore
{
    private readonly JsonOriginals<DateTime> _store;

    public JsonInstalledStore(string path) => _store = new JsonOriginals<DateTime>(path);

    public IReadOnlyDictionary<string, DateTime> Installed => _store.Snapshot();
    public void Record(string wingetId, DateTime when) => _store.Remember(wingetId, when);
}
