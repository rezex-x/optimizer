using System;
using System.Collections.Generic;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeNicPropertySource : INicPropertySource
{
    public NicInfo? Next { get; set; }
    public List<string> Reads { get; } = new();
    public List<(string Adapter, string Keyword, string Value)> Sets { get; } = new();
    public string? SetResult { get; set; }

    public NicInfo? Read(string pnpDeviceId)
    {
        Reads.Add(pnpDeviceId);
        return Next;
    }

    public string? Set(string adapterName, string keyword, string value)
    {
        Sets.Add((adapterName, keyword, value));
        return SetResult;
    }
}

public sealed class FakeAdvancedBackup : IAdvancedBackup
{
    private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Originals => new Dictionary<string, string>(_map, StringComparer.OrdinalIgnoreCase);

    public bool FailRemember { get; set; }

    public bool Remember(string key, string original)
    {
        if (FailRemember) return false;
        if (!_map.ContainsKey(key)) _map[key] = original;
        return true;
    }

    public void Forget(string key) => _map.Remove(key);
}
