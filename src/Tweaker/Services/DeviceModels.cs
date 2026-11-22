using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public sealed record DeviceSample(string InstanceId, string Name, string PnpClass, string Manufacturer, int ErrorCode, bool? PowerSaving);

public sealed record DeviceSnapshot(IReadOnlyList<DeviceSample> Devices)
{
    public static readonly DeviceSnapshot Empty = new(Array.Empty<DeviceSample>());
}

public interface IDeviceSource
{
    DeviceSnapshot Read();

    string? SetPowerSaving(string instanceId, bool enabled);
}

public interface IDevicePowerBackup
{
    IReadOnlyDictionary<string, bool> Originals { get; }

    void Remember(string instanceId, bool original);

    void Forget(string instanceId);
}
