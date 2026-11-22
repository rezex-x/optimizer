using System;
using System.IO;
using System.Linq;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class PresetTests
{
    private static MainViewModel Real()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "catalog.json");
        return new MainViewModel(new CatalogService(new FileCatalogSource(path)),
            new FakeTweakEngine(), new FakeConfirmationService());
    }

    [Fact]
    public void There_are_six_presets_with_expected_names()
    {
        var names = Real().Presets.Select(p => p.Name).ToArray();
        Assert.Equal(
            new[] { "Recommended", "Performance", "Competitive", "Streaming", "Privacy & Debloat", "Balanced" },
            names);
    }

    [Fact]
    public void Every_preset_id_resolves_to_a_real_tweak()
    {
        var vm = Real();
        var ids = new CatalogService(new FileCatalogSource(Path.Combine(AppContext.BaseDirectory, "catalog.json")))
            .Tweaks.Select(t => t.Id).ToHashSet();

        foreach (var preset in vm.Presets)
            foreach (var id in preset.Ids)
                Assert.True(ids.Contains(id), $"preset '{preset.Name}' references missing id '{id}'");
    }
}
