using Tweaker.Core.Catalog;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class AppCatalogTests
{
    [Fact]
    public void Catalog_is_populated_and_well_formed()
    {
        var apps = AppCatalog.Apps;
        Assert.True(apps.Count >= 30);
        Assert.All(apps, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.WingetId));
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            Assert.False(string.IsNullOrWhiteSpace(a.Category));
        });
        Assert.Contains(apps, a => a.WingetId == "Google.Chrome");
        Assert.Contains(apps, a => a.WingetId == "Valve.Steam");
        Assert.Equal(apps.Select(a => a.WingetId).Distinct().Count(), apps.Count);
        foreach (var c in new[] { "Browsers", "Development", "Utilities", "Media", "Gaming", "Security" })
            Assert.Contains(apps, a => a.Category == c);
    }
}
