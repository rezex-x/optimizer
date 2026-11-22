namespace Tweaker.Core.Catalog;

public enum SafetyLevel { Safe, Caution, Keep }

public sealed record ServiceAdvice(SafetyLevel Level, string Reason);

public static class ServiceSafety
{
    private static readonly Dictionary<string, ServiceAdvice> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Spooler"] = Safe("Only needed for printing."),
        ["Fax"] = Safe("Only needed for faxing."),
        ["DiagTrack"] = Safe("Telemetry: sends usage data to Microsoft."),
        ["dmwappushservice"] = Safe("Routes telemetry messages."),
        ["MapsBroker"] = Safe("Only needed for offline maps."),
        ["RetailDemo"] = Safe("Store demo mode."),
        ["wisvc"] = Safe("Only needed for Windows Insider builds."),
        ["WerSvc"] = Safe("Sends crash reports to Microsoft."),
        ["lfsvc"] = Safe("Only needed for location services."),
        ["RemoteRegistry"] = Safe("Lets other PCs edit this registry; off is safer."),
        ["PhoneSvc"] = Safe("Only needed for telephony."),
        ["icssvc"] = Safe("Only needed for the mobile hotspot."),
        ["SEMgrSvc"] = Safe("Only needed for NFC payments."),
        ["TrkWks"] = Safe("Tracks moved files on network shares."),
        ["WMPNetworkSvc"] = Safe("Windows Media Player library sharing."),
        ["PcaSvc"] = Safe("Program Compatibility Assistant pop-ups."),
        ["edgeupdate"] = Safe("Edge updater; Edge updates itself when opened."),
        ["edgeupdatem"] = Safe("Edge updater; Edge updates itself when opened."),
        ["gupdate"] = Safe("Google updater; Chrome updates itself when opened."),
        ["gupdatem"] = Safe("Google updater; Chrome updates itself when opened."),

        ["SysMain"] = Caution("Speeds up app launches; stopping it can slow them."),
        ["WSearch"] = Caution("Windows Search gets slow without the index."),
        ["XblAuthManager"] = Caution("Xbox sign-in for games and Game Pass."),
        ["XblGameSave"] = Caution("Xbox cloud saves."),
        ["XboxNetApiSvc"] = Caution("Xbox Live multiplayer networking."),
        ["XboxGipSvc"] = Caution("Xbox controllers and accessories."),
        ["bthserv"] = Caution("Bluetooth devices stop working."),
        ["TabletInputService"] = Caution("Touch keyboard, pen and handwriting."),
        ["WbioSrvc"] = Caution("Fingerprint and Windows Hello sign-in."),
        ["stisvc"] = Caution("Scanners and some cameras."),
        ["SCardSvr"] = Caution("Smart cards."),
        ["CDPSvc"] = Caution("Nearby sharing and Phone Link."),
        ["wlidsvc"] = Caution("Microsoft account sign-in for apps and the Store."),
        ["Themes"] = Caution("Windows themes and visual styles."),
        ["DPS"] = Caution("Network and system troubleshooters."),
        ["wuauserv"] = Caution("Windows Update; pause updates instead."),
        ["BITS"] = Caution("Background downloads for updates and the Store."),
        ["W32Time"] = Caution("Keeps the clock in sync."),
        ["LanmanWorkstation"] = Caution("Needed to open network shares."),
        ["LanmanServer"] = Caution("Needed to share files from this PC."),

        ["WinDefend"] = Keep("Microsoft Defender antivirus."),
        ["SecurityHealthService"] = Keep("Windows Security."),
        ["wscsvc"] = Keep("Security Center."),
        ["mpssvc"] = Keep("Windows Firewall."),
        ["BFE"] = Keep("Firewall and IPsec engine."),
        ["Dhcp"] = Keep("Gets this PC's network address."),
        ["Dnscache"] = Keep("Resolves web addresses."),
        ["nsi"] = Keep("Network connections."),
        ["RpcSs"] = Keep("Core Windows service."),
        ["RpcEptMapper"] = Keep("Core Windows service."),
        ["DcomLaunch"] = Keep("Core Windows service."),
        ["PlugPlay"] = Keep("Detects devices."),
        ["Power"] = Keep("Power management."),
        ["ProfSvc"] = Keep("Loads user profiles."),
        ["Schedule"] = Keep("Task Scheduler."),
        ["Winmgmt"] = Keep("WMI: many apps and Tweaker itself need it."),
        ["EventLog"] = Keep("Windows event logs."),
        ["Audiosrv"] = Keep("Sound."),
        ["AudioEndpointBuilder"] = Keep("Sound devices."),
        ["CryptSvc"] = Keep("Certificates and signatures."),
        ["TrustedInstaller"] = Keep("Installs Windows updates and features."),
        ["gpsvc"] = Keep("Group Policy."),
        ["UserManager"] = Keep("Core Windows service."),
        ["CoreMessagingRegistrar"] = Keep("Core Windows service."),
        ["StateRepository"] = Keep("Store apps and the Start menu."),
        ["AppXSvc"] = Keep("Store app installs and updates."),
        ["camsvc"] = Keep("App permissions for camera and microphone."),
        ["vgc"] = Keep("Riot Vanguard anti-cheat: VALORANT and League won't start."),
        ["EasyAntiCheat"] = Keep("Easy Anti-Cheat: protected games won't start."),
        ["EasyAntiCheat_EOS"] = Keep("Easy Anti-Cheat: protected games won't start."),
        ["BEService"] = Keep("BattlEye anti-cheat: protected games won't start."),
    };

    public static ServiceAdvice? For(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return null;
        if (Table.TryGetValue(serviceName, out ServiceAdvice? advice)) return advice;
        int underscore = serviceName.LastIndexOf('_');
        return underscore > 0 && Table.TryGetValue(serviceName[..underscore], out advice) ? advice : null;
    }

    private static ServiceAdvice Safe(string reason) => new(SafetyLevel.Safe, reason);
    private static ServiceAdvice Caution(string reason) => new(SafetyLevel.Caution, reason);
    private static ServiceAdvice Keep(string reason) => new(SafetyLevel.Keep, reason);
}
