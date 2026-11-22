using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tweaker.Core.Catalog;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class AppRemovalViewModelTests
{
    private readonly FakeAppxService _appx = new();
    private readonly List<string> _log = new();

    private AppRemovalViewModel Vm() => new(_appx, _log.Add);

    private static string NameOf(string package) => RemovableApps.All.Single(a => a.PackageName == package).Name;

    private static RemovableAppRowViewModel Row(AppRemovalViewModel vm, string package) => vm.Rows.Single(r => r.PackageName == package);

    private void InstallFour()
    {
        foreach (string p in new[] { "Microsoft.BingNews", "Microsoft.Copilot", "Microsoft.YourPhone", "Clipchamp.Clipchamp" })
            _appx.Installed.Add(p);
    }

    [Fact]
    public void Rows_are_sorted_by_impact_then_name_and_start_unselected()
    {
        AppRemovalViewModel vm = Vm();

        Assert.Equal(RemovableApps.All.Count, vm.Rows.Count);
        List<RemovableAppRowViewModel> sorted = vm.Rows.OrderBy(r => r.Impact).ThenBy(r => r.Name, System.StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted.Select(r => r.PackageName), vm.Rows.Select(r => r.PackageName));
        Assert.All(vm.Rows, r => Assert.False(r.IsInstalled));
        Assert.All(vm.Rows, r => Assert.False(r.IsSelected));
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public async Task Load_marks_installed_rows_and_selects_the_recommended_ones()
    {
        InstallFour();
        AppRemovalViewModel vm = Vm();

        await vm.LoadAsync();

        foreach (string p in new[] { "Microsoft.BingNews", "Microsoft.Copilot", "Microsoft.YourPhone", "Clipchamp.Clipchamp" })
            Assert.True(Row(vm, p).IsInstalled, p);
        Assert.Equal(4, vm.Rows.Count(r => r.IsInstalled));
        Assert.True(Row(vm, "Microsoft.BingNews").IsSelected);
        Assert.True(Row(vm, "Microsoft.Copilot").IsSelected);
        Assert.True(Row(vm, "Clipchamp.Clipchamp").IsSelected);
        Assert.False(Row(vm, "Microsoft.YourPhone").IsSelected);
        Assert.Equal(3, vm.SelectedCount);
        Assert.Equal("3 of 4 installed apps selected", vm.Summary);
        Assert.Equal(Impact.High, vm.Rows[0].Impact);
        Assert.False(vm.IsLoading);
        Assert.False(vm.HasLoadError);
    }

    [Fact]
    public async Task A_row_that_is_not_installed_cannot_be_selected()
    {
        InstallFour();
        AppRemovalViewModel vm = Vm();
        await vm.LoadAsync();
        RemovableAppRowViewModel skype = Row(vm, "Microsoft.SkypeApp");

        skype.IsSelected = true;

        Assert.False(skype.IsSelected);
        Assert.False(skype.CanSelect);

        vm.SelectAllCommand.Execute(null);

        Assert.Equal(4, vm.SelectedCount);
        Assert.All(vm.Rows, r => Assert.Equal(r.IsInstalled, r.IsSelected));

        vm.SelectNoneCommand.Execute(null);
        Assert.Equal(0, vm.SelectedCount);

        vm.SelectRecommendedCommand.Execute(null);
        Assert.Equal(3, vm.SelectedCount);
        Assert.False(Row(vm, "Microsoft.YourPhone").IsSelected);
    }

    [Fact]
    public async Task Every_selection_change_raises_SelectionChanged()
    {
        InstallFour();
        AppRemovalViewModel vm = Vm();
        await vm.LoadAsync();
        int raised = 0;
        vm.SelectionChanged += (_, _) => raised++;

        Row(vm, "Microsoft.YourPhone").IsSelected = true;
        vm.SelectNoneCommand.Execute(null);

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task A_failed_detection_sets_the_load_error()
    {
        _appx.DetectFails = true;
        AppRemovalViewModel vm = Vm();

        await vm.LoadAsync();

        Assert.Equal("Couldn't list the installed apps.", vm.LoadError);
        Assert.True(vm.HasLoadError);
        Assert.All(vm.Rows, r => Assert.False(r.IsInstalled));
        Assert.Equal("No removable apps found", vm.Summary);
    }

    [Fact]
    public async Task Removing_logs_one_line_per_app_and_reloads()
    {
        InstallFour();
        _appx.FailFor["Microsoft.Copilot"] = "in use";
        AppRemovalViewModel vm = Vm();
        await vm.LoadAsync();

        int removed = await vm.RemoveSelectedAsync();

        Assert.Equal(2, removed);
        Assert.Equal("Running Remove Preinstalled Apps", _log[0]);
        Assert.Contains("    " + NameOf("Microsoft.BingNews") + " — removed", _log);
        Assert.Contains("    " + NameOf("Clipchamp.Clipchamp") + " — removed", _log);
        Assert.Contains("    Copilot — failed: in use", _log);
        Assert.Equal("Removed 2 of 3 apps", _log.Last());
        Assert.Equal(5, _log.Count);

        Assert.False(Row(vm, "Microsoft.BingNews").IsInstalled);
        Assert.False(Row(vm, "Microsoft.BingNews").IsSelected);
        Assert.False(Row(vm, "Clipchamp.Clipchamp").IsInstalled);
        Assert.True(Row(vm, "Microsoft.Copilot").IsInstalled);
        Assert.True(Row(vm, "Microsoft.Copilot").IsSelected);
        Assert.False(Row(vm, "Microsoft.YourPhone").IsSelected);
        Assert.Equal(1, vm.SelectedCount);
    }

    [Fact]
    public async Task All_apps_go_in_one_removal_call()
    {
        InstallFour();
        AppRemovalViewModel vm = Vm();
        await vm.LoadAsync();

        await vm.RemoveSelectedAsync();

        Assert.Equal(1, _appx.RemoveManyCalls);
        Assert.Equal(0, vm.LastFailedCount);
    }

    [Fact]
    public async Task An_app_already_gone_is_logged_not_installed_and_is_not_a_failure()
    {
        InstallFour();
        _appx.Installed.Add("Microsoft.SkypeApp");
        AppRemovalViewModel vm = Vm();
        await vm.LoadAsync();
        vm.SelectNoneCommand.Execute(null);
        Row(vm, "Microsoft.SkypeApp").IsSelected = true;
        Row(vm, "Clipchamp.Clipchamp").IsSelected = true;
        _appx.Installed.Remove("Microsoft.SkypeApp");

        int removed = await vm.RemoveSelectedAsync();

        Assert.Equal(1, removed);
        Assert.Contains("    " + NameOf("Microsoft.SkypeApp") + " — not installed", _log);
        Assert.Equal("    Skype — not installed", _log.Single(l => l.Contains("not installed")));
        Assert.Contains("Removed 1 of 2 apps", _log);
        Assert.Equal(0, vm.LastFailedCount);
        Assert.False(Row(vm, "Microsoft.SkypeApp").IsInstalled);
    }

    [Fact]
    public async Task A_failed_app_is_counted()
    {
        InstallFour();
        _appx.FailFor["Clipchamp.Clipchamp"] = "in use";
        AppRemovalViewModel vm = Vm();
        await vm.LoadAsync();

        await vm.RemoveSelectedAsync();

        Assert.Equal(1, vm.LastFailedCount);
    }

    [Fact]
    public async Task The_new_Teams_and_Outlook_start_unticked()
    {
        _appx.Installed.Add("MSTeams");
        _appx.Installed.Add("Microsoft.OutlookForWindows");
        _appx.Installed.Add("MicrosoftTeams");
        AppRemovalViewModel vm = Vm();

        await vm.LoadAsync();

        Assert.False(Row(vm, "MSTeams").IsSelected);
        Assert.False(Row(vm, "Microsoft.OutlookForWindows").IsSelected);
        Assert.True(Row(vm, "MicrosoftTeams").IsSelected);
    }

    [Fact]
    public async Task Removing_with_nothing_selected_logs_and_calls_nothing()
    {
        InstallFour();
        AppRemovalViewModel vm = Vm();
        await vm.LoadAsync();
        vm.SelectNoneCommand.Execute(null);

        int removed = await vm.RemoveSelectedAsync();

        Assert.Equal(0, removed);
        Assert.Equal(new[] { "Remove Preinstalled Apps: no apps selected" }, _log);
        Assert.Empty(_appx.Removed);
    }

    [Fact]
    public void The_checklist_toggles_open()
    {
        AppRemovalViewModel vm = Vm();
        Assert.False(vm.IsExpanded);

        vm.ToggleExpandedCommand.Execute(null);

        Assert.True(vm.IsExpanded);
    }
}
