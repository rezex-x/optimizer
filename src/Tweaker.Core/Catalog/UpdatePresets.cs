using System.Text.Json;
using Tweaker.Core.Models;

namespace Tweaker.Core.Catalog;

public enum UpdatePreset { Default, SecurityOnly, DisableAll }

public static class UpdatePresets
{
    public const string PolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    public const string AuPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

    private static RegistryOp Dw(string path, string name, int value) => new()
    {
        Hive = "HKLM", Path = path, Name = name, Kind = "DWord", Value = JsonSerializer.SerializeToElement(value)
    };

    public static RegistryOp[] Ops(UpdatePreset p) => p switch
    {
        UpdatePreset.Default => new[]
        {
            Dw(AuPath, "NoAutoUpdate", 0),
            Dw(PolicyPath, "DeferFeatureUpdates", 0),
            Dw(PolicyPath, "DeferFeatureUpdatesPeriodInDays", 0),
        },
        UpdatePreset.SecurityOnly => new[]
        {
            Dw(AuPath, "NoAutoUpdate", 0),
            Dw(PolicyPath, "DeferFeatureUpdates", 1),
            Dw(PolicyPath, "DeferFeatureUpdatesPeriodInDays", 365),
            Dw(PolicyPath, "DeferQualityUpdates", 0),
        },
        UpdatePreset.DisableAll => new[]
        {
            Dw(AuPath, "NoAutoUpdate", 1),
        },
        _ => Array.Empty<RegistryOp>(),
    };

    public static string ServiceCommand(UpdatePreset p) => p switch
    {
        UpdatePreset.DisableAll => "sc stop wuauserv & sc config wuauserv start= disabled",
        _ => "sc config wuauserv start= demand",
    };
}
