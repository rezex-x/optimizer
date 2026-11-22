using System;
using System.Collections.Generic;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakePowerSettings : IPowerSettings
{
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");

    public Guid? Scheme { get; set; } = Balanced;
    public Dictionary<string, uint> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, uint> Defaults { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(Guid Scheme, Guid Subgroup, Guid Setting, uint Value)> Writes { get; } = new();
    public string? WriteResult { get; set; }

    public bool WriteLandsDespiteError { get; set; }

    public Guid? ActiveScheme() => Scheme;

    public uint? ReadAc(Guid scheme, Guid subgroup, Guid setting) =>
        Values.TryGetValue(PowerKeys.Key(scheme, subgroup, setting), out uint value) ? value : null;

    public uint? ReadAcDefault(Guid scheme, Guid subgroup, Guid setting) =>
        Defaults.TryGetValue(PowerKeys.Key(scheme, subgroup, setting), out uint value) ? value : null;

    public string? WriteAc(Guid scheme, Guid subgroup, Guid setting, uint value)
    {
        Writes.Add((scheme, subgroup, setting, value));
        if (WriteResult is null || WriteLandsDespiteError) Values[PowerKeys.Key(scheme, subgroup, setting)] = value;
        return WriteResult;
    }
}

public sealed class FakePowerBackup : IPowerBackup
{
    private readonly Dictionary<string, uint> _map = new(StringComparer.OrdinalIgnoreCase);

    public bool FailRemember { get; set; }
    public IReadOnlyDictionary<string, uint> Originals => new Dictionary<string, uint>(_map, StringComparer.OrdinalIgnoreCase);

    public bool Remember(string key, uint original)
    {
        if (FailRemember) return false;
        if (!_map.ContainsKey(key)) _map[key] = original;
        return true;
    }

    public void Forget(string key) => _map.Remove(key);
}
