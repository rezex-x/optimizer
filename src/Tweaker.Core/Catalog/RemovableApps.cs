namespace Tweaker.Core.Catalog;

public enum Impact { High, Medium, Low }

public sealed record RemovableApp(string PackageName, string Name, Impact Impact, bool Recommended, string Note);

public static class RemovableApps
{
    public const string TweakId = "bulk-app-removal";

    public static IReadOnlyList<RemovableApp> All { get; } = new List<RemovableApp>
    {
        new("MicrosoftTeams", "Microsoft Teams (personal, classic)", Impact.High, true, "Starts with Windows and runs in the background."),
        new("MSTeams", "Microsoft Teams (new)", Impact.High, false, "Also the work/school Teams client."),
        new("Microsoft.OutlookForWindows", "Outlook (new)", Impact.High, false, "Windows' built-in mail app on current builds."),
        new("Microsoft.Copilot", "Copilot", Impact.High, true, "The Copilot app."),
        new("Microsoft.549981C3F5F10", "Cortana", Impact.High, true, "Discontinued assistant."),
        new("Microsoft.BingNews", "News", Impact.High, true, "Feeds the Widgets board."),
        new("Microsoft.BingWeather", "Weather", Impact.High, true, "Feeds the Widgets board."),
        new("Microsoft.MicrosoftOfficeHub", "Microsoft 365 (Office hub)", Impact.High, true, "Office upsell launcher."),
        new("Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", Impact.High, true, "Ad-supported games."),

        new("Clipchamp.Clipchamp", "Clipchamp", Impact.Medium, true, "Video editor."),
        new("microsoft.windowscommunicationsapps", "Mail and Calendar", Impact.Medium, true, "Retired; replaced by the new Outlook."),
        new("Microsoft.Getstarted", "Tips", Impact.Medium, true, "Tips and suggestions."),
        new("Microsoft.WindowsFeedbackHub", "Feedback Hub", Impact.Medium, true, ""),
        new("Microsoft.People", "People", Impact.Medium, true, ""),
        new("Microsoft.Todos", "Microsoft To Do", Impact.Medium, true, ""),
        new("Microsoft.PowerAutomateDesktop", "Power Automate", Impact.Medium, true, ""),
        new("Microsoft.WindowsMaps", "Maps", Impact.Medium, true, "Downloads map updates in the background."),
        new("Microsoft.BingSearch", "Bing Search", Impact.Medium, true, "Web results in Windows search."),
        new("Microsoft.YourPhone", "Phone Link", Impact.Medium, false, "Keep it if you connect your phone."),
        new("Microsoft.MixedReality.Portal", "Mixed Reality Portal", Impact.Medium, true, "Only for Windows Mixed Reality headsets."),
        new("MicrosoftCorporationII.MicrosoftFamily", "Microsoft Family", Impact.Medium, true, ""),

        new("Microsoft.SkypeApp", "Skype", Impact.Low, true, "Retired."),
        new("Microsoft.MSPaint", "Paint 3D", Impact.Low, true, "Classic Paint is separate and stays."),
        new("Microsoft.3DBuilder", "3D Builder", Impact.Low, true, ""),
        new("Microsoft.Microsoft3DViewer", "3D Viewer", Impact.Low, true, ""),
        new("Microsoft.Print3D", "Print 3D", Impact.Low, true, ""),
        new("Microsoft.OneConnect", "Mobile Plans", Impact.Low, true, ""),
        new("Microsoft.Messaging", "Messaging", Impact.Low, true, ""),
        new("Microsoft.ZuneVideo", "Movies & TV", Impact.Low, false, ""),
        new("Microsoft.WindowsSoundRecorder", "Sound Recorder", Impact.Low, false, ""),
        new("Microsoft.WindowsAlarms", "Alarms & Clock", Impact.Low, false, ""),
        new("MicrosoftCorporationII.QuickAssist", "Quick Assist", Impact.Low, false, "Remote help from someone you trust."),
        new("Microsoft.Windows.DevHome", "Dev Home", Impact.Low, false, ""),
        new("Microsoft.GamingApp", "Xbox app", Impact.Low, false, "Needed for PC Game Pass."),
    };

    public static IReadOnlyList<string> NeverOffered { get; } = new[]
    {
        "Microsoft.WindowsStore", "Microsoft.StorePurchaseApp", "Microsoft.DesktopAppInstaller", "Microsoft.Windows.Photos",
        "Microsoft.WindowsCalculator", "Microsoft.WindowsNotepad", "Microsoft.Paint", "Microsoft.WindowsTerminal",
        "Microsoft.ScreenSketch", "Microsoft.SecHealthUI", "Microsoft.GetHelp", "Microsoft.VCLibs", "Microsoft.UI.Xaml",
        "Microsoft.NET", "Microsoft.XboxIdentityProvider", "Microsoft.XboxGameOverlay", "Microsoft.XboxGamingOverlay",
        "Microsoft.XboxSpeechToTextOverlay", "Microsoft.Xbox.TCUI",
    };
}
