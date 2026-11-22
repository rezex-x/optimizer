using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class TweakViewModelTests
{
    private static Tweak T(string id = "t") => new() { Id = id, Name = "N", Type = TweakType.Registry };
    private static Tweak Batch(string id = "b") => new() { Id = id, Name = "N", Type = TweakType.Batch };
    private static FakeConfirmationService Yes() => new() { Result = true };

    [Fact]
    public async Task Toggle_from_off_applies_logs_and_shows_the_applied_label()
    {
        var engine = new FakeTweakEngine();
        var logs = new List<string>();
        var vm = new TweakViewModel(T(), engine, Yes(), s => logs.Add(s));

        Assert.False(vm.IsApplied);
        Assert.Equal("", vm.StatusText);

        await vm.ToggleAsync();

        Assert.Contains("t", engine.Applied);
        Assert.Equal(TweakState.Applied, vm.State);
        Assert.True(vm.IsApplied);
        Assert.Equal("applied", vm.StatusText);
        Assert.Contains(logs, l => l.Contains("Applied"));
    }

    [Fact]
    public async Task Toggle_from_on_reverts_and_clears_the_label()
    {
        var engine = new FakeTweakEngine();
        var logs = new List<string>();
        var vm = new TweakViewModel(T(), engine, Yes(), s => logs.Add(s));
        await vm.ToggleAsync();
        logs.Clear();

        await vm.ToggleAsync();

        Assert.Contains("t", engine.Reverted);
        Assert.Equal(TweakState.NotApplied, vm.State);
        Assert.Equal("", vm.StatusText);
        Assert.Contains(logs, l => l.Contains("Reverted"));
    }

    [Fact]
    public async Task Apply_error_is_caught_logged_and_resyncs_the_switch()
    {
        var logs = new List<string>();
        var vm = new TweakViewModel(T(), new ThrowingEngine(), Yes(), s => logs.Add(s));
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        await vm.ApplyAsync();

        Assert.Contains(logs, l => l.Contains("ERROR"));
        Assert.False(vm.IsApplied);
        Assert.Contains(nameof(vm.IsApplied), raised);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Apply_is_blocked_when_confirmation_is_declined()
    {
        var engine = new FakeTweakEngine();
        var confirm = new FakeConfirmationService { Result = false };
        var vm = new TweakViewModel(T(), engine, confirm, _ => { });

        await vm.ApplyAsync();

        Assert.Empty(engine.Applied);
        Assert.False(vm.IsApplied);
        Assert.Single(confirm.SingleRequests);
    }

    [Fact]
    public async Task Declined_apply_renotifies_IsApplied_so_the_switch_resyncs_off()
    {
        var engine = new FakeTweakEngine();
        var confirm = new FakeConfirmationService { Result = false };
        var vm = new TweakViewModel(T(), engine, confirm, _ => { });
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        await vm.ApplyAsync();

        Assert.False(vm.IsApplied);
        Assert.Contains(nameof(vm.IsApplied), raised);
        Assert.Empty(engine.Applied);
    }

    [Fact]
    public async Task Apply_proceeds_and_passes_copy_when_confirmed()
    {
        var engine = new FakeTweakEngine();
        var confirm = new FakeConfirmationService { Result = true };
        var tweak = new Tweak { Id = "t", Name = "N", Category = "System",
            Type = TweakType.Registry, Benefit = "fps", Downsides = "risk", Warning = "W" };
        var vm = new TweakViewModel(tweak, engine, confirm, _ => { });

        await vm.ApplyAsync();

        Assert.Contains("t", engine.Applied);
        Assert.Equal("fps", confirm.SingleRequests[0].Benefit);
        Assert.Equal("W", confirm.SingleRequests[0].Warning);
    }

    [Fact]
    public async Task Revert_does_not_confirm()
    {
        var engine = new FakeTweakEngine();
        var confirm = new FakeConfirmationService { Result = false };
        var vm = new TweakViewModel(T(), engine, confirm, _ => { });

        await vm.RevertAsync();

        Assert.Contains("t", engine.Reverted);
        Assert.Empty(confirm.SingleRequests);
    }

    [Fact]
    public async Task Script_tweak_is_a_switch_that_applies_and_reverts_through_the_engine()
    {
        var engine = new FakeTweakEngine();
        var vm = new TweakViewModel(Batch("batch1"), engine, Yes(), _ => { });

        Assert.True(vm.ShowSwitch);
        Assert.False(vm.IsApplied);

        await vm.ToggleAsync();
        Assert.Contains("batch1", engine.Applied);
        Assert.True(vm.IsApplied);
        Assert.Equal("applied", vm.StatusText);

        await vm.ToggleAsync();
        Assert.Contains("batch1", engine.Reverted);
        Assert.False(vm.IsApplied);
        Assert.Equal("", vm.StatusText);
    }

    [Fact]
    public void Script_tweak_detected_as_applied_starts_on_with_the_label()
    {
        var engine = new FakeTweakEngine();
        engine.Overrides["b"] = TweakState.Applied;
        var vm = new TweakViewModel(Batch("b"), engine, Yes(), _ => { });

        Assert.True(vm.IsApplied);
        Assert.Equal("applied", vm.StatusText);
    }

    [Fact]
    public async Task Run_once_tweak_has_no_switch_and_Run_confirms_then_runs_without_an_applied_state()
    {
        var engine = new FakeTweakEngine();
        var confirm = new FakeConfirmationService { Result = true };
        var tweak = new Tweak { Id = "r", Name = "R", Type = TweakType.Batch, RunOnce = true };
        var logs = new List<string>();
        var vm = new TweakViewModel(tweak, engine, confirm, s => logs.Add(s));

        Assert.True(vm.IsRunOnce);
        Assert.False(vm.ShowSwitch);

        await vm.RunAsync();

        Assert.Contains("r", engine.Applied);
        Assert.Single(confirm.SingleRequests);
        Assert.False(vm.IsApplied);
        Assert.Equal("", vm.StatusText);
        Assert.Contains(logs, l => l.Contains("Ran"));
    }

    [Fact]
    public async Task While_the_engine_runs_the_switch_is_disabled_labelled_and_ignores_reentry()
    {
        var engine = new GatedEngine();
        var vm = new TweakViewModel(Batch("slow"), engine, Yes(), _ => { });

        Task first = vm.ToggleAsync();
        Assert.True(engine.Entered.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanToggle);
        Assert.Equal("applying…", vm.StatusText);

        await vm.ToggleAsync();
        Assert.Equal(1, engine.ApplyCalls);

        engine.Release.Set();
        await first;

        Assert.False(vm.IsBusy);
        Assert.True(vm.CanToggle);
        Assert.Equal("applied", vm.StatusText);
    }

    [Fact]
    public void RefreshState_rereads_the_engine()
    {
        var engine = new FakeTweakEngine();
        var vm = new TweakViewModel(T(), engine, Yes(), _ => { });
        Assert.False(vm.IsApplied);

        engine.Overrides["t"] = TweakState.Applied;
        vm.RefreshState();

        Assert.True(vm.IsApplied);
        Assert.Equal("applied", vm.StatusText);
    }

    [Fact]
    public async Task RefreshState_is_ignored_while_the_engine_runs()
    {
        var engine = new GatedEngine();
        var vm = new TweakViewModel(Batch("slow"), engine, Yes(), _ => { });

        Task first = vm.ToggleAsync();
        Assert.True(engine.Entered.Wait(TimeSpan.FromSeconds(5)));
        vm.RefreshState();
        Assert.Equal("applying…", vm.StatusText);

        engine.Release.Set();
        await first;
        Assert.Equal("applied", vm.StatusText);
    }

    [Fact]
    public async Task Revert_error_is_caught_logged_and_resyncs_the_switch()
    {
        var logs = new List<string>();
        var vm = new TweakViewModel(T(), new ThrowingEngine(), Yes(), s => logs.Add(s));
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        await vm.RevertAsync();

        Assert.Contains(logs, l => l.Contains("ERROR reverting"));
        Assert.Contains(nameof(vm.IsApplied), raised);
        Assert.False(vm.IsBusy);
    }

    private static Tweak ValueTweak() => new()
    {
        Id = "v", Name = "V", Category = "CPU", Type = TweakType.Value,
        Power = new PowerSettingSpec { Subgroup = "54533251-82be-4824-96c1-47b60b740d00", Setting = "0cc5b647-c1df-4637-891a-dec35c318583", Unit = "%" },
    };

    private static string ValueKey(Tweak t) =>
        PowerKeys.Key(FakePowerSettings.Balanced, new Guid(t.Power!.Subgroup), new Guid(t.Power.Setting));

    [Fact]
    public async Task Value_rows_have_no_switch_and_highlight_when_changed()
    {
        Tweak t = ValueTweak();
        var power = new FakePowerSettings();
        power.Values[ValueKey(t)] = 10;
        var engine = new FakeTweakEngine();
        var value = new PowerValueViewModel(t, power, new FakePowerBackup(), Yes(), _ => { });
        var vm = new TweakViewModel(t, engine, Yes(), _ => { }, value);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.True(vm.IsValue);
        Assert.False(vm.ShowSwitch);
        Assert.Same(value, vm.Value);
        Assert.False(vm.IsApplied);

        value.Number = 60;
        value.ApplyCommand.Execute(null);

        Assert.True(vm.IsApplied);
        Assert.Contains(nameof(TweakViewModel.IsApplied), raised);
        Assert.Equal("", vm.StatusText);

        await vm.ToggleAsync();
        await vm.ApplyAsync();
        Assert.Empty(engine.Applied);
        Assert.Empty(engine.Reverted);
    }

    [Fact]
    public void Refreshing_a_value_row_re_reads_the_setting()
    {
        Tweak t = ValueTweak();
        var power = new FakePowerSettings();
        power.Values[ValueKey(t)] = 10;
        var value = new PowerValueViewModel(t, power, new FakePowerBackup(), Yes(), _ => { });
        var vm = new TweakViewModel(t, new FakeTweakEngine(), Yes(), _ => { }, value);

        power.Values[ValueKey(t)] = 35;
        vm.RefreshState();

        Assert.Equal(35, value.Number);
    }

    [Fact]
    public async Task An_engine_call_reports_the_change_once_it_is_done()
    {
        int calls = 0;
        bool busyDuringCallback = true;
        TweakViewModel? vm = null;
        vm = new TweakViewModel(T(), new FakeTweakEngine(), Yes(), _ => { }, changed: () => { calls++; busyDuringCallback = vm!.IsBusy; });

        await vm.ApplyAsync();

        Assert.Equal(1, calls);
        Assert.False(busyDuringCallback);
    }

    [Fact]
    public async Task A_cancelled_apply_reports_no_change()
    {
        int calls = 0;
        var vm = new TweakViewModel(T(), new FakeTweakEngine(), new FakeConfirmationService { Result = false }, _ => { },
            changed: () => calls++);

        await vm.ApplyAsync();

        Assert.Equal(0, calls);
    }

    private sealed class ThrowingEngine : ITweakEngine
    {
        public TweakState GetState(Tweak tweak) => TweakState.NotApplied;
        public void Apply(Tweak tweak) => throw new InvalidOperationException("boom");
        public void Revert(Tweak tweak) => throw new InvalidOperationException("boom");
    }

    private sealed class GatedEngine : ITweakEngine
    {
        private volatile bool _applied;
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public int ApplyCalls;

        public TweakState GetState(Tweak tweak) => _applied ? TweakState.Applied : TweakState.NotApplied;

        public void Apply(Tweak tweak)
        {
            Interlocked.Increment(ref ApplyCalls);
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            _applied = true;
        }

        public void Revert(Tweak tweak) => _applied = false;
    }
}
