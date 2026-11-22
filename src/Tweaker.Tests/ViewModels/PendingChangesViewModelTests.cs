using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tweaker.Core.Models;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class PendingChangesViewModelTests
{
    private readonly List<string> _ran = new();
    private readonly List<string> _discarded = new();
    private readonly List<string> _log = new();

    private PendingChange Change(string key, string action = "Apply", RiskLevel risk = RiskLevel.Low, bool restart = false,
        string? warning = null, bool fail = false) =>
        new(key, "Tweak " + key, action, "System", risk, restart, "desc", "benefit", "downsides", warning,
            () => { if (fail) throw new InvalidOperationException("boom"); _ran.Add(key); return Task.CompletedTask; },
            () => _discarded.Add(key));

    private PendingChangesViewModel Build() => new(_log.Add);

    [Fact]
    public void Staging_queues_changes_and_restaging_a_key_replaces_it()
    {
        PendingChangesViewModel vm = Build();
        int changed = 0;
        vm.Changed += (_, _) => changed++;

        vm.Stage(Change("a"));
        vm.Stage(Change("b", restart: true));
        vm.Stage(Change("a", action: "Revert"));

        Assert.Equal(new[] { "a", "b" }, vm.Items.Select(i => i.Key));
        Assert.Equal("Revert", vm.Items[0].ActionText);
        Assert.True(vm.IsPending("b"));
        Assert.True(vm.HasItems);
        Assert.Equal("2 changes queued · 1 needs a restart", vm.Summary);
        Assert.Equal(3, changed);
    }

    [Fact]
    public async Task Apply_runs_every_change_in_order_and_empties_the_bar()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a"));
        vm.Stage(Change("b"));

        await vm.ApplyAllAsync();

        Assert.Equal(new[] { "a", "b" }, _ran);
        Assert.Empty(vm.Items);
        Assert.False(vm.IsApplying);
        Assert.Empty(_discarded);
        Assert.Equal("Applied 2 changes", _log.Last());
    }

    [Fact]
    public async Task A_failing_change_is_logged_and_the_rest_still_run()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a", fail: true));
        vm.Stage(Change("b"));

        await vm.ApplyAllAsync();

        Assert.Equal(new[] { "b" }, _ran);
        Assert.Contains(_log, l => l.StartsWith("ERROR apply Tweak a: boom"));
        Assert.Equal("Applied 1 change; 1 failed", _log.Last());
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task A_change_that_throws_the_failed_marker_counts_as_failed_without_a_second_error_line()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(new PendingChange("a", "Tweak a", "Apply", "System", RiskLevel.Low, false, "desc", "benefit", "downsides", null,
            () => throw new PendingChangeFailedException(), () => _discarded.Add("a")));
        vm.Stage(Change("b"));

        await vm.ApplyAllAsync();

        Assert.Equal(new[] { "b" }, _ran);
        Assert.DoesNotContain(_log, l => l.Contains("ERROR"));
        Assert.Equal("Applied 1 change; 1 failed", _log.Last());
        Assert.Empty(_discarded);
    }

    [Fact]
    public void Revert_discards_everything_and_snaps_rows_back()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a"));
        vm.Stage(Change("b"));

        vm.DiscardCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.Equal(new[] { "a", "b" }, _discarded);
        Assert.Empty(_ran);
        Assert.Equal(new[] { "Reverted: Tweak a (queued apply not applied)", "Reverted: Tweak b (queued apply not applied)",
            "Reverted 2 queued changes" }, _log);
    }

    [Fact]
    public void Removing_one_item_logs_it_as_reverted()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a", action: "Revert"));

        vm.RemoveCommand.Execute(vm.Items[0]);

        Assert.Equal(new[] { "Reverted: Tweak a (queued revert not applied)" }, _log);
    }

    [Fact]
    public void Unstage_calls_discard_but_withdraw_does_not()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a"));
        vm.Stage(Change("b"));

        vm.Unstage("a");
        vm.Withdraw("b");

        Assert.Empty(vm.Items);
        Assert.Equal(new[] { "a" }, _discarded);
    }

    [Fact]
    public void Removing_one_item_from_the_bar_discards_it()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a"));
        vm.Stage(Change("b"));

        vm.RemoveCommand.Execute(vm.Items[0]);

        Assert.Equal(new[] { "b" }, vm.Items.Select(i => i.Key));
        Assert.Equal(new[] { "a" }, _discarded);
    }

    [Fact]
    public void Warnings_show_without_expanding_and_show_all_expands_every_item()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a", warning: "Breaks Xbox controllers."));
        vm.Stage(Change("b", risk: RiskLevel.High));
        vm.Stage(Change("c"));

        Assert.Equal((true, "Breaks Xbox controllers."), (vm.Items[0].HasWarning, vm.Items[0].WarningText));
        Assert.True(vm.Items[1].HasWarning);
        Assert.False(vm.Items[2].HasWarning);

        vm.ToggleAllDetailsCommand.Execute(null);
        Assert.All(vm.Items, i => Assert.True(i.IsExpanded));
        vm.Stage(Change("d"));
        Assert.True(vm.Items.Last().IsExpanded);
        vm.Items[0].ToggleCommand.Execute(null);
        Assert.False(vm.Items[0].IsExpanded);
    }

    [Fact]
    public void Restaging_a_key_keeps_its_expanded_state()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a"));
        vm.Stage(Change("b"));
        vm.Items[0].ToggleCommand.Execute(null);
        Assert.True(vm.Items[0].IsExpanded);

        vm.Stage(Change("a", action: "Revert"));
        vm.Stage(Change("b", action: "Revert"));

        Assert.Equal("Revert", vm.Items[0].ActionText);
        Assert.True(vm.Items[0].IsExpanded);
        Assert.False(vm.Items[1].IsExpanded);
    }

    [Fact]
    public async Task A_key_restaged_during_apply_runs_its_newest_change_and_new_keys_wait()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(new PendingChange("a", "A", "Apply", "System", RiskLevel.Low, false, "", "", "", null,
            () =>
            {
                _ran.Add("a");
                vm.Stage(Change("b", action: "Revert"));
                vm.Stage(Change("c"));
                return Task.CompletedTask;
            }, () => { }));
        vm.Stage(new PendingChange("b", "B", "Apply", "System", RiskLevel.Low, false, "", "", "", null,
            () => { _ran.Add("stale b"); return Task.CompletedTask; }, () => { }));

        await vm.ApplyAllAsync();

        Assert.Equal(new[] { "a", "b" }, _ran);
        Assert.Equal(new[] { "c" }, vm.Items.Select(i => i.Key));
    }

    [Fact]
    public async Task While_applying_the_bar_is_locked()
    {
        var gate = new TaskCompletionSource();
        PendingChangesViewModel vm = Build();
        vm.Stage(new PendingChange("slow", "Slow", "Apply", "System", RiskLevel.Low, false, "", "", "", null,
            () => gate.Task, () => _discarded.Add("slow")));
        vm.Stage(Change("b"));

        Task applying = vm.ApplyAllAsync();
        Assert.True(vm.IsApplying);
        Assert.False(vm.CanEdit);
        Assert.Equal("Applying 1 of 2…", vm.Progress);
        vm.DiscardAll();
        Assert.Empty(_discarded);

        gate.SetResult();
        await applying;
        Assert.Equal(new[] { "b" }, _ran);
        Assert.True(vm.CanEdit);
    }

    [Fact]
    public async Task A_change_unstaged_while_applying_never_runs_and_its_row_snaps_back()
    {
        var gate = new TaskCompletionSource();
        PendingChangesViewModel vm = Build();
        vm.Stage(new PendingChange("slow", "Slow", "Apply", "System", RiskLevel.Low, false, "", "", "", null,
            () => gate.Task, () => _discarded.Add("slow")));
        vm.Stage(Change("b"));
        vm.Stage(Change("c"));

        Task applying = vm.ApplyAllAsync();
        vm.Unstage("c");
        Assert.Equal(new[] { "c" }, _discarded);
        Assert.False(vm.IsPending("c"));

        gate.SetResult();
        await applying;
        Assert.Equal(new[] { "b" }, _ran);
        Assert.Equal("Applied 2 changes", _log.Last());
    }

    [Fact]
    public async Task The_bar_stays_visible_while_its_last_item_runs()
    {
        var gate = new TaskCompletionSource();
        PendingChangesViewModel vm = Build();
        vm.Stage(new PendingChange("slow", "Slow", "Apply", "System", RiskLevel.Low, false, "", "", "", null,
            () => gate.Task, () => { }));
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        Assert.True(vm.IsVisible);

        Task applying = vm.ApplyAllAsync();
        Assert.False(vm.HasItems);
        Assert.True(vm.IsVisible);

        gate.SetResult();
        await applying;
        Assert.False(vm.IsVisible);
        Assert.Contains(nameof(PendingChangesViewModel.IsVisible), raised);
    }

    [Fact]
    public void A_run_once_change_is_marked_irreversible()
    {
        PendingChangesViewModel vm = Build();
        vm.Stage(Change("a"));
        vm.Stage(new PendingChange("r", "Clean", "Run", "System", RiskLevel.Low, false, "", "", "", null,
            () => Task.CompletedTask, () => { }, RunOnce: true));

        Assert.False(vm.Items[0].IsIrreversible);
        Assert.True(vm.Items[1].IsIrreversible);
    }
}
