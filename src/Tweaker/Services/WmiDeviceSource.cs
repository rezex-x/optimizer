using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;

namespace Tweaker.Services;

public sealed class WmiDeviceSource : IDeviceSource
{
    private const string Columns = "PNPDeviceID, Name, PNPClass, Manufacturer, ConfigManagerErrorCode";

    public DeviceSnapshot Read()
    {
        try
        {
            var devices = new List<DeviceSample>();
            bool ok = Query(@"root\cimv2", "SELECT " + Columns + " FROM Win32_PnPEntity WHERE Present = TRUE", mo => Add(devices, mo));
            if (!ok)
            {
                devices.Clear();
                ok = Query(@"root\cimv2", "SELECT " + Columns + " FROM Win32_PnPEntity", mo => Add(devices, mo));
            }
            if (!ok) return DeviceSnapshot.Empty;

            var power = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Query(@"root\WMI", "SELECT InstanceName, Enable FROM MSPower_DeviceEnable", mo =>
            {
                string? name = mo["InstanceName"]?.ToString();
                if (name is not null && mo["Enable"] is bool enabled) power[DeviceRules.InstanceIdOf(name)] = enabled;
            });

            return new DeviceSnapshot(devices
                .Select(d => d with { PowerSaving = power.TryGetValue(d.InstanceId, out bool on) ? on : null })
                .ToList());
        }
        catch
        {
            return DeviceSnapshot.Empty;
        }
    }

    public string? SetPowerSaving(string instanceId, bool enabled)
    {
        try
        {
            string? result = "this device has no power-saving setting";
            bool matched = false;
            bool ok = Query(@"root\WMI", "SELECT * FROM MSPower_DeviceEnable", mo =>
            {
                if (matched) return;
                string? name = mo["InstanceName"]?.ToString();
                if (name is null || !string.Equals(DeviceRules.InstanceIdOf(name), instanceId, StringComparison.OrdinalIgnoreCase)) return;
                matched = true;
                try
                {
                    mo["Enable"] = enabled;
                    mo.Put();
                    result = null;
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                }
            });
            if (!ok && !matched) return "couldn't read the power settings";
            return result;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static void Add(List<DeviceSample> devices, ManagementObject mo)
    {
        string id = mo["PNPDeviceID"]?.ToString() ?? "";
        if (id.Length == 0) return;
        devices.Add(new DeviceSample(
            id,
            mo["Name"]?.ToString() ?? "Unknown device",
            mo["PNPClass"]?.ToString() ?? "",
            mo["Manufacturer"]?.ToString() ?? "",
            ToInt(mo["ConfigManagerErrorCode"]),
            null));
    }

    private static bool Query(string scope, string wql, Action<ManagementObject> each)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql),
                new EnumerationOptions { Timeout = TimeSpan.FromSeconds(10), ReturnImmediately = true, Rewindable = false });
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
