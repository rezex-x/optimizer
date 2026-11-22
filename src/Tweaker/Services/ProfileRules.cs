using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public static class ProfileRules
{
    private static readonly string[] KeptVendors =
    {
        "NVIDIA", "Advanced Micro Devices", "AMD", "Intel", "Realtek", "Logitech", "Razer", "Corsair", "SteelSeries",
        "ASUSTeK", "ASUS", "Micro-Star", "MSI", "GIGA-BYTE", "Gigabyte", "Elgato", "Wacom", "Synaptics", "ELAN",
        "Dolby", "Nahimic", "A-Volute", "Waves",
        "Avast", "AVG", "Avira", "Bitdefender", "ESET", "F-Secure", "G DATA", "Kaspersky", "Malwarebytes", "McAfee",
        "NortonLifeLock", "Norton", "Gen Digital", "Symantec", "Sophos", "Trend Micro", "Webroot", "Emsisoft",
        "Panda Security", "Check Point", "CrowdStrike", "SentinelOne",
        "VB-Audio", "Creative Technology", "Creative Labs", "Turtle Beach", "Cooler Master", "NZXT", "Tobii", "HyperX",
    };

    private static readonly HashSet<string> AllowlistExempt = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam.exe", "steamwebhelper.exe", "steamservice.exe", "GameOverlayUI.exe",
        "EpicGamesLauncher.exe", "EpicWebHelper.exe", "EOSOverlayRenderer-Win64-Shipping.exe",
        "RiotClientServices.exe", "Riot Client.exe", "RiotClientUx.exe", "RiotClientUxRender.exe",
        "Battle.net.exe", "Agent.exe", "EADesktop.exe", "EABackgroundService.exe",
        "upc.exe", "UbisoftConnect.exe", "UplayWebCore.exe", "GalaxyClient.exe", "GalaxyClient Helper.exe",
        "RockstarService.exe", "SocialClubHelper.exe", "XboxPcApp.exe",
        "GameBar.exe", "GameBarFTServer.exe", "gamingservices.exe", "gamingservicesnet.exe", "msedgewebview2.exe",
        "TabTip.exe", "Widgets.exe", "WidgetService.exe", "PhoneExperienceHost.exe", "CrossDeviceService.exe",
    };

    public static string NormalizeExe(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        string file = name.Trim().Trim('"').Trim();
        int slash = file.LastIndexOfAny(new[] { '\\', '/' });
        if (slash >= 0) file = file[(slash + 1)..];
        if (file.Length == 0) return "";
        return file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file : file + ".exe";
    }

    public static bool SameExe(string? a, string? b)
    {
        string x = NormalizeExe(a);
        return x.Length > 0 && string.Equals(x, NormalizeExe(b), StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesService(string entry, ServiceSample service)
    {
        string e = entry.Trim();
        return e.Length > 0
            && (string.Equals(e, service.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, service.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAllowlistExempt(string exeName) => AllowlistExempt.Contains(exeName);

    public static bool IsKeptVendor(string? company)
    {
        if (string.IsNullOrWhiteSpace(company)) return false;
        string c = company.Trim();
        foreach (string vendor in KeptVendors)
        {
            if (c.StartsWith(vendor, StringComparison.OrdinalIgnoreCase)
                && (c.Length == vendor.Length || !char.IsLetter(c[vendor.Length])))
                return true;
        }
        return false;
    }
}
