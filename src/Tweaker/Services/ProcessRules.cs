using System;
using System.Collections.Generic;
using System.IO;

namespace Tweaker.Services;

public static class ProcessRules
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Secure System", "Memory Compression",
        "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe", "services.exe", "lsass.exe", "LsaIso.exe",
        "svchost.exe", "dwm.exe", "fontdrvhost.exe", "sihost.exe", "explorer.exe", "TrustedInstaller.exe",
        "WUDFHost.exe", "ctfmon.exe",
        "MsMpEng.exe", "NisSrv.exe", "SecurityHealthService.exe", "MpDefenderCoreService.exe", "SgrmBroker.exe",
        "EasyAntiCheat.exe", "EasyAntiCheat_EOS.exe", "BEService.exe", "BEService_x64.exe", "vgc.exe", "vgtray.exe",
        "FACEITService.exe", "FACEIT.exe", "EAAntiCheat.GameService.exe", "EAAntiCheat.GameServiceLauncher.exe", "PnkBstrA.exe", "PnkBstrB.exe",
        "SGuard64.exe", "SGuardSvc64.exe", "GameMon.exe", "GameMon64.exe",
    };

    private static readonly string[] ProtectedSuffixes = { "_BE.exe", "_EAC.exe", "_EAC_EOS.exe" };
    private static readonly string[] ProtectedPrefixes = { "EasyAntiCheat", "BEClient" };

    public static bool IsProtected(ProcessSample process, int selfPid) =>
        process.Pid <= 4 || process.Pid == selfPid || process.IsCritical || IsProtectedName(process.Name);

    private static bool IsProtectedName(string name)
    {
        if (ProtectedNames.Contains(name) || string.Equals(name, "start_protected_game.exe", StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (string suffix in ProtectedSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        foreach (string prefix in ProtectedPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static ProcessKind Kind(ProcessSample process, string windowsDir)
    {
        if (process.Pid <= 4 || string.IsNullOrEmpty(process.ImagePath)) return ProcessKind.Windows;
        string prefix = windowsDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (process.ImagePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return ProcessKind.Windows;
        return process.HasWindow ? ProcessKind.App : ProcessKind.Background;
    }
}
