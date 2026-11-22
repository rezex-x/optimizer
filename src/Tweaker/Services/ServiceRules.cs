using System;
using System.Collections.Generic;

namespace Tweaker.Services;

public static class ServiceRules
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "SamSs", "EventLog", "Winmgmt", "CryptSvc", "KeyIso", "VaultSvc",
        "BFE", "MpsSvc", "Dhcp", "Dnscache", "nsi", "NlaSvc", "netprofm", "Wcmsvc", "PlugPlay", "Power", "ProfSvc",
        "Schedule", "Appinfo", "UserManager", "StateRepository", "BrokerInfrastructure", "CoreMessagingRegistrar",
        "SystemEventsBroker", "TimeBrokerSvc", "gpsvc", "TrustedInstaller",
        "Audiosrv", "AudioEndpointBuilder", "Wlansvc", "TextInputManagementService", "Themes", "LanmanWorkstation",
        "EventSystem", "SENS", "DispBrokerDesktopSvc",
        "WinDefend", "WdNisSvc", "SecurityHealthService", "wscsvc", "Sense",
        "EasyAntiCheat", "EasyAntiCheat_EOS", "BEService", "vgc", "FACEIT", "FACEITService", "PnkBstrA", "PnkBstrB",
        "EAAntiCheatService",
    };

    public static bool IsProtected(ServiceSample service) => ProtectedNames.Contains(service.Name);

    public static bool IsWindows(string? imagePath, string? company, string windowsDir)
    {
        if (!string.IsNullOrWhiteSpace(company)) return company.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(imagePath)) return false;
        return imagePath.StartsWith(windowsDir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)
            && !imagePath.Contains(@"\System32\DriverStore\", StringComparison.OrdinalIgnoreCase);
    }
}
