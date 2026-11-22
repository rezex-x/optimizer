using System.Linq;
using Tweaker.Core.Catalog;
using Tweaker.Resources;
using Xunit;

namespace Tweaker.Tests.Install;

public class AppIconTests
{
    [Fact]
    public void Every_app_has_a_nonblank_initial()
    {
        Assert.All(AppCatalog.Apps, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
        Assert.All(AppCatalog.Apps, a => Assert.True(char.IsLetterOrDigit(a.Name.TrimStart()[0])));
    }

    [Fact]
    public void Brand_icon_keys_are_all_real_winget_ids()
    {
        var ids = AppCatalog.Apps.Select(a => a.WingetId).ToHashSet();
        foreach (var key in BrandIcons.Keys)
            Assert.Contains(key, ids);
    }

    [Fact]
    public void Every_brand_glyph_has_a_parseable_color()
    {
        foreach (var key in BrandIcons.Keys)
        {
            Assert.True(BrandIcons.TryGetColor(key, out string hex), $"{key} has a glyph but no color");
            var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            Assert.True(c.A == 255);
        }
    }

    [Fact]
    public void Every_app_has_its_own_logo()
    {
        var missing = AppCatalog.Apps.Where(a => !AppIconImages.Has(a.WingetId) && !BrandIcons.TryGetPath(a.WingetId, out _))
            .Select(a => a.WingetId).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Bundled_logos_load_as_images()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        foreach (var app in AppCatalog.Apps.Where(a => AppIconImages.Has(a.WingetId)))
        {
            var image = AppIconImages.Get(app.WingetId);
            Assert.NotNull(image);
            Assert.True(image!.Width > 0, $"{app.WingetId} logo did not decode");
        }
    }
}
