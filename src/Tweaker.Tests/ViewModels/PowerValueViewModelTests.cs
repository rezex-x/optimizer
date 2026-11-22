using System;
using System.Collections.Generic;
using System.Linq;
using Tweaker.Core.Models;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class PowerValueViewModelTests
{
    private const string Sub = "54533251-82be-4824-96c1-47b60b740d00";
    private const string CpMin = "0cc5b647-c1df-4637-891a-dec35c318583";
    private const string Boost = "be337238-0d82-4146-a960-4f3749d470c7";
    private static readonly Guid Balanced = FakePowerSettings.Balanced;
    private static readonly Guid High = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    private readonly FakePowerSettings _power = new();
    private readonly FakePowerBackup _backup = new();
    private readonly FakeConfirmationService _confirm = new() { Result = true };
    private readonly List<string> _log = new();

    private static string K(Guid scheme, string setting) => PowerKeys.Key(scheme, new Guid(Sub), new Guid(setting));

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

    private PowerValueViewModel Build(Tweak tweak) => new(tweak, _power, _backup, _confirm, _log.Add);

    [Fact]
    public void Reads_the_current_and_default_value_of_the_active_plan()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _power.Defaults[K(Balanced, CpMin)] = 5;

        PowerValueViewModel vm = Build(Slider());

        Assert.True(vm.IsSlider);
        Assert.False(vm.IsChoice);
        Assert.True(vm.IsAvailable);
        Assert.Equal(10, vm.Number);
        Assert.Equal("10", vm.NumberText);
        Assert.Equal("Plugged in: 10 % · Windows default 5 %", vm.StatusLine);
        Assert.Equal(("0 %", "100 %"), (vm.MinText, vm.MaxText));
        Assert.False(vm.HasPending);
        Assert.False(vm.IsChanged);
        Assert.False(vm.CanReset);
    }

    [Fact]
    public void A_staged_value_is_written_only_after_apply_and_confirmation()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        PowerValueViewModel vm = Build(Slider());

        vm.Number = 100;
        Assert.True(vm.HasPending);
        Assert.Empty(_power.Writes);

        vm.ApplyCommand.Execute(null);

        Assert.Single(_confirm.SingleRequests);
        Assert.Equal((Balanced, 100u), (_power.Writes.Single().Scheme, _power.Writes.Single().Value));
        Assert.Equal(10u, _backup.Originals[K(Balanced, CpMin)]);
        Assert.True(vm.IsChanged);
        Assert.True(vm.CanReset);
        Assert.False(vm.HasPending);
        Assert.Equal("Plugged in: 100 %", vm.StatusLine);
        Assert.Contains(_log, l => l.Contains("Set Core parking minimum to 100 %"));
    }

    [Fact]
    public void The_number_box_snaps_to_the_range()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        PowerValueViewModel vm = Build(Slider());

        vm.NumberText = "250";
        Assert.Equal(100, vm.Number);
        Assert.Equal("100", vm.NumberText);
        vm.NumberText = "abc";
        Assert.Equal(100, vm.Number);
        vm.Number = -4;
        Assert.Equal(0, vm.Number);
    }

    [Fact]
    public void Cancelling_changes_nothing_and_keeps_the_staged_value()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _confirm.Result = false;
        PowerValueViewModel vm = Build(Slider());

        vm.Number = 50;
        vm.ApplyCommand.Execute(null);

        Assert.Empty(_power.Writes);
        Assert.Empty(_backup.Originals);
        Assert.Equal("Cancelled — nothing changed.", vm.Message);
        Assert.True(vm.HasPending);
    }

    [Fact]
    public void Returning_to_the_original_forgets_it()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        PowerValueViewModel vm = Build(Slider());

        vm.Number = 50;
        vm.ApplyCommand.Execute(null);
        vm.Number = 10;
        vm.ApplyCommand.Execute(null);

        Assert.Equal(2, _power.Writes.Count);
        Assert.Empty(_backup.Originals);
        Assert.False(vm.IsChanged);
    }

    [Fact]
    public void Reset_restores_the_original_without_asking()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        PowerValueViewModel vm = Build(Slider());
        vm.Number = 80;
        vm.ApplyCommand.Execute(null);
        _confirm.SingleRequests.Clear();

        vm.ResetCommand.Execute(null);

        Assert.Empty(_confirm.SingleRequests);
        Assert.Equal(10u, _power.Writes.Last().Value);
        Assert.Empty(_backup.Originals);
        Assert.False(vm.CanReset);
        Assert.Equal(10, vm.Number);
        Assert.Equal("Restored 10 %.", vm.Message);
    }

    [Fact]
    public void A_failed_write_keeps_no_backup_and_says_why()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _power.WriteResult = "Access is denied.";
        PowerValueViewModel vm = Build(Slider());

        vm.Number = 90;
        vm.ApplyCommand.Execute(null);

        Assert.Empty(_backup.Originals);
        Assert.Equal("Couldn't change it: Access is denied.", vm.Message);
        Assert.StartsWith("Plugged in: 10 %", vm.StatusLine);
        Assert.Contains(_log, l => l.StartsWith("ERROR changing Core parking minimum"));
    }

    [Fact]
    public void A_backup_that_cant_be_saved_blocks_the_write()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _backup.FailRemember = true;
        PowerValueViewModel vm = Build(Slider());

        vm.Number = 90;
        vm.ApplyCommand.Execute(null);

        Assert.Empty(_power.Writes);
        Assert.Equal("Couldn't save a backup of the current value — nothing was changed.", vm.Message);
    }

    [Fact]
    public void A_setting_the_pc_lacks_is_unavailable()
    {
        PowerValueViewModel vm = Build(Slider());

        Assert.False(vm.IsAvailable);
        Assert.Equal("Not available on this PC", vm.StatusLine);
        vm.Number = 50;
        Assert.False(vm.HasPending);
        vm.ApplyCommand.Execute(null);
        Assert.Empty(_power.Writes);
    }

    [Fact]
    public void Picking_an_option_writes_it_after_confirming()
    {
        _power.Values[K(Balanced, Boost)] = 1;
        PowerValueViewModel vm = Build(Dropdown());

        Assert.True(vm.IsChoice);
        Assert.Equal("Enabled", vm.SelectedOption!.Label);

        vm.SelectedOption = vm.Options[2];

        Assert.Equal(2u, _power.Writes.Single().Value);
        Assert.Equal("Aggressive", vm.SelectedOption!.Label);
        Assert.Equal(1u, _backup.Originals[K(Balanced, Boost)]);
        Assert.Equal("Plugged in: Aggressive", vm.StatusLine);
    }

    [Fact]
    public void A_cancelled_pick_snaps_back()
    {
        _power.Values[K(Balanced, Boost)] = 1;
        _confirm.Result = false;
        PowerValueViewModel vm = Build(Dropdown());
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedOption = vm.Options[0];

        Assert.Empty(_power.Writes);
        Assert.Equal("Enabled", vm.SelectedOption!.Label);
        Assert.Contains(nameof(PowerValueViewModel.SelectedOption), raised);
    }

    [Fact]
    public void An_unlisted_value_reads_as_custom()
    {
        _power.Values[K(Balanced, Boost)] = 6;
        PowerValueViewModel vm = Build(Dropdown());

        Assert.Null(vm.SelectedOption);
        Assert.Equal("Plugged in: custom (6)", vm.StatusLine);
    }

    [Fact]
    public void Switching_plans_shows_that_plans_value_and_originals()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _power.Values[K(High, CpMin)] = 100;
        PowerValueViewModel vm = Build(Slider());
        vm.Number = 50;
        vm.ApplyCommand.Execute(null);

        _power.Scheme = High;
        vm.Refresh();

        Assert.Equal(100, vm.Number);
        Assert.False(vm.IsChanged);
        Assert.Equal(10u, _backup.Originals[K(Balanced, CpMin)]);
    }

    private const string PlanChanged = "The power plan changed — check the new value and try again.";
    private const string ValueChanged = "The value changed outside this row — check it and try again.";

    [Fact]
    public void A_plan_switch_since_the_last_refresh_writes_nothing()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _power.Values[K(High, CpMin)] = 100;
        PowerValueViewModel vm = Build(Slider());
        vm.Number = 50;

        _power.Scheme = High;
        vm.ApplyCommand.Execute(null);

        Assert.Empty(_confirm.SingleRequests);
        Assert.Empty(_power.Writes);
        Assert.Empty(_backup.Originals);
        Assert.Equal(PlanChanged, vm.Message);
        Assert.Equal(100, vm.Number);
        Assert.StartsWith("Plugged in: 100 %", vm.StatusLine);
    }

    [Fact]
    public void A_plan_switch_while_the_dialog_is_open_writes_nothing()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _power.Values[K(High, CpMin)] = 100;
        var confirm = new CallbackConfirm(() => _power.Scheme = High);
        var vm = new PowerValueViewModel(Slider(), _power, _backup, confirm, _log.Add);
        vm.Number = 50;

        vm.ApplyCommand.Execute(null);

        Assert.Equal(1, confirm.Calls);
        Assert.Empty(_power.Writes);
        Assert.Empty(_backup.Originals);
        Assert.Equal(PlanChanged, vm.Message);
    }

    [Fact]
    public void A_value_changed_outside_the_row_writes_nothing_and_shows_the_new_value()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        PowerValueViewModel vm = Build(Slider());
        vm.Number = 50;

        _power.Values[K(Balanced, CpMin)] = 100;
        vm.ApplyCommand.Execute(null);

        Assert.Empty(_power.Writes);
        Assert.Empty(_backup.Originals);
        Assert.Equal(ValueChanged, vm.Message);
        Assert.Equal(100, vm.Number);
        Assert.Equal("Plugged in: 100 % · " + ValueChanged, vm.StatusLine);
    }

    [Fact]
    public void A_stale_pick_writes_nothing_and_snaps_the_dropdown_to_the_new_value()
    {
        _power.Values[K(Balanced, Boost)] = 1;
        _power.Values[K(High, Boost)] = 2;
        PowerValueViewModel vm = Build(Dropdown());

        _power.Scheme = High;
        vm.SelectedOption = vm.Options[0];

        Assert.Empty(_power.Writes);
        Assert.Equal("Aggressive", vm.SelectedOption!.Label);
        Assert.Equal(PlanChanged, vm.Message);
    }

    [Fact]
    public void A_stale_reset_writes_nothing()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        PowerValueViewModel vm = Build(Slider());
        vm.Number = 80;
        vm.ApplyCommand.Execute(null);

        _power.Values[K(Balanced, CpMin)] = 100;
        vm.ResetCommand.Execute(null);

        Assert.Single(_power.Writes);
        Assert.Equal(10u, _backup.Originals[K(Balanced, CpMin)]);
        Assert.Equal(ValueChanged, vm.Message);
        Assert.Equal(100, vm.Number);
    }

    [Fact]
    public void A_write_that_lands_but_cant_re_apply_the_plan_keeps_the_original()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _power.WriteResult = "The parameter is incorrect.";
        _power.WriteLandsDespiteError = true;
        PowerValueViewModel vm = Build(Slider());

        vm.Number = 90;
        vm.ApplyCommand.Execute(null);

        Assert.Equal(10u, _backup.Originals[K(Balanced, CpMin)]);
        Assert.True(vm.IsChanged);
        Assert.True(vm.CanReset);
        Assert.Equal(90, vm.Number);
        Assert.StartsWith("Saved, but Windows couldn't re-apply the plan", vm.Message);
        Assert.Equal("Saved, but Windows couldn't re-apply the plan (The parameter is incorrect.) — it takes effect the next time the plan is activated.",
            vm.Message);
    }

    [Fact]
    public void A_reset_that_lands_but_cant_re_apply_the_plan_forgets_the_original()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        PowerValueViewModel vm = Build(Slider());
        vm.Number = 90;
        vm.ApplyCommand.Execute(null);
        _power.WriteResult = "The parameter is incorrect.";
        _power.WriteLandsDespiteError = true;

        vm.ResetCommand.Execute(null);

        Assert.Empty(_backup.Originals);
        Assert.Equal(10, vm.Number);
        Assert.StartsWith("Restored 10 %, but Windows couldn't re-apply the plan", vm.Message);
    }

    [Fact]
    public void A_failed_reset_keeps_the_original()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        PowerValueViewModel vm = Build(Slider());
        vm.Number = 90;
        vm.ApplyCommand.Execute(null);
        _power.WriteResult = "Access is denied.";

        vm.ResetCommand.Execute(null);

        Assert.Equal(10u, _backup.Originals[K(Balanced, CpMin)]);
        Assert.True(vm.CanReset);
        Assert.Equal("Couldn't reset it: Access is denied.", vm.Message);
    }

    [Fact]
    public void A_refresh_clears_the_last_result()
    {
        _power.Values[K(Balanced, CpMin)] = 10;
        _confirm.Result = false;
        PowerValueViewModel vm = Build(Slider());
        vm.Number = 50;
        vm.ApplyCommand.Execute(null);
        Assert.Equal("Cancelled — nothing changed.", vm.Message);

        vm.Refresh();

        Assert.Equal("", vm.Message);
        Assert.Equal("Plugged in: 10 %", vm.StatusLine);
    }

    private sealed class CallbackConfirm : IConfirmationService
    {
        private readonly Action _whileOpen;
        public CallbackConfirm(Action whileOpen) => _whileOpen = whileOpen;
        public int Calls { get; private set; }

        public bool Confirm(TweakConfirmRequest request)
        {
            Calls++;
            _whileOpen();
            return true;
        }

        public bool ConfirmBatch(string title, IReadOnlyList<TweakConfirmRequest> items) => true;
    }
}
