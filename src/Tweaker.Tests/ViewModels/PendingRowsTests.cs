using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tweaker.Core.Models;
using Tweaker.Core.Nvidia;
using Tweaker.Core.Services;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class PendingRowsTests
{
    private const string Sub = "54533251-82be-4824-96c1-47b60b740d00";
    private const string CpMin = "0cc5b647-c1df-4637-891a-dec35c318583";
    private const string Boost = "be337238-0d82-4146-a960-4f3749d470c7";
    private static readonly DateTime T0 = new(2026, 9, 27, 12, 0, 0);

    private const string Json = """
    [
      { "id": "b", "name": "Bravo", "category": "Network",  "description": "ping", "type": "registry", "risk": "medium", "requiresRestart": false, "apply": [], "revert": [] },
      { "id": "a", "name": "Alpha", "category": "Graphics", "description": "gpu",  "type": "registry", "risk": "low",    "requiresRestart": false, "apply": [], "revert": [] },
      { "id": "c", "name": "Charlie","category": "Graphics", "description": "vsync","type": "registry", "risk": "low",    "requiresRestart": false, "apply": [], "revert": [] }
    ]
    """;

    private readonly FakeTweakEngine _engine = new();
    private readonly FakeConfirmationService _confirm = new() { Result = true };
    private readonly PendingChangesViewModel _bar = new();
    private readonly List<string> _log = new();

    private static Tweak Registry(string id = "t") => new()
    {
        Id = id, Name = "Tweak " + id, Category = "System", Type = TweakType.Registry, Risk = RiskLevel.Low,
        Description = "desc", Benefit = "benefit", Downsides = "downsides",
    };

    private TweakViewModel Row(Tweak tweak, PendingChangesViewModel? bar) => new(tweak, _engine, _confirm, _log.Add, pending: bar);

    private static string K(string setting) => PowerKeys.Key(FakePowerSettings.Balanced, new Guid(Sub), new Guid(setting));

    private static Tweak Slider() => new()
    {
        Id = "power-core-parking-min", Name = "Core parking minimum", Category = "CPU", Type = TweakType.Value, Risk = RiskLevel.Low,
        Power = new PowerSettingSpec { Subgroup = Sub, Setting = CpMin, Min = 0, Max = 100, Step = 1, Unit = "%" },
    };

    private static Tweak Dropdown() => new()
    {
        Id = "power-boost-mode", Name = "Boost mode", Category = "CPU", Type = TweakType.Value, Risk = RiskLevel.Medium,
        Power = new PowerSettingSpec
        {
            Subgroup = Sub, Setting = Boost,
            Options = new List<PowerOption>
            {
                new() { Value = 0, Label = "Disabled" }, new() { Value = 1, Label = "Enabled" }, new() { Value = 2, Label = "Aggressive" },
            },
        },
    };

    private MainViewModel Main(PendingChangesViewModel? bar = null, ActivityLog? activity = null)
    {
        using var temp = new TempDir();
        string path = Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, Json);
        var catalog = new CatalogService(new FileCatalogSource(path));
        return new MainViewModel(catalog, _engine, _confirm, pending: bar, activity: activity);
    }

    [Fact]
    public async Task A_switch_queues_instead_of_asking_and_runs_on_apply()
    {
        TweakViewModel row = Row(Registry(), _bar);

        await row.ToggleAsync();

        PendingItemViewModel item = Assert.Single(_bar.Items);
        Assert.Equal("Apply", item.ActionText);
        Assert.Equal("tweak|t", item.Key);
        Assert.Equal("System", item.Category);
        Assert.True(row.IsPending);
        Assert.True(row.IsSwitchOn);
        Assert.False(row.IsApplied);
        Assert.Equal("will apply", row.StatusText);
        Assert.Empty(_confirm.SingleRequests);
        Assert.Empty(_engine.Applied);

        await _bar.ApplyAllAsync();

        Assert.Contains("t", _engine.Applied);
        Assert.False(row.IsPending);
        Assert.True(row.IsSwitchOn);
        Assert.Equal("applied", row.StatusText);
        Assert.Empty(_confirm.SingleRequests);
    }

    [Fact]
    public async Task Toggling_a_pending_switch_again_unstages_it()
    {
        TweakViewModel row = Row(Registry(), _bar);

        await row.ToggleAsync();
        await row.ToggleAsync();

        Assert.Empty(_bar.Items);
        Assert.False(row.IsSwitchOn);
        Assert.False(row.IsPending);
        Assert.Equal("", row.StatusText);
        Assert.Empty(_engine.Applied);
    }

    [Fact]
    public async Task An_applied_switch_queues_a_revert()
    {
        _engine.Overrides["t"] = TweakState.Applied;
        TweakViewModel row = Row(Registry(), _bar);

        await row.ToggleAsync();

        Assert.Equal("Revert", Assert.Single(_bar.Items).ActionText);
        Assert.False(row.IsSwitchOn);
        Assert.Equal("will revert", row.StatusText);

        await _bar.ApplyAllAsync();

        Assert.Contains("t", _engine.Reverted);
    }

    [Fact]
    public async Task Discard_snaps_the_switch_back()
    {
        TweakViewModel row = Row(Registry(), _bar);
        await row.ToggleAsync();
        int raised = 0;
        row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TweakViewModel.IsSwitchOn)) raised++; };

        _bar.DiscardAll();

        Assert.False(row.IsSwitchOn);
        Assert.True(raised > 0);
        Assert.Empty(_engine.Applied);
    }

    [Fact]
    public async Task A_run_once_row_queues_a_run()
    {
        var tweak = new Tweak { Id = "clean", Name = "Clean caches", Type = TweakType.Batch, RunOnce = true };
        TweakViewModel row = Row(tweak, _bar);

        await row.RunAsync();

        Assert.Equal("Run", Assert.Single(_bar.Items).ActionText);
        Assert.True(_bar.Items[0].IsIrreversible);
        Assert.Equal("will run", row.StatusText);
        Assert.Empty(_confirm.SingleRequests);
        Assert.Empty(_engine.Applied);

        await _bar.ApplyAllAsync();

        Assert.Contains("clean", _engine.Applied);
    }

    [Fact]
    public async Task Without_a_bar_the_switch_still_asks_and_applies_at_once()
    {
        TweakViewModel row = Row(Registry(), null);

        await row.ToggleAsync();

        Assert.Single(_confirm.SingleRequests);
        Assert.Contains("t", _engine.Applied);
        Assert.False(row.IsPending);
        Assert.True(row.IsSwitchOn);
    }

    [Fact]
    public void A_preset_queues_its_tweaks()
    {
        MainViewModel main = Main(bar: _bar);

        main.ApplyPreset(new TweakPreset("P", new[] { "a", "b" }));

        Assert.Equal(new[] { "tweak|a", "tweak|b" }, _bar.Items.Select(i => i.Key));
        Assert.Empty(_engine.Applied);
        Assert.Empty(_confirm.BatchRequests);
        Assert.Contains(main.Log, l => l.Contains("queued 2 tweaks"));
    }

    [Fact]
    public void A_second_preset_replaces_the_queue_instead_of_adding_to_it()
    {
        var log = new List<string>();
        var bar = new PendingChangesViewModel(log.Add);
        MainViewModel main = Main(bar: bar);

        main.ApplyPreset(new TweakPreset("First", new[] { "a" }));
        main.ApplyPreset(new TweakPreset("Second", new[] { "b", "c" }));

        Assert.Equal(new[] { "tweak|b", "tweak|c" }, bar.Items.Select(i => i.Key));
        Assert.Contains("Reverted: Alpha (queued apply not applied)", log);
        Assert.False(main.Groups.SelectMany(g => g.Tweaks).Single(t => t.Id == "a").IsSwitchOn);
    }

    [Fact]
    public async Task Moving_a_power_slider_queues_a_set_and_moving_back_withdraws_it()
    {
        var power = new FakePowerSettings();
        var backup = new FakePowerBackup();
        power.Values[K(CpMin)] = 10;
        var vm = new PowerValueViewModel(Slider(), power, backup, _confirm, _log.Add, _bar);

        vm.Number = 60;

        Assert.Equal("Set to 60 %", Assert.Single(_bar.Items).ActionText);
        Assert.Equal("power|power-core-parking-min", _bar.Items[0].Key);
        Assert.Empty(power.Writes);
        Assert.False(vm.ShowApplyButton);

        vm.Number = 10;

        Assert.Empty(_bar.Items);

        vm.Number = 70;
        await _bar.ApplyAllAsync();

        Assert.Equal(70u, Assert.Single(power.Writes).Value);
        Assert.Empty(_confirm.SingleRequests);
        Assert.Equal(10u, backup.Originals[K(CpMin)]);
        Assert.Equal(70, vm.Number);
    }

    [Fact]
    public void Discarding_a_power_change_snaps_the_slider_back()
    {
        var power = new FakePowerSettings();
        power.Values[K(CpMin)] = 10;
        var vm = new PowerValueViewModel(Slider(), power, new FakePowerBackup(), _confirm, _log.Add, _bar);

        vm.Number = 60;
        _bar.DiscardAll();

        Assert.Equal(10, vm.Number);
        Assert.Equal("10", vm.NumberText);
        Assert.Empty(power.Writes);
    }

    [Fact]
    public void Picking_a_power_option_queues_it()
    {
        var power = new FakePowerSettings();
        power.Values[K(Boost)] = 1;
        var vm = new PowerValueViewModel(Dropdown(), power, new FakePowerBackup(), _confirm, _log.Add, _bar);

        vm.SelectedOption = vm.Options.First(o => o.Label == "Aggressive");

        Assert.Equal("Set to Aggressive", Assert.Single(_bar.Items).ActionText);
        Assert.Equal("Aggressive", vm.SelectedOption!.Label);
        Assert.Empty(power.Writes);
        Assert.Empty(_confirm.SingleRequests);

        _bar.DiscardAll();

        Assert.Equal("Enabled", vm.SelectedOption!.Label);
    }

    [Fact]
    public async Task An_nvidia_switch_queues()
    {
        using var temp = new TempDir();
        var service = new FakeNvidiaSettingsService { IsAvailable = true };
        var backup = new NvidiaBackupStore(Path.Combine(temp.Path, "nvidia.json"));
        var setting = new NvidiaSetting
        {
            Id = "power-management", Name = "Power management", SettingId = 0x1057EB71, RecommendedValue = 1, DefaultValue = 0,
            Risk = RiskLevel.Low,
        };
        var row = new NvidiaSettingViewModel(setting, service, backup, _confirm, _log.Add, _bar);

        row.ToggleCommand.Execute(null);

        PendingItemViewModel item = Assert.Single(_bar.Items);
        Assert.Equal("Apply", item.ActionText);
        Assert.Equal("nvidia|" + setting.SettingId, item.Key);
        Assert.True(row.IsSwitchOn);
        Assert.Equal("will apply", row.StatusText);
        Assert.Empty(_confirm.SingleRequests);
        Assert.Empty(service.Applied);

        await _bar.ApplyAllAsync();

        Assert.Contains((setting.SettingId, 1u), service.Applied);
        Assert.True(row.IsApplied);
        Assert.False(row.IsPending);
        Assert.Empty(_confirm.SingleRequests);
    }

    [Fact]
    public async Task A_queued_tweak_whose_engine_throws_counts_as_failed_and_is_logged_once()
    {
        var log = new List<string>();
        var bar = new PendingChangesViewModel(log.Add);
        _engine.ThrowOnApply = true;
        var row = new TweakViewModel(Registry(), _engine, _confirm, log.Add, pending: bar);

        await row.ToggleAsync();
        await bar.ApplyAllAsync();

        Assert.Equal("Applied 0 changes; 1 failed", log.Last());
        Assert.Single(log, l => l.Contains("ERROR"));
        Assert.False(row.IsSwitchOn);
        Assert.False(row.IsApplied);
    }

    [Fact]
    public async Task The_switch_stays_on_while_a_queued_apply_runs()
    {
        var engine = new GatedTweakEngine();
        var row = new TweakViewModel(Registry(), engine, _confirm, _log.Add, pending: _bar);
        await row.ToggleAsync();
        var offWhileRunning = new List<bool>();
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TweakViewModel.IsSwitchOn) && row.IsBusy && !row.IsSwitchOn) offWhileRunning.Add(true);
        };

        Task applying = _bar.ApplyAllAsync();
        Assert.True(engine.Entered.Wait(TimeSpan.FromSeconds(5)));

        Assert.False(row.IsPending);
        Assert.True(row.IsBusy);
        Assert.True(row.IsSwitchOn);

        engine.Release.Set();
        await applying;

        Assert.True(row.IsSwitchOn);
        Assert.True(row.IsApplied);
        Assert.Empty(offWhileRunning);
    }

    [Fact]
    public async Task A_queued_power_write_that_went_stale_counts_as_failed()
    {
        var log = new List<string>();
        var bar = new PendingChangesViewModel(log.Add);
        var power = new FakePowerSettings();
        power.Values[K(CpMin)] = 10;
        var vm = new PowerValueViewModel(Slider(), power, new FakePowerBackup(), _confirm, _log.Add, bar);

        vm.Number = 60;
        power.Scheme = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        await bar.ApplyAllAsync();

        Assert.Empty(power.Writes);
        Assert.Equal("Applied 0 changes; 1 failed", log.Last());
        Assert.DoesNotContain(log, l => l.Contains("ERROR"));
        Assert.Contains("power plan changed", vm.Message);
    }

    [Fact]
    public async Task A_queued_apply_leaves_the_bar_when_a_refresh_finds_the_row_already_applied()
    {
        TweakViewModel row = Row(Registry(), _bar);
        await row.ToggleAsync();

        _engine.Overrides["t"] = TweakState.Applied;
        row.RefreshState();

        Assert.Empty(_bar.Items);
        Assert.False(row.IsPending);
        Assert.True(row.IsSwitchOn);
        Assert.Equal("applied", row.StatusText);
    }

    [Fact]
    public async Task A_queued_apply_on_a_row_that_is_already_applied_is_skipped_not_reapplied()
    {
        var log = new List<string>();
        var bar = new PendingChangesViewModel(log.Add);
        var row = new TweakViewModel(Registry(), _engine, _confirm, log.Add, pending: bar);
        await row.ToggleAsync();

        _engine.Overrides["t"] = TweakState.Applied;
        await bar.ApplyAllAsync();

        Assert.DoesNotContain("t", _engine.Applied);
        Assert.Contains("Skipped: Tweak t (already applied)", log);
        Assert.Equal("Applied 1 change", log.Last());
        Assert.True(row.IsApplied);
        Assert.True(row.IsSwitchOn);
    }

    [Fact]
    public async Task A_queued_revert_keeps_the_switch_off_when_the_live_state_moves()
    {
        _engine.Overrides["t"] = TweakState.Applied;
        TweakViewModel row = Row(Registry(), _bar);
        await row.ToggleAsync();
        Assert.False(row.IsSwitchOn);

        _engine.Overrides["t"] = TweakState.NotApplied;
        Assert.False(row.IsSwitchOn);
        Assert.Equal("will revert", row.StatusText);
        _engine.Overrides["t"] = TweakState.Partial;
        row.RefreshState();
        Assert.True(row.IsPending);
        Assert.False(row.IsSwitchOn);
        Assert.Equal("will revert", row.StatusText);

        _engine.Overrides["t"] = TweakState.NotApplied;
        await _bar.ApplyAllAsync();

        Assert.Empty(_engine.Reverted);
        Assert.Contains("Skipped: Tweak t (already off)", _log);
        Assert.False(row.IsSwitchOn);
    }

    [Fact]
    public async Task A_queued_nvidia_apply_on_a_setting_already_applied_is_skipped()
    {
        using var temp = new TempDir();
        var log = new List<string>();
        var bar = new PendingChangesViewModel(log.Add);
        var service = new FakeNvidiaSettingsService { IsAvailable = true };
        var setting = new NvidiaSetting
        {
            Id = "power-management", Name = "Power management", SettingId = 0x1057EB71, RecommendedValue = 1, DefaultValue = 0,
            Risk = RiskLevel.Low,
        };
        var row = new NvidiaSettingViewModel(setting, service, new NvidiaBackupStore(Path.Combine(temp.Path, "nvidia.json")),
            _confirm, log.Add, bar);
        row.ToggleCommand.Execute(null);

        service.Values[setting.SettingId] = 1;
        await bar.ApplyAllAsync();

        Assert.Empty(service.Applied);
        Assert.Contains("Skipped: Power management (already applied)", log);
        Assert.Equal("Applied 1 change", log.Last());
        Assert.True(row.IsApplied);
        Assert.True(row.IsSwitchOn);
    }

    [Fact]
    public void A_queued_nvidia_change_leaves_the_bar_when_a_refresh_finds_it_at_its_target()
    {
        using var temp = new TempDir();
        var service = new FakeNvidiaSettingsService { IsAvailable = true };
        var setting = new NvidiaSetting { Id = "pm", Name = "Power management", SettingId = 7, RecommendedValue = 1, DefaultValue = 0 };
        var row = new NvidiaSettingViewModel(setting, service, new NvidiaBackupStore(Path.Combine(temp.Path, "nvidia.json")),
            _confirm, _log.Add, _bar);
        row.ToggleCommand.Execute(null);

        row.SetCurrent(1);

        Assert.Empty(_bar.Items);
        Assert.True(row.IsSwitchOn);
        Assert.Equal("applied", row.StatusText);
    }

    [Fact]
    public async Task A_queued_nvidia_change_logs_why_when_the_gpu_is_gone()
    {
        using var temp = new TempDir();
        var log = new List<string>();
        var bar = new PendingChangesViewModel(log.Add);
        var service = new FakeNvidiaSettingsService { IsAvailable = true };
        var setting = new NvidiaSetting { Id = "pm", Name = "Power management", SettingId = 7, RecommendedValue = 1, DefaultValue = 0 };
        var row = new NvidiaSettingViewModel(setting, service, new NvidiaBackupStore(Path.Combine(temp.Path, "nvidia.json")),
            _confirm, log.Add, bar);
        row.ToggleCommand.Execute(null);

        service.IsAvailable = false;
        await bar.ApplyAllAsync();

        Assert.Contains("NVIDIA GPU not available: Power management", log);
        Assert.Equal("Applied 0 changes; 1 failed", log.Last());
        Assert.Empty(service.Applied);
    }

    [Fact]
    public async Task A_queued_power_write_keeps_the_plan_it_was_staged_on()
    {
        var log = new List<string>();
        var bar = new PendingChangesViewModel(log.Add);
        var power = new FakePowerSettings();
        var other = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        power.Values[K(CpMin)] = 10;
        power.Values[PowerKeys.Key(other, new Guid(Sub), new Guid(CpMin))] = 20;
        var vm = new PowerValueViewModel(Slider(), power, new FakePowerBackup(), _confirm, _log.Add, bar);

        vm.Number = 60;
        power.Scheme = other;
        vm.Refresh();
        await bar.ApplyAllAsync();

        Assert.Empty(power.Writes);
        Assert.Equal("Applied 0 changes; 1 failed", log.Last());
        Assert.Contains("power plan changed", vm.Message);
        Assert.Equal(20, vm.Number);
    }

    private const string AppsJson = """
    [
      { "id": "bulk-app-removal", "name": "Remove Preinstalled Apps", "category": "Debloat", "description": "Removes apps.", "type": "batch", "risk": "medium", "requiresRestart": false, "runOnce": true, "applyScript": "echo removed" }
    ]
    """;

    private async Task<(MainViewModel Main, TweakViewModel Row, FakeAppxService Appx)> AppsMain()
    {
        var appx = new FakeAppxService();
        foreach (string p in new[] { "Microsoft.BingNews", "Microsoft.Copilot", "Clipchamp.Clipchamp", "Microsoft.YourPhone" })
            appx.Installed.Add(p);
        using var temp = new TempDir();
        string path = Path.Combine(temp.Path, "catalog.json");
        File.WriteAllText(path, AppsJson);
        var catalog = new CatalogService(new FileCatalogSource(path));
        var main = new MainViewModel(catalog, _engine, _confirm, pending: _bar, appx: appx);
        TweakViewModel row = main.Groups.SelectMany(g => g.Tweaks).Single(t => t.Id == "bulk-app-removal");
        Assert.True(row.HasApps);
        await row.Apps!.LoadAsync();
        return (main, row, appx);
    }

    [Fact]
    public async Task The_app_removal_row_queues_the_chosen_apps_and_removes_them_on_apply()
    {
        (MainViewModel main, TweakViewModel row, FakeAppxService appx) = await AppsMain();
        Assert.Equal(3, row.Apps!.SelectedCount);
        Assert.True(row.CanRun);

        await row.RunAsync();

        PendingItemViewModel item = Assert.Single(_bar.Items);
        Assert.Equal("Remove Preinstalled Apps (3 apps)", item.Name);
        Assert.StartsWith("Removes: ", item.Description);
        foreach (RemovableAppRowViewModel app in row.Apps.Selected) Assert.Contains(app.Name, item.Description);
        Assert.True(item.IsIrreversible);
        Assert.Empty(_confirm.SingleRequests);

        await _bar.ApplyAllAsync();

        Assert.Equal(new[] { "Clipchamp.Clipchamp", "Microsoft.BingNews", "Microsoft.Copilot" }, appx.Removed.OrderBy(p => p, StringComparer.Ordinal));
        Assert.DoesNotContain("bulk-app-removal", _engine.Applied);
        Assert.Contains(main.Log, l => l.Contains("Running Remove Preinstalled Apps"));
        Assert.Contains(main.Log, l => l.Contains("Removed 3 of 3 apps"));
        Assert.Equal(0, row.Apps.SelectedCount);
        Assert.False(row.CanRun);
    }

    private async Task<string> ApplyRemovalWithOneAppGone(bool clipchampFails)
    {
        var log = new List<string>();
        var bar = new PendingChangesViewModel(log.Add);
        var appx = new FakeAppxService();
        appx.Installed.Add("Microsoft.BingNews");
        appx.Installed.Add("Clipchamp.Clipchamp");
        var apps = new AppRemovalViewModel(appx, log.Add);
        await apps.LoadAsync();
        var tweak = new Tweak { Id = "bulk-app-removal", Name = "Remove Preinstalled Apps", Type = TweakType.Batch, RunOnce = true };
        var row = new TweakViewModel(tweak, _engine, _confirm, log.Add, pending: bar, apps: apps);
        await row.RunAsync();
        appx.Installed.Remove("Microsoft.BingNews");
        if (clipchampFails) appx.FailFor["Clipchamp.Clipchamp"] = "in use";

        await bar.ApplyAllAsync();

        Assert.Contains("    News — not installed", log);
        return log.Last();
    }

    [Fact]
    public async Task A_queued_removal_with_an_app_already_gone_still_succeeds()
    {
        Assert.Equal("Applied 1 change", await ApplyRemovalWithOneAppGone(clipchampFails: false));
    }

    [Fact]
    public async Task A_queued_removal_with_a_failed_app_counts_as_failed()
    {
        Assert.Equal("Applied 0 changes; 1 failed", await ApplyRemovalWithOneAppGone(clipchampFails: true));
    }

    [Fact]
    public async Task Changing_the_selection_while_queued_restages_and_emptying_it_unstages()
    {
        (_, TweakViewModel row, _) = await AppsMain();
        await row.RunAsync();

        row.Apps!.Rows.Single(r => r.PackageName == "Microsoft.Copilot").IsSelected = false;

        PendingItemViewModel item = Assert.Single(_bar.Items);
        Assert.Equal("Remove Preinstalled Apps (2 apps)", item.Name);
        Assert.DoesNotContain("Copilot", item.Description);
        Assert.True(row.IsPending);

        row.Apps.SelectNoneCommand.Execute(null);

        Assert.Empty(_bar.Items);
        Assert.False(row.IsPending);
        Assert.False(row.CanRun);
    }

    [Fact]
    public async Task The_app_removal_row_with_nothing_chosen_queues_nothing()
    {
        (MainViewModel main, TweakViewModel row, _) = await AppsMain();
        row.Apps!.SelectNoneCommand.Execute(null);

        await row.RunAsync();
        Assert.False(row.StageApply());

        Assert.Empty(_bar.Items);
        Assert.Contains(main.Log, l => l.Contains("Choose at least one app to remove"));
    }

    [Fact]
    public async Task Without_a_bar_the_app_removal_row_asks_then_removes()
    {
        var appx = new FakeAppxService();
        appx.Installed.Add("Microsoft.BingNews");
        var apps = new AppRemovalViewModel(appx, _log.Add);
        await apps.LoadAsync();
        var tweak = new Tweak { Id = "bulk-app-removal", Name = "Remove Preinstalled Apps", Type = TweakType.Batch, RunOnce = true };
        var row = new TweakViewModel(tweak, _engine, _confirm, _log.Add, apps: apps);

        await row.RunAsync();

        Assert.Single(_confirm.SingleRequests);
        Assert.Equal(new[] { "Microsoft.BingNews" }, appx.Removed);
        Assert.Empty(_engine.Applied);
        Assert.Equal("Removed 1 of 1 app", _log.Last());
    }

    [Fact]
    public async Task Logs_go_to_the_shared_activity_log()
    {
        var activity = new ActivityLog(null, () => T0);
        MainViewModel main = Main(bar: _bar, activity: activity);
        TweakViewModel alpha = main.Groups.SelectMany(g => g.Tweaks).First(t => t.Id == "a");

        await alpha.ToggleAsync();
        await _bar.ApplyAllAsync();

        Assert.Same(activity.Lines, main.Log);
        Assert.Contains(activity.Lines, l => l.Contains("[Tweaks] Applied: Alpha"));
    }
}
