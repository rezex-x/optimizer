using System;
using System.IO;
using System.Linq;
using Tweaker.Core.Json;
using Tweaker.Core.Models;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class IssuesCatalogTests
{
    private static List<Tweak> Catalog() =>
        TweakJson.DeserializeCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json")));

    [Fact]
    public void Hide_widgets_uses_the_policy_newer_windows_honours()
    {
        Tweak t = Assert.Single(Catalog(), x => x.Id == "tb-widgets");

        RegistryOp apply = Assert.Single(t.Apply);
        Assert.Equal(("HKLM", @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0),
            (apply.Hive, apply.Path, apply.Name, apply.Value.GetInt32()));
        Assert.Equal(t.Apply.Select(o => (o.Hive, o.Path, o.Name)), t.Revert.Select(o => (o.Hive, o.Path, o.Name)));
        Assert.DoesNotContain(t.Apply.Concat(t.Revert), o => o.Name == "TaskbarDa");
    }

    [Fact]
    public void Bulk_app_removal_describes_the_picker()
    {
        Tweak t = Assert.Single(Catalog(), x => x.Id == "bulk-app-removal");
        Assert.Equal("Choose which preinstalled apps to remove (impact High/Medium/Low); they are removed for every user and won't reinstall for new users.",
            t.Description);
    }
}
