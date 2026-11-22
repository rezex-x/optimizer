using Tweaker.Core.Catalog;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class UpdatesViewModelTests
{
    [Fact]
    public void Initial_selection_comes_from_detect_active()
    {
        var vm = new UpdatesViewModel(new FakeWindowsUpdateService { Active = UpdatePreset.SecurityOnly });
        Assert.Equal(UpdatePreset.SecurityOnly, vm.SelectedPreset);
    }

    [Fact]
    public void Warning_shows_only_for_disable_all()
    {
        var vm = new UpdatesViewModel(new FakeWindowsUpdateService());
        vm.SelectedPreset = UpdatePreset.SecurityOnly;
        Assert.False(vm.ShowWarning);
        vm.SelectedPreset = UpdatePreset.DisableAll;
        Assert.True(vm.ShowWarning);
    }

    [Fact]
    public void Apply_calls_service_and_sets_active_label()
    {
        var svc = new FakeWindowsUpdateService();
        var vm = new UpdatesViewModel(svc);
        vm.SelectedPreset = UpdatePreset.DisableAll;
        vm.ApplyCommand.Execute(null);
        Assert.Contains(UpdatePreset.DisableAll, svc.Applied);
        Assert.Contains("Disable", vm.ActiveLabel);
    }

    [Fact]
    public void SelectedRow_tracks_selected_preset()
    {
        var vm = new UpdatesViewModel(new FakeWindowsUpdateService());
        vm.SelectedPreset = UpdatePreset.SecurityOnly;
        Assert.Equal(UpdatePreset.SecurityOnly, vm.SelectedRow?.Value);
        Assert.NotEmpty(vm.SelectedRow!.Details);
    }
}
