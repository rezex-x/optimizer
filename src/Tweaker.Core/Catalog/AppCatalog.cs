namespace Tweaker.Core.Catalog;

public static class AppCatalog
{
    public static IReadOnlyList<InstallApp> Apps { get; } = new List<InstallApp>
    {
        new("Google.Chrome", "Google Chrome", "Web browser by Google", "Browsers"),
        new("Mozilla.Firefox", "Mozilla Firefox", "Open-source browser by Mozilla", "Browsers"),
        new("Brave.Brave", "Brave", "Privacy-focused browser", "Browsers"),
        new("Microsoft.Edge", "Microsoft Edge", "Built-in Chromium browser", "Browsers"),
        new("LibreWolf.LibreWolf", "LibreWolf", "Hardened Firefox fork", "Browsers"),
        new("Vivaldi.Vivaldi", "Vivaldi", "Highly customizable browser", "Browsers"),

        new("Microsoft.VisualStudioCode", "VS Code", "Code editor by Microsoft", "Development"),
        new("Git.Git", "Git", "Version control system", "Development"),
        new("OpenJS.NodeJS.LTS", "Node.js LTS", "JavaScript runtime", "Development"),
        new("Python.Python.3.12", "Python 3", "Programming language", "Development"),
        new("Microsoft.WindowsTerminal", "Windows Terminal", "Modern terminal app", "Development"),
        new("Microsoft.WSL", "WSL", "Linux subsystem for Windows", "Development"),
        new("Docker.DockerDesktop", "Docker Desktop", "Container platform", "Development"),
        new("Postman.Postman", "Postman", "API development tool", "Development"),

        new("7zip.7zip", "7-Zip", "File archiver", "Utilities"),
        new("VideoLAN.VLC", "VLC", "Media player", "Utilities"),
        new("voidtools.Everything", "Everything", "Instant file search", "Utilities"),
        new("TGRMNSoftware.BulkRenameUtility", "Bulk Rename Utility", "Batch file renaming", "Utilities"),
        new("Microsoft.PowerToys", "PowerToys", "Windows utilities suite", "Utilities"),
        new("Notepad++.Notepad++", "Notepad++", "Text editor", "Utilities"),

        new("OBSProject.OBSStudio", "OBS Studio", "Screen recording & streaming", "Media"),
        new("GIMP.GIMP", "GIMP", "Image editor", "Media"),
        new("HandBrake.HandBrake", "HandBrake", "Video transcoder", "Media"),
        new("Spotify.Spotify", "Spotify", "Music streaming", "Media"),
        new("IrfanView.IrfanView", "IrfanView", "Image viewer", "Media"),
        new("ShareX.ShareX", "ShareX", "Screenshots and screen recording", "Media"),

        new("Valve.Steam", "Steam", "PC gaming platform", "Gaming"),
        new("EpicGames.EpicGamesLauncher", "Epic Games", "Game store", "Gaming"),
        new("Discord.Discord", "Discord", "Voice & text chat", "Gaming"),
        new("Guru3D.Afterburner", "MSI Afterburner", "GPU overclocking", "Gaming"),

        new("WinDirStat.WinDirStat", "WinDirStat", "See what fills your disk", "System tools"),
        new("AntibodySoftware.WizTree", "WizTree", "Very fast disk space analyzer", "System tools"),
        new("CPUID.CPU-Z", "CPU-Z", "CPU, memory and board details", "System tools"),
        new("TechPowerUp.GPU-Z", "GPU-Z", "Graphics card details and sensors", "System tools"),
        new("REALiX.HWiNFO", "HWiNFO", "Full hardware sensors and logging", "System tools"),
        new("CPUID.HWMonitor", "HWMonitor", "Temperatures, voltages and fans", "System tools"),
        new("CrystalDewWorld.CrystalDiskInfo", "CrystalDiskInfo", "Drive health (S.M.A.R.T.)", "System tools"),
        new("CrystalDewWorld.CrystalDiskMark", "CrystalDiskMark", "Drive speed benchmark", "System tools"),
        new("Microsoft.Sysinternals.ProcessExplorer", "Process Explorer", "Advanced Task Manager", "System tools"),
        new("Microsoft.Sysinternals.ProcessMonitor", "Process Monitor", "Live file, registry and process activity", "System tools"),
        new("Microsoft.Sysinternals.Autoruns", "Autoruns", "Everything that starts with Windows", "System tools"),
        new("Resplendence.LatencyMon", "LatencyMon", "Find drivers causing DPC latency", "System tools"),
        new("Wagnardsoft.DisplayDriverUninstaller", "Display Driver Uninstaller", "Cleanly remove GPU drivers", "System tools"),
        new("TechPowerUp.NVCleanstall", "NVCleanstall", "Install only the NVIDIA driver parts you want", "System tools"),
        new("Rufus.Rufus", "Rufus", "Create bootable USB drives", "System tools"),
        new("RevoUninstaller.RevoUninstaller", "Revo Uninstaller", "Uninstall apps and their leftovers", "System tools"),
        new("Klocman.BulkCrapUninstaller", "BCUninstaller", "Remove many apps at once", "System tools"),
        new("OO-Software.ShutUp10", "O&O ShutUp10", "Privacy settings in one place", "System tools"),
        new("BleachBit.BleachBit", "BleachBit", "Free up disk space", "System tools"),

        new("Malwarebytes.Malwarebytes", "Malwarebytes", "Anti-malware tool", "Security"),
        new("DominikReichl.KeePass", "KeePass", "Password manager", "Security"),
        new("IDRIX.VeraCrypt", "VeraCrypt", "Disk encryption", "Security"),
        new("Henry++.simplewall", "SimpleWall", "Windows firewall manager", "Security"),
    };
}
