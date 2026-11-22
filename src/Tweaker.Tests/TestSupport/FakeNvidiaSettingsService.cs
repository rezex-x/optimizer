using System.Collections.Generic;
using Tweaker.Core.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeNvidiaSettingsService : INvidiaSettingsService
{
    public bool IsAvailable { get; set; } = true;
    public string? GpuName { get; set; } = "NVIDIA GeForce RTX 4070";
    public Dictionary<uint, uint> Values { get; } = new();
    public List<(uint Id, uint Value)> Applied { get; } = new();
    public int ReadSettingCalls { get; private set; }
    public int ReadSettingsCalls { get; private set; }

    public uint? ReadSetting(uint settingId)
    {
        ReadSettingCalls++;
        return Values.TryGetValue(settingId, out uint v) ? v : (uint?)null;
    }

    public IReadOnlyDictionary<uint, uint?> ReadSettings(IReadOnlyCollection<uint> settingIds)
    {
        ReadSettingsCalls++;
        var map = new Dictionary<uint, uint?>();
        foreach (uint id in settingIds)
            map[id] = Values.TryGetValue(id, out uint v) ? v : (uint?)null;
        return map;
    }

    public uint? ApplySetting(uint settingId, uint value)
    {
        uint? prior = Values.TryGetValue(settingId, out uint p) ? p : (uint?)null;
        Values[settingId] = value;
        Applied.Add((settingId, value));
        return prior;
    }
}
