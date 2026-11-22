using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class MainViewModelTests
{
    private const string Json = """
    [
      { "id": "b", "name": "Bravo", "category": "Network",  "description": "ping", "type": "registry", "risk": "medium", "requiresRestart": false, "apply": [], "revert": [] },
      { "id": "a", "name": "Alpha", "category": "Graphics", "description": "gpu",  "type": "registry", "risk": "low",    "requiresRestart": false, "apply": [], "revert": [] },
      { "id": "c", "name": "Charlie","category": "Graphics", "description": "vsync","type": "registry", "risk": "low",    "requiresRestart": false, "apply": [], "revert": [] }
    ]
    """;

    private static MainViewModel Build()
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, Json);
        var catalog = new CatalogService(new FileCatalogSource(path));
        return new MainViewModel(catalog, new FakeTweakEngine(), new FakeConfirmationService());
    }

    [Fact]
    public void Categories_start_with_All()
    {
        MainViewModel vm = Build();
        Assert.Equal("All", vm.Categories[0]);
        Assert.Contains("Graphics", vm.Categories);
        Assert.Contains("Network", vm.Categories);
    }

    [Fact]
    public void All_shows_one_group_per_category()
    {
        MainViewModel vm = Build();
        vm.SelectedCategory = "All";
        Assert.Equal(new[] { "Graphics", "Network" }, vm.Groups.Select(g => g.Name));
        Assert.Equal(2, vm.Groups.First(g => g.Name == "Graphics").Tweaks.Count);
    }

    [Fact]
    public void Selecting_a_category_shows_only_that_group()
    {
        MainViewModel vm = Build();
        vm.SelectedCategory = "Network";
        Assert.Single(vm.Groups);
        Assert.Equal("Network", vm.Groups[0].Name);
    }

    [Fact]
    public void Search_filters_by_name_and_drops_empty_groups()
    {
        MainViewModel vm = Build();
        vm.SelectedCategory = "All";
        vm.SearchText = "Charlie";
        Assert.Single(vm.Groups);
        Assert.Equal("Graphics", vm.Groups[0].Name);
        Assert.Single(vm.Groups[0].Tweaks);
    }

    [Fact]
    public void Categories_are_sorted_alphabetically_not_insertion_order()
    {
        MainViewModel vm = Build();
        Assert.Equal(new[] { "All", "Graphics", "Network" }, vm.Categories);

        vm.SelectedCategory = "All";
        Assert.Equal(new[] { "Graphics", "Network" }, vm.Groups.Select(g => g.Name));
    }

    [Fact]
    public void Search_is_case_insensitive_and_matches_name()
    {
        MainViewModel vm = Build();
        vm.SelectedCategory = "All";
        vm.SearchText = "alpha";
        Assert.Single(vm.Groups);
        Assert.Equal("Graphics", vm.Groups[0].Name);
        Assert.Single(vm.Groups[0].Tweaks);
        Assert.Equal("Alpha", vm.Groups[0].Tweaks[0].Name);
    }

    [Fact]
    public void Search_matches_description()
    {
        MainViewModel vm = Build();
        vm.SelectedCategory = "All";
        vm.SearchText = "ping";
        Assert.Single(vm.Groups);
        Assert.Equal("Network", vm.Groups[0].Name);
        Assert.Single(vm.Groups[0].Tweaks);
        Assert.Equal("Bravo", vm.Groups[0].Tweaks[0].Name);
    }

    [Fact]
    public void IsDev_reflects_catalog_can_edit()
    {
        MainViewModel vm = Build();
        Assert.True(vm.IsDev);
    }

    [Fact]
    public void ApplyPreset_applies_each_existing_tweak_id()
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        System.IO.File.WriteAllText(path, """
        [
          { "id":"a","name":"A","category":"X","description":"","type":"registry","risk":"low","requiresRestart":false,"apply":[],"revert":[] },
          { "id":"b","name":"B","category":"X","description":"","type":"registry","risk":"low","requiresRestart":false,"apply":[],"revert":[] }
        ]
        """);
        var engine = new FakeTweakEngine();
        var vm = new MainViewModel(new Tweaker.Core.Services.CatalogService(new FileCatalogSource(path)), engine, new FakeConfirmationService());

        vm.ApplyPreset(new TweakPreset("T", new[] { "a", "b", "missing" }));

        Assert.Contains("a", engine.Applied);
        Assert.Contains("b", engine.Applied);
        Assert.DoesNotContain("missing", engine.Applied);
    }

    [Fact]
    public void ApplyPreset_is_blocked_when_batch_confirmation_declined()
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        System.IO.File.WriteAllText(path, """
        [ { "id":"a","name":"A","category":"X","description":"","type":"registry","risk":"low","requiresRestart":false,"apply":[],"revert":[] } ]
        """);
        var engine = new FakeTweakEngine();
        var confirm = new FakeConfirmationService { Result = false };
        var vm = new MainViewModel(new CatalogService(new FileCatalogSource(path)), engine, confirm);

        vm.ApplyPreset(new TweakPreset("T", new[] { "a" }));

        Assert.Empty(engine.Applied);
        Assert.Single(confirm.BatchRequests);
        Assert.Equal("A", confirm.BatchRequests[0].Items[0].Name);
    }

    private static MainViewModel BuildWithConfigSurface()
    {
        var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        System.IO.File.WriteAllText(path, """
        [
          { "id":"g","name":"G","category":"Graphics","description":"","type":"registry","risk":"low","requiresRestart":false,"apply":[],"revert":[] },
          { "id":"t","name":"T","category":"Taskbar","description":"","type":"registry","risk":"low","requiresRestart":false,"apply":[],"revert":[] }
        ]
        """);
        return new MainViewModel(new CatalogService(new FileCatalogSource(path)), new FakeTweakEngine(), new FakeConfirmationService());
    }

    [Fact]
    public void Tweaks_tab_excludes_config_surface_categories()
    {
        MainViewModel vm = BuildWithConfigSurface();
        Assert.DoesNotContain("Taskbar", vm.Categories);
        vm.SelectedCategory = "All";
        Assert.DoesNotContain(vm.Groups, g => g.Name == "Taskbar");
        Assert.Contains(vm.Groups, g => g.Name == "Graphics");
    }

    [Fact]
    public void ActiveCategory_raises_change_but_does_not_rebuild()
    {
        MainViewModel vm = BuildWithConfigSurface();
        vm.SelectedCategory = "All";
        var groupsBefore = vm.Groups;
        int count = vm.Groups.Count;
        bool raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.ActiveCategory)) raised = true; };

        vm.ActiveCategory = "Graphics";

        Assert.True(raised);
        Assert.Same(groupsBefore, vm.Groups);
        Assert.Equal(count, vm.Groups.Count);
    }

    [Fact]
    public void Selecting_a_category_syncs_ActiveCategory_no_stale_highlight()
    {
        MainViewModel vm = Build();
        vm.SelectedCategory = "All";
        vm.ActiveCategory = "Graphics";
        vm.SelectedCategory = "Network";
        Assert.Equal("Network", vm.ActiveCategory);
    }

    private const string OneScriptCatalog = """
    [ { "id":"s","name":"Script","category":"X","description":"","type":"batch","risk":"low","requiresRestart":false,
        "applyScript":"echo on","revertScript":"echo off","apply":[],"revert":[] } ]
    """;

    private static MainViewModel BuildWith(TempDir temp, string catalogJson, ITweakEngine engine, FakeConfirmationService? confirm = null)
    {
        string path = Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, catalogJson);
        return new MainViewModel(new CatalogService(new FileCatalogSource(path)), engine, confirm ?? new FakeConfirmationService { Result = true });
    }

    [Fact]
    public async Task A_rebuild_keeps_the_same_row_so_a_running_script_stays_busy()
    {
        using var temp = new TempDir();
        var engine = new GatedTweakEngine();
        MainViewModel vm = BuildWith(temp, OneScriptCatalog, engine);
        TweakViewModel row = vm.Groups.SelectMany(g => g.Tweaks).Single();

        Task first = row.ToggleAsync();
        Assert.True(engine.Entered.Wait(TimeSpan.FromSeconds(5)));

        vm.SearchText = "Scr";
        TweakViewModel shown = vm.Groups.SelectMany(g => g.Tweaks).Single();
        Assert.Same(row, shown);
        Assert.True(shown.IsBusy);

        engine.Release.Set();
        await first;
        Assert.Equal("applied", shown.StatusText);
    }

    [Fact]
    public void ApplyPreset_skips_a_row_whose_script_is_still_running()
    {
        using var temp = new TempDir();
        var engine = new GatedTweakEngine();
        var logs = new List<string>();
        MainViewModel vm = BuildWith(temp, OneScriptCatalog, engine);
        TweakViewModel row = vm.Groups.SelectMany(g => g.Tweaks).Single();

        _ = row.ToggleAsync();
        Assert.True(engine.Entered.Wait(TimeSpan.FromSeconds(5)));

        vm.ApplyPreset(new TweakPreset("P", new[] { "s" }));

        Assert.Equal(1, engine.ApplyCalls);
        Assert.Contains(vm.Log, l => l.Contains("still running"));
        engine.Release.Set();
    }

    [Fact]
    public void ApplyPreset_skips_tweaks_that_are_already_applied()
    {
        using var temp = new TempDir();
        var engine = new FakeTweakEngine();
        engine.Overrides["a"] = TweakState.Applied;
        MainViewModel vm = BuildWith(temp, """
        [
          { "id":"a","name":"A","category":"X","description":"","type":"registry","risk":"low","requiresRestart":false,"apply":[],"revert":[] },
          { "id":"b","name":"B","category":"X","description":"","type":"registry","risk":"low","requiresRestart":false,"apply":[],"revert":[] }
        ]
        """, engine);

        vm.ApplyPreset(new TweakPreset("P", new[] { "a", "b" }));

        Assert.DoesNotContain("a", engine.Applied);
        Assert.Contains("b", engine.Applied);
    }

    [Fact]
    public void Preset_applied_script_tweak_shows_on_with_the_real_engine()
    {
        using var temp = new TempDir();
        var registry = new FakeRegistryService();
        var engine = new TweakEngine(registry, new BackupService(registry, temp.Path), new BatchService(new FakeProcessRunner()),
            new RegistryStateDetector(registry), new InMemoryAppliedTweakStore());
        MainViewModel vm = BuildWith(temp, OneScriptCatalog, engine);

        vm.ApplyPreset(new TweakPreset("P", new[] { "s" }));

        TweakViewModel row = vm.Groups.SelectMany(g => g.Tweaks).Single();
        Assert.True(row.IsApplied);
        Assert.Equal("applied", row.StatusText);
    }

    [Fact]
    public void Searching_a_myth_shows_why_it_is_left_out()
    {
        MainViewModel vm = Build();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.SearchText = "disable hpet";
        Assert.True(vm.ShowMythNote);
        Assert.Contains("HPET", vm.MythNote);
        Assert.Contains(nameof(MainViewModel.ShowMythNote), raised);

        vm.SearchText = "vsync";
        Assert.False(vm.ShowMythNote);
        Assert.Equal("", vm.MythNote);
    }

    private const string FreedJson = """
    [
      { "id": "sysmain", "name": "SysMain", "category": "Services", "description": "d", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [], "freesRamMb": 100 },
      { "id": "widgets", "name": "Widgets", "category": "Taskbar", "description": "d", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [], "freesRamMb": 150 },
      { "id": "plain", "name": "Plain", "category": "Services", "description": "d", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [] },
      { "id": "val", "name": "Val", "category": "CPU", "description": "d", "type": "value", "risk": "low", "requiresRestart": false, "freesRamMb": 999,
        "power": { "subgroup": "54533251-82be-4824-96c1-47b60b740d00", "setting": "0cc5b647-c1df-4637-891a-dec35c318583", "min": 0, "max": 100, "step": 1, "unit": "%" } }
    ]
    """;

    private static MainViewModel BuildFreed(FakeTweakEngine engine, FakeConfirmationService? confirm = null, string json = FreedJson)
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, json);
        var catalog = new CatalogService(new FileCatalogSource(path));
        return new MainViewModel(catalog, engine, confirm ?? new FakeConfirmationService());
    }

    [Fact]
    public void Estimated_freed_sums_applied_tweaks_including_config_and_excluding_value_tweaks()
    {
        var engine = new FakeTweakEngine();
        engine.Overrides["sysmain"] = TweakState.Applied;
        engine.Overrides["widgets"] = TweakState.Applied;
        engine.Overrides["plain"] = TweakState.Applied;
        engine.Overrides["val"] = TweakState.Applied;
        MainViewModel vm = BuildFreed(engine);

        Assert.Equal(250, vm.EstimatedFreedMb);

        engine.Overrides["widgets"] = TweakState.NotApplied;
        Assert.Equal(100, vm.EstimatedFreedMb);
    }

    [Fact]
    public async System.Threading.Tasks.Task Applying_a_row_raises_the_estimate()
    {
        var engine = new FakeTweakEngine();
        MainViewModel vm = BuildFreed(engine, new FakeConfirmationService { Result = true });
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);
        Assert.Equal(0, vm.EstimatedFreedMb);

        TweakViewModel row = vm.Groups.SelectMany(g => g.Tweaks).Single(t => t.Id == "sysmain");
        await row.ApplyAsync();

        Assert.Contains(nameof(MainViewModel.EstimatedFreedMb), raised);
        Assert.Equal(100, vm.EstimatedFreedMb);
    }

    private const string CoveredJson = """
    [
      { "id": "disable-sysmain", "name": "SysMain", "category": "Services", "description": "d", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [], "freesRamMb": 100 },
      { "id": "disable-search-indexing", "name": "Search", "category": "Services", "description": "d", "type": "registry", "risk": "low", "requiresRestart": true, "apply": [], "revert": [], "freesRamMb": 60 },
      { "id": "background-apps-off", "name": "Background", "category": "Privacy", "description": "d", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [], "freesRamMb": 150 }
    ]
    """;

    [Fact]
    public void Covered_services_are_those_of_the_applied_estimates()
    {
        var engine = new FakeTweakEngine();
        engine.Overrides["disable-sysmain"] = TweakState.Applied;
        engine.Overrides["background-apps-off"] = TweakState.Applied;
        MainViewModel vm = BuildFreed(engine, json: CoveredJson);

        Assert.Equal(new[] { "SysMain" }, vm.CoveredServices);
        Assert.Contains("sysmain", vm.CoveredServices);

        engine.Overrides["disable-search-indexing"] = TweakState.Applied;
        Assert.Equal(new[] { "SysMain", "WSearch" }, vm.CoveredServices.OrderBy(n => n, StringComparer.Ordinal));

        engine.Overrides["disable-sysmain"] = TweakState.NotApplied;
        engine.Overrides["disable-search-indexing"] = TweakState.NotApplied;
        Assert.Empty(vm.CoveredServices);
    }

    [Fact]
    public void RefreshStates_raises_the_estimate()
    {
        MainViewModel vm = BuildFreed(new FakeTweakEngine());
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.RefreshStates();

        Assert.Contains(nameof(MainViewModel.EstimatedFreedMb), raised);
    }

    private const string ValueJson = """
    [
      { "id": "r", "name": "Reg", "category": "CPU", "description": "d", "type": "registry", "risk": "low", "requiresRestart": false, "apply": [], "revert": [] },
      { "id": "v", "name": "Val", "category": "CPU", "description": "d", "type": "value", "risk": "low", "requiresRestart": false,
        "power": { "subgroup": "54533251-82be-4824-96c1-47b60b740d00", "setting": "0cc5b647-c1df-4637-891a-dec35c318583", "min": 0, "max": 100, "step": 1, "unit": "%" } }
    ]
    """;

    private static MainViewModel BuildWithValues(bool wirePower, FakeTweakEngine? engine = null, FakeConfirmationService? confirm = null,
        FakePowerSettings? power = null)
    {
        using var temp = new TempDir();
        string path = System.IO.Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, ValueJson);
        var catalog = new CatalogService(new FileCatalogSource(path));
        return wirePower
            ? new MainViewModel(catalog, engine ?? new FakeTweakEngine(), confirm ?? new FakeConfirmationService(), power ?? new FakePowerSettings(), new FakePowerBackup())
            : new MainViewModel(catalog, engine ?? new FakeTweakEngine(), confirm ?? new FakeConfirmationService());
    }

    [Fact]
    public void Value_tweaks_get_an_editor_when_power_settings_are_wired()
    {
        MainViewModel vm = BuildWithValues(wirePower: true);

        TweakViewModel row = vm.Groups.Single().Tweaks.Single(t => t.Id == "v");
        Assert.True(row.IsValue);
        Assert.NotNull(row.Value);
        Assert.Null(vm.Groups.Single().Tweaks.Single(t => t.Id == "r").Value);
    }

    [Fact]
    public void Value_tweaks_are_hidden_without_power_settings()
    {
        MainViewModel vm = BuildWithValues(wirePower: false);

        Assert.Equal(new[] { "r" }, vm.Groups.Single().Tweaks.Select(t => t.Id));
    }

    [Fact]
    public void Presets_skip_value_tweaks()
    {
        var engine = new FakeTweakEngine();
        var confirm = new FakeConfirmationService { Result = true };
        MainViewModel vm = BuildWithValues(wirePower: true, engine, confirm);

        vm.ApplyPreset(new TweakPreset("Mixed", new[] { "r", "v" }));

        Assert.Equal(new[] { "r" }, engine.Applied);
        Assert.Single(confirm.BatchRequests.Single().Items);
    }

    [Fact]
    public async System.Threading.Tasks.Task A_switch_row_refreshes_the_value_rows()
    {
        var power = new FakePowerSettings();
        string key = PowerKeys.Key(FakePowerSettings.Balanced, new System.Guid("54533251-82be-4824-96c1-47b60b740d00"),
            new System.Guid("0cc5b647-c1df-4637-891a-dec35c318583"));
        power.Values[key] = 10;
        MainViewModel vm = BuildWithValues(wirePower: true, confirm: new FakeConfirmationService { Result = true }, power: power);
        TweakViewModel valueRow = vm.Groups.Single().Tweaks.Single(t => t.Id == "v");
        Assert.Equal(10, valueRow.Value!.Number);

        power.Values[key] = 100;
        await vm.Groups.Single().Tweaks.Single(t => t.Id == "r").ApplyAsync();

        Assert.Equal(100, valueRow.Value.Number);
    }
}
