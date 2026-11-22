using System;
using System.IO;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class CatalogServiceTests
{
    private const string Json = """
    [
      { "id": "a", "name": "A", "category": "Graphics", "description": "", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [] },
      { "id": "b", "name": "B", "category": "Network",  "description": "", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [] },
      { "id": "c", "name": "C", "category": "Graphics", "description": "", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [] }
    ]
    """;

    [Fact]
    public void Categories_are_distinct_and_sorted()
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, Json);
        var svc = new CatalogService(new FileCatalogSource(path));

        Assert.Equal(new[] { "Graphics", "Network" }, svc.Categories());
    }

    [Fact]
    public void InCategory_filters()
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, Json);
        var svc = new CatalogService(new FileCatalogSource(path));

        Assert.Equal(new[] { "a", "c" }, svc.InCategory("Graphics").Select(t => t.Id));
    }

    [Fact]
    public void AddOrUpdate_persists_and_replaces_by_id()
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, Json);
        var svc = new CatalogService(new FileCatalogSource(path));

        svc.AddOrUpdate(new Tweak { Id = "a", Name = "A2", Category = "Graphics" });
        svc.AddOrUpdate(new Tweak { Id = "d", Name = "D", Category = "Power" });

        var reloaded = new CatalogService(new FileCatalogSource(path));
        Assert.Equal("A2", reloaded.Tweaks.Single(t => t.Id == "a").Name);
        Assert.Contains(reloaded.Tweaks, t => t.Id == "d");
        Assert.Equal(4, reloaded.Tweaks.Count);
    }

    [Fact]
    public void AddOrUpdate_throws_when_source_is_readonly()
    {
        var source = new EmbeddedCatalogSource(typeof(EmbeddedCatalogSource).Assembly, "does.not.exist");
        var svc = new CatalogService(source);
        Assert.Throws<InvalidOperationException>(() => svc.AddOrUpdate(new Tweak { Id = "x", Category = "C" }));
    }
}
