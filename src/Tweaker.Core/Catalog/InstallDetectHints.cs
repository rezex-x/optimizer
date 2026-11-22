namespace Tweaker.Core.Catalog;

public sealed record InstallDetectHint(IReadOnlyList<string> DisplayNames, IReadOnlyList<string> Processes, bool Exact = false);

public static class InstallDetectHints
{
    private static readonly Dictionary<string, InstallDetectHint> Hints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Google.Chrome"] = new(new[] { "Google Chrome" }, new[] { "chrome" }),
        ["Mozilla.Firefox"] = new(new[] { "Mozilla Firefox" }, new[] { "firefox" }),
        ["Brave.Brave"] = new(new[] { "Brave" }, new[] { "brave" }),
        ["Microsoft.VisualStudioCode"] = new(new[] { "Microsoft Visual Studio Code" }, new[] { "Code" }),
        ["Git.Git"] = new(new[] { "Git" }, Array.Empty<string>(), Exact: true),
        ["Microsoft.WSL"] = new(new[] { "WSL", "Windows Subsystem for Linux" }, Array.Empty<string>()),
        ["OpenJS.NodeJS.LTS"] = new(new[] { "Node.js" }, Array.Empty<string>()),
        ["Python.Python.3.12"] = new(new[] { "Python 3.12" }, Array.Empty<string>()),
        ["Microsoft.WindowsTerminal"] = new(Array.Empty<string>(), new[] { "WindowsTerminal" }),
        ["Valve.Steam"] = new(new[] { "Steam" }, new[] { "steam" }),
        ["EpicGames.EpicGamesLauncher"] = new(new[] { "Epic Games Launcher" }, new[] { "EpicGamesLauncher" }),
        ["Discord.Discord"] = new(new[] { "Discord" }, new[] { "Discord" }),
        ["Guru3D.Afterburner"] = new(new[] { "MSI Afterburner" }, new[] { "MSIAfterburner" }),
        ["OBSProject.OBSStudio"] = new(new[] { "OBS Studio" }, new[] { "obs64" }),
        ["Spotify.Spotify"] = new(new[] { "Spotify" }, new[] { "Spotify" }),
        ["voidtools.Everything"] = new(new[] { "Everything" }, new[] { "Everything" }),
        ["Microsoft.PowerToys"] = new(new[] { "PowerToys", "PowerToys (Preview)" }, new[] { "PowerToys" }),
        ["DominikReichl.KeePass"] = new(new[] { "KeePass Password Safe" }, new[] { "KeePass" }),
        ["Henry++.simplewall"] = new(new[] { "simplewall" }, new[] { "simplewall" }),
        ["REALiX.HWiNFO"] = new(new[] { "HWiNFO" }, new[] { "HWiNFO64" }),
        ["CPUID.CPU-Z"] = new(new[] { "CPUID CPU-Z" }, new[] { "cpuz" }),
        ["CPUID.HWMonitor"] = new(new[] { "CPUID HWMonitor" }, new[] { "HWMonitor" }),
        ["TechPowerUp.GPU-Z"] = new(new[] { "TechPowerUp GPU-Z" }, new[] { "GPU-Z" }),
        ["OO-Software.ShutUp10"] = new(new[] { "O&O ShutUp10", "O&O ShutUp10++" }, Array.Empty<string>()),
    };

    public static InstallDetectHint For(InstallApp app) =>
        Hints.TryGetValue(app.WingetId, out InstallDetectHint? hint) ? hint : new InstallDetectHint(new[] { app.Name }, Array.Empty<string>());
}
