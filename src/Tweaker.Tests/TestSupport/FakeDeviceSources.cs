using System;
using System.Collections.Generic;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeDeviceSource : IDeviceSource
{
    public DeviceSnapshot Next { get; set; } = DeviceSnapshot.Empty;
    public int Reads { get; private set; }
    public List<(string Id, bool Enabled)> Sets { get; } = new();
    public string? SetResult { get; set; }

    public HashSet<string> FailIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DeviceSnapshot Read()
    {
        Reads++;
        return Next;
    }

    public string? SetPowerSaving(string instanceId, bool enabled)
    {
        Sets.Add((instanceId, enabled));
        return FailIds.Contains(instanceId) ? "Access is denied." : SetResult;
    }
}

public sealed class FakeDevicePowerBackup : IDevicePowerBackup
{
    private readonly Dictionary<string, bool> _map = new(StringComparer.OrdinalIgnoreCase);

    public int Remembers { get; private set; }
    public IReadOnlyDictionary<string, bool> Originals => new Dictionary<string, bool>(_map, StringComparer.OrdinalIgnoreCase);

    public void Remember(string instanceId, bool original)
    {
        Remembers++;
        if (!_map.ContainsKey(instanceId)) _map[instanceId] = original;
    }

    public void Forget(string instanceId) => _map.Remove(instanceId);
}
