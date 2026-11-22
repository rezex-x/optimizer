using System;
using System.Linq;
using Tweaker.Core.Catalog;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class InstalledMatcherTests
{
    private static readonly InstallApp Chrome = new("Google.Chrome", "Google Chrome", "", "Browsers");
    private static readonly InstallApp Git = new("Git.Git", "Git", "", "Development");
    private static readonly InstallApp Code = new("Microsoft.VisualStudioCode", "VS Code", "", "Development");
    private static readonly InstallApp Discord = new("Discord.Discord", "Discord", "", "Gaming");
    private static readonly InstallApp SevenZip = new("7zip.7zip", "7-Zip", "", "Utilities");

    [Fact]
    public void A_winget_id_in_the_list_output_counts()
    {
        const string output = "Name            Id              Version\n-----\nGoogle Chrome   Google.Chrome   128.0\n";
        Assert.Equal(new[] { "Google.Chrome" }, InstalledMatcher.Match(new[] { Chrome, Git }, output, Array.Empty<string>(), Array.Empty<string>()));
    }

    [Fact]
    public void An_uninstall_display_name_counts_when_it_is_the_name_or_the_name_and_more()
    {
        var found = InstalledMatcher.Match(new[] { SevenZip, Git, Code }, "",
            new[] { "7-Zip 23.01 (x64)", "GitHub Desktop", "Microsoft Visual Studio Code (User)" }, Array.Empty<string>());

        Assert.Contains("7zip.7zip", found);
        Assert.Contains("Microsoft.VisualStudioCode", found);
        Assert.DoesNotContain("Git.Git", found);
    }

    [Fact]
    public void A_running_process_from_the_hints_counts()
    {
        var found = InstalledMatcher.Match(new[] { Discord, Chrome }, "", Array.Empty<string>(), new[] { "Discord.exe", "explorer" });
        Assert.Equal(new[] { "Discord.Discord" }, found.ToArray());
    }

    [Fact]
    public void Hints_fall_back_to_the_tile_name()
    {
        InstallDetectHint hint = InstallDetectHints.For(new InstallApp("X.Y", "Some Tool", "", "Utilities"));
        Assert.Equal(new[] { "Some Tool" }, hint.DisplayNames);
        Assert.Empty(hint.Processes);
    }

    [Fact]
    public void A_winget_id_cut_short_with_an_ellipsis_matches_as_a_prefix()
    {
        var pe = new InstallApp("Microsoft.Sysinternals.ProcessExplorer", "Process Explorer", "", "System tools");
        const string output = "Process Explorer   Microsoft.Sysinternals.Proc…   17.05\nGit   Git…   2.4";

        var found = InstalledMatcher.Match(new[] { pe, Git }, output, Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal(new[] { "Microsoft.Sysinternals.ProcessExplorer" }, found.ToArray());
    }

    [Fact]
    public void A_cut_short_id_that_fits_two_catalog_apps_counts_for_neither()
    {
        var pe = new InstallApp("Microsoft.Sysinternals.ProcessExplorer", "Process Explorer", "", "System tools");
        var pm = new InstallApp("Microsoft.Sysinternals.ProcessMonitor", "Process Monitor", "", "System tools");
        const string output = "Process Explorer   Microsoft.Sysinternals.Proc…   17.05\n";

        Assert.Empty(InstalledMatcher.Match(new[] { pe, pm }, output, Array.Empty<string>(), Array.Empty<string>()));
    }

    [Fact]
    public void An_exact_hint_needs_the_exact_name()
    {
        Assert.Empty(InstalledMatcher.Match(new[] { Git }, "", new[] { "Git LFS version 3.4.0", "Git Extensions 4.2.1" }, Array.Empty<string>()));
        Assert.Equal(new[] { "Git.Git" }, InstalledMatcher.Match(new[] { Git }, "", new[] { "Git" }, Array.Empty<string>()));
    }

    [Fact]
    public void A_version_name_may_continue_with_a_dot()
    {
        var python = new InstallApp("Python.Python.3.12", "Python 3.12", "", "Development");
        Assert.Equal(new[] { "Python.Python.3.12" },
            InstalledMatcher.Match(new[] { python }, "", new[] { "Python 3.12.6 (64-bit)" }, Array.Empty<string>()));
        Assert.Empty(InstalledMatcher.Match(new[] { Discord }, "", new[] { "Discord.old" }, Array.Empty<string>()));
    }

    [Fact]
    public void PowerToys_preview_ShutUp10_plus_plus_and_WSL_are_recognised()
    {
        var powerToys = new InstallApp("Microsoft.PowerToys", "PowerToys", "", "Utilities");
        var shutUp = new InstallApp("OO-Software.ShutUp10", "O&O ShutUp10", "", "System tools");
        var wsl = new InstallApp("Microsoft.WSL", "WSL", "", "Development");

        Assert.Single(InstalledMatcher.Match(new[] { powerToys }, "", new[] { "PowerToys (Preview)" }, Array.Empty<string>()));
        Assert.Single(InstalledMatcher.Match(new[] { shutUp }, "", new[] { "O&O ShutUp10++" }, Array.Empty<string>()));
        Assert.Single(InstalledMatcher.Match(new[] { wsl }, "", new[] { "Windows Subsystem for Linux" }, Array.Empty<string>()));
    }

    [Fact]
    public void Discord_PTB_is_not_Discord()
    {
        Assert.Empty(InstalledMatcher.Match(new[] { Discord }, "", new[] { "Discord PTB" }, Array.Empty<string>()));
    }

    [Fact]
    public void Preview_editions_are_not_the_app()
    {
        var found = InstalledMatcher.Match(new[] { Code }, "",
            new[] { "Microsoft Visual Studio Code - Insiders (User)" }, Array.Empty<string>());
        Assert.Empty(found);
    }

    [Fact]
    public void Exported_ids_are_read_from_the_winget_export_json()
    {
        const string json = "{\"Sources\":[{\"Packages\":[{\"PackageIdentifier\":\"Discord.Discord\"},{\"PackageIdentifier\" : \"7zip.7zip\"}]}]}";
        Assert.Equal("Discord.Discord\n7zip.7zip", Tweaker.Services.InstalledAppDetector.ExportedIds(json));
    }
}
