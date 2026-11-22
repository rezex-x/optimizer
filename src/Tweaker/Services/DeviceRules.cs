using System;
using System.Collections.Generic;
using System.Linq;

namespace Tweaker.Services;

public static class DeviceRules
{
    public static readonly IReadOnlyList<string> GroupOrder = new[]
    {
        "Display", "Processor", "USB", "Input", "Network", "Audio", "Bluetooth", "Storage", "System", "Other",
    };

    private static readonly Dictionary<string, string> Groups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Display"] = "Display",
        ["Processor"] = "Processor",
        ["USB"] = "USB",
        ["USBDevice"] = "USB",
        ["HIDClass"] = "Input",
        ["Keyboard"] = "Input",
        ["Mouse"] = "Input",
        ["XnaComposite"] = "Input",
        ["Net"] = "Network",
        ["MEDIA"] = "Audio",
        ["Bluetooth"] = "Bluetooth",
        ["SCSIAdapter"] = "Storage",
        ["HDC"] = "Storage",
        ["System"] = "System",
    };

    private static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "SoftwareDevice", "SoftwareComponent", "Volume", "VolumeSnapshot", "PrintQueue", "AudioEndpoint", "DiskDrive",
    };

    public static string? Group(string? pnpClass, int errorCode)
    {
        string c = pnpClass?.Trim() ?? "";
        if (Groups.TryGetValue(c, out string? group)) return group;
        if (Hidden.Contains(c))
        {
            if (!IsProblem(errorCode)) return null;
            if (string.Equals(c, "DiskDrive", StringComparison.OrdinalIgnoreCase)) return "Storage";
            if (string.Equals(c, "AudioEndpoint", StringComparison.OrdinalIgnoreCase)) return "Audio";
        }
        return "Other";
    }

    public static bool IsProblem(int errorCode) => errorCode != 0 && errorCode != 22;
    public static bool IsDisabled(int errorCode) => errorCode == 22;

    public static string ProblemText(int errorCode) => errorCode switch
    {
        1 => "Not configured correctly",
        3 => "Driver may be corrupted",
        10 => "Device can't start",
        12 => "Not enough free resources",
        14 => "Restart needed",
        18 => "Reinstall the drivers",
        19 => "Registry problem",
        21 => "Being removed",
        22 => "Disabled",
        24 => "Not present or not working",
        28 => "Drivers not installed",
        29 => "Disabled by firmware",
        31 => "Not working properly",
        32 => "Driver disabled",
        37 => "Driver failed to start",
        39 => "Driver missing or corrupted",
        43 => "Windows has stopped this device",
        45 => "Not connected",
        47 => "Waiting to be safely removed",
        48 => "Driver blocked",
        52 => "Driver not signed",
        _ => "Problem",
    };

    public static bool IsUsbOrInput(string group) => group is "USB" or "Input";

    public static string InstanceIdOf(string powerInstanceName)
    {
        int underscore = powerInstanceName.LastIndexOf('_');
        if (underscore > 0 && underscore < powerInstanceName.Length - 1
            && powerInstanceName[(underscore + 1)..].All(char.IsDigit))
            return powerInstanceName[..underscore];
        return powerInstanceName;
    }
}
