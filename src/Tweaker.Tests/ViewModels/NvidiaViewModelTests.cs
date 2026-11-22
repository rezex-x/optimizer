using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tweaker.Core.Nvidia;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class NvidiaViewModelTests
{
    private static (NvidiaViewModel vm, FakeNvidiaSettingsService svc, FakeConfirmationService confirm) Build(
        bool available = true, bool confirmResult = true)
    {
        var svc = new FakeNvidiaSettingsService { IsAvailable = available };
        var confirm = new FakeConfirmationService { Result = confirmResult };
        var backup = new NvidiaBackupStore(Path.Combine(Path.GetTempPath(), "nv-" + Path.GetRandomFileName() + ".json"));
        var vm = new NvidiaViewModel(svc, backup, confirm, NvidiaSettingsCatalog.Items());
        return (vm, svc, confirm);
    }

    [Fact]
    public void Categories_are_All_then_each_category_in_catalog_order()
    {
        var (vm, _, _) = Build();
        List<string> expected = NvidiaSettingsCatalog.Items().Select(s => s.Category).Distinct().ToList();

        Assert.Equal(new[] { "All" }.Concat(expected), vm.Categories);
        Assert.Equal("All", vm.SelectedCategory);
        Assert.Equal(expected, vm.Groups.Select(g => g.Name));
    }

    [Fact]
    public void Groups_reuse_the_setting_rows()
    {
        var (vm, _, _) = Build();

        Assert.Equal(vm.Settings.Count, vm.Groups.Sum(g => g.Settings.Count));
        Assert.All(vm.Groups.SelectMany(g => g.Settings), s => Assert.Contains(s, vm.Settings));
        Assert.All(vm.Groups, g => Assert.All(g.Settings, s => Assert.Equal(g.Name, s.Category)));
    }

    [Fact]
    public void Selecting_a_category_shows_only_that_group()
    {
        var (vm, _, _) = Build();

        vm.SelectedCategory = "Sync";

        NvidiaGroup group = Assert.Single(vm.Groups);
        Assert.Equal("Sync", group.Name);
        Assert.Equal(vm.Settings.Where(s => s.Category == "Sync"), group.Settings);
        Assert.Equal(NvidiaSettingsCatalog.Items().Count, vm.Settings.Count);

        vm.SelectedCategory = "All";
        Assert.Equal(vm.Categories.Count - 1, vm.Groups.Count);
    }

    [Fact]
    public void Unavailable_gpu_shows_banner_and_blocks_edit()
    {
        var (vm, _, _) = Build(available: false);
        Assert.False(vm.IsAvailable);
        Assert.Contains("No NVIDIA GPU", vm.GpuStatus);
        Assert.All(vm.Settings, s => Assert.False(s.CanEdit));
    }

    [Fact]
    public void Available_gpu_shows_name()
    {
        var (vm, _, _) = Build(available: true);
        Assert.True(vm.IsAvailable);
        Assert.Contains("NVIDIA", vm.GpuStatus);
        Assert.All(vm.Settings, s => Assert.True(s.CanEdit));
    }

    [Fact]
    public void Apply_declined_makes_no_service_call()
    {
        var (vm, svc, _) = Build(confirmResult: false);
        vm.Settings.First().ApplyCommand.Execute(null);
        Assert.Empty(svc.Applied);
    }

    [Fact]
    public void Apply_confirmed_sets_recommended_and_stores_prior()
    {
        var (vm, svc, _) = Build(confirmResult: true);
        var row = vm.Settings.First();
        var setting = NvidiaSettingsCatalog.Items().First(s => s.Id == "power-management");
        svc.Values[setting.SettingId] = setting.DefaultValue;

        row.ApplyCommand.Execute(null);

        Assert.Contains((setting.SettingId, setting.RecommendedValue), svc.Applied);
        Assert.True(row.IsApplied);
    }

    [Fact]
    public void Revert_restores_stored_prior()
    {
        var (vm, svc, _) = Build(confirmResult: true);
        var row = vm.Settings.First();
        var setting = NvidiaSettingsCatalog.Items().First(s => s.Id == "power-management");
        svc.Values[setting.SettingId] = setting.DefaultValue;
        row.ApplyCommand.Execute(null);

        row.RevertCommand.Execute(null);

        Assert.Equal(setting.DefaultValue, svc.ReadSetting(setting.SettingId));
    }

    [Fact]
    public void ApplyRecommended_declined_applies_nothing()
    {
        var (vm, svc, _) = Build(confirmResult: false);
        vm.ApplyRecommendedCommand.Execute(null);
        Assert.Empty(svc.Applied);
    }

    [Fact]
    public void ApplyRecommended_confirmed_applies_every_setting_once()
    {
        var (vm, svc, _) = Build(confirmResult: true);
        vm.ApplyRecommendedCommand.Execute(null);
        Assert.Equal(NvidiaSettingsCatalog.Items().Count, svc.Applied.Count);
    }

    [Fact]
    public void Render_uses_no_per_setting_reads_and_Refresh_batches_once()
    {
        var (vm, svc, _) = Build(available: true);
        var pm = NvidiaSettingsCatalog.Items().First(s => s.Id == "power-management");
        svc.Values[pm.SettingId] = pm.RecommendedValue;

        _ = vm.Settings.Select(s => s.IsApplied).ToList();
        Assert.Equal(0, svc.ReadSettingCalls);

        vm.Refresh();

        Assert.Equal(1, svc.ReadSettingsCalls);
        Assert.Equal(0, svc.ReadSettingCalls);
        Assert.True(vm.Settings.First(s => s.SettingId == pm.SettingId).IsApplied);
    }

    [Fact]
    public void Toggle_applies_when_off_then_reverts_when_on_with_the_applied_label()
    {
        var (vm, svc, _) = Build(confirmResult: true);
        var row = vm.Settings.First();
        var setting = NvidiaSettingsCatalog.Items().First(s => s.Id == "power-management");
        svc.Values[setting.SettingId] = setting.DefaultValue;

        row.ToggleCommand.Execute(null);
        Assert.True(row.IsApplied);
        Assert.Equal("applied", row.StatusText);

        row.ToggleCommand.Execute(null);
        Assert.False(row.IsApplied);
        Assert.Equal("", row.StatusText);
        Assert.Equal(setting.DefaultValue, svc.ReadSetting(setting.SettingId));
    }

    [Fact]
    public void Declined_toggle_renotifies_IsApplied_so_the_switch_resyncs_off()
    {
        var (vm, svc, _) = Build(confirmResult: false);
        var row = vm.Settings.First();
        var raised = new List<string>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        row.ToggleCommand.Execute(null);

        Assert.Empty(svc.Applied);
        Assert.False(row.IsApplied);
        Assert.Contains(nameof(row.IsApplied), raised);
    }

    [Fact]
    public void ApplyRecommended_skips_settings_that_are_already_applied()
    {
        var (vm, svc, _) = Build(confirmResult: true);
        var pm = NvidiaSettingsCatalog.Items().First(s => s.Id == "power-management");
        svc.Values[pm.SettingId] = pm.RecommendedValue;
        vm.Refresh();

        vm.ApplyRecommendedCommand.Execute(null);

        Assert.Equal(NvidiaSettingsCatalog.Items().Count - 1, svc.Applied.Count);
        Assert.DoesNotContain(svc.Applied, a => a.Id == pm.SettingId);
    }
}
