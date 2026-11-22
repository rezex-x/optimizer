using System;
using System.Collections.Generic;

namespace Tweaker.Core.Services;

public sealed class NvidiaSettingsException : Exception
{
    public NvidiaSettingsException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface INvidiaSettingsService
{
    bool IsAvailable { get; }

    string? GpuName { get; }

    uint? ReadSetting(uint settingId);

    IReadOnlyDictionary<uint, uint?> ReadSettings(IReadOnlyCollection<uint> settingIds);

    uint? ApplySetting(uint settingId, uint value);
}
