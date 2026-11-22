using System.Linq;
using Tweaker.Core.Catalog;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class RemovableAppsTests
{
    [Fact]
    public void The_list_covers_the_usual_bloat_with_impacts()
    {
        var names = RemovableApps.All.Select(a => a.PackageName).ToList();

        foreach (string p in new[] { "Microsoft.OutlookForWindows", "Clipchamp.Clipchamp", "MicrosoftTeams", "MSTeams", "Microsoft.Copilot",
                     "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.MicrosoftOfficeHub", "Microsoft.GamingApp" })
            Assert.Contains(p, names);
        Assert.Equal(names.Count, names.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(RemovableApps.All, a => a.Impact == Impact.High);
        Assert.Contains(RemovableApps.All, a => a.Impact == Impact.Medium);
        Assert.Contains(RemovableApps.All, a => a.Impact == Impact.Low);
        Assert.All(RemovableApps.All, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
        Assert.All(RemovableApps.All, a => Assert.Matches(@"^[A-Za-z0-9._-]+$", a.PackageName));
    }

    [Fact]
    public void Store_winget_photos_and_frameworks_are_never_offered()
    {
        foreach (string kept in RemovableApps.NeverOffered)
            Assert.DoesNotContain(RemovableApps.All, a => a.PackageName.Contains(kept, System.StringComparison.OrdinalIgnoreCase));
        foreach (string p in new[] { "Microsoft.WindowsStore", "Microsoft.DesktopAppInstaller", "Microsoft.Windows.Photos", "Microsoft.VCLibs",
                     "Microsoft.XboxIdentityProvider", "Microsoft.GetHelp", "Microsoft.SecHealthUI" })
            Assert.Contains(p, RemovableApps.NeverOffered);
    }

    [Fact]
    public void Things_people_use_start_unticked()
    {
        Assert.False(RemovableApps.All.Single(a => a.PackageName == "Microsoft.YourPhone").Recommended);
        RemovableApp xbox = RemovableApps.All.Single(a => a.PackageName == "Microsoft.GamingApp");
        Assert.False(xbox.Recommended);
        Assert.Contains("Game Pass", xbox.Note);
        Assert.False(RemovableApps.All.Single(a => a.PackageName == "MSTeams").Recommended);
        Assert.False(RemovableApps.All.Single(a => a.PackageName == "Microsoft.OutlookForWindows").Recommended);
        Assert.True(RemovableApps.All.Single(a => a.PackageName == "MicrosoftTeams").Recommended);
    }
}
