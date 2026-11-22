using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;

namespace Tweaker.Services;

public sealed class WmiStorageDiagnostic : IStorageDiagnostic
{
    private const double BytesPerGib = 1024.0 * 1024.0 * 1024.0;

    public StorageResult Read() => new(ReadDrives(), ReadVolumes());

    private static IReadOnlyList<DriveHealth> ReadDrives()
    {
        var list = new List<DriveHealth>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            var query = new ObjectQuery("SELECT FriendlyName, Size, MediaType, SpindleSpeed, HealthStatus FROM MSFT_PhysicalDisk");
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    string model = mo["FriendlyName"]?.ToString()?.Trim() ?? "Disk";
                    double? cap = ToGb(mo["Size"]);
                    DriveKind kind = MapKind(mo["MediaType"], mo["SpindleSpeed"]);
                    SmartHealth smart = MapHealth(mo["HealthStatus"]);
                    list.Add(new DriveHealth(model, kind, smart, cap));
                }
            }
        }
        catch { }
        return list;
    }

    private static IReadOnlyList<VolumeUsage> ReadVolumes()
    {
        var list = new List<VolumeUsage>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    string letter = mo["DeviceID"]?.ToString() ?? "";
                    double? free = ToGb(mo["FreeSpace"]);
                    double? size = ToGb(mo["Size"]);
                    list.Add(new VolumeUsage(letter, free, size));
                }
            }
        }
        catch { }
        return list;
    }

    private static DriveKind MapKind(object? mediaType, object? spindle)
    {
        try
        {
            if (mediaType != null)
            {
                int mt = Convert.ToInt32(mediaType, CultureInfo.InvariantCulture);
                if (mt == 4) return DriveKind.Ssd;
                if (mt == 3) return DriveKind.Hdd;
            }
            if (spindle != null)
            {
                uint sp = Convert.ToUInt32(spindle, CultureInfo.InvariantCulture);
                if (sp == 0) return DriveKind.Ssd;
                if (sp != uint.MaxValue) return DriveKind.Hdd;
            }
        }
        catch { }
        return DriveKind.Unknown;
    }

    private static SmartHealth MapHealth(object? health)
    {
        try
        {
            if (health == null) return SmartHealth.Unknown;
            int h = Convert.ToInt32(health, CultureInfo.InvariantCulture);
            return h switch
            {
                0 => SmartHealth.Ok,
                1 => SmartHealth.PredictedFail,
                2 => SmartHealth.PredictedFail,
                _ => SmartHealth.Unknown,
            };
        }
        catch { return SmartHealth.Unknown; }
    }

    private static double? ToGb(object? bytes)
    {
        try { return bytes is null ? null : Convert.ToDouble(bytes, CultureInfo.InvariantCulture) / BytesPerGib; }
        catch { return null; }
    }
}
