using System;
using System.Linq;
using NvAPIWrapper;
using NvAPIWrapper.DRS;
using NvAPIWrapper.GPU;
using Tweaker.Core.Services;

namespace Tweaker.Services;

public sealed class NvApiWrapperSettingsService : INvidiaSettingsService
{
    private readonly bool _available;
    private readonly string? _gpuName;

    public NvApiWrapperSettingsService()
    {
        try
        {
            NVIDIA.Initialize();
            PhysicalGPU[] gpus = PhysicalGPU.GetPhysicalGPUs();
            _available = gpus.Length > 0;
            _gpuName = gpus.FirstOrDefault()?.FullName;
        }
        catch { _available = false; _gpuName = null; }
    }

    public bool IsAvailable => _available;
    public string? GpuName => _gpuName;

    public uint? ReadSetting(uint settingId)
    {
        if (!_available) return null;
        try
        {
            using var session = DriverSettingsSession.CreateAndLoad();
            var setting = session.BaseProfile.GetSetting(settingId);
            return setting is null ? (uint?)null : Convert.ToUInt32(setting.CurrentValue);
        }
        catch { return null; }
    }

    public System.Collections.Generic.IReadOnlyDictionary<uint, uint?> ReadSettings(
        System.Collections.Generic.IReadOnlyCollection<uint> settingIds)
    {
        var map = new System.Collections.Generic.Dictionary<uint, uint?>();
        if (!_available) return map;
        try
        {
            using var session = DriverSettingsSession.CreateAndLoad();
            foreach (uint id in settingIds)
            {
                try
                {
                    var setting = session.BaseProfile.GetSetting(id);
                    map[id] = setting is null ? (uint?)null : Convert.ToUInt32(setting.CurrentValue);
                }
                catch { map[id] = null; }
            }
        }
        catch { }
        return map;
    }

    public uint? ApplySetting(uint settingId, uint value)
    {
        if (!_available) throw new NvidiaSettingsException("No NVIDIA GPU available.");
        try
        {
            using var session = DriverSettingsSession.CreateAndLoad();
            var existing = session.BaseProfile.GetSetting(settingId);
            uint? prior = existing is null ? (uint?)null : Convert.ToUInt32(existing.CurrentValue);
            session.BaseProfile.SetSetting(settingId, value);
            session.Save();
            return prior;
        }
        catch (Exception ex) { throw new NvidiaSettingsException("Failed to apply NVIDIA setting.", ex); }
    }
}
