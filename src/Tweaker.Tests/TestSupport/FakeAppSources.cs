using System;
using System.Collections.Generic;
using Tweaker.Core.Catalog;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeAppxService : IAppxService
{
    public HashSet<string> Installed { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Removed { get; } = new();
    public Dictionary<string, string> FailFor { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool DetectFails { get; set; }

    public IReadOnlySet<string>? InstalledPackageNames() => DetectFails ? null : new HashSet<string>(Installed, StringComparer.OrdinalIgnoreCase);

    public int RemoveManyCalls { get; private set; }

    public IReadOnlyList<AppxRemovalResult> RemoveMany(IReadOnlyList<string> packageNames)
    {
        RemoveManyCalls++;
        var results = new List<AppxRemovalResult>();
        foreach (string packageName in packageNames)
        {
            if (FailFor.TryGetValue(packageName, out string? reason))
                results.Add(new AppxRemovalResult(packageName, AppxRemoval.Failed, reason));
            else if (!Installed.Contains(packageName))
                results.Add(new AppxRemovalResult(packageName, AppxRemoval.NotInstalled, ""));
            else
            {
                Removed.Add(packageName);
                Installed.Remove(packageName);
                results.Add(new AppxRemovalResult(packageName, AppxRemoval.Removed, ""));
            }
        }
        return results;
    }
}

public sealed class FakeInstalledAppDetector : IInstalledAppDetector
{
    public HashSet<string> Next { get; } = new(StringComparer.OrdinalIgnoreCase);
    private int _calls;
    public int Calls => _calls;
    public System.Threading.ManualResetEventSlim? Gate { get; set; }

    public IReadOnlySet<string> Detect(IReadOnlyList<InstallApp> apps)
    {
        System.Threading.Interlocked.Increment(ref _calls);
        Gate?.Wait(TimeSpan.FromSeconds(10));
        return new HashSet<string>(Next, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class FakeInstalledStore : IInstalledStore
{
    private readonly Dictionary<string, DateTime> _map = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, DateTime> Installed => new Dictionary<string, DateTime>(_map, StringComparer.OrdinalIgnoreCase);
    public void Record(string wingetId, DateTime when) { if (!_map.ContainsKey(wingetId)) _map[wingetId] = when; }
}
