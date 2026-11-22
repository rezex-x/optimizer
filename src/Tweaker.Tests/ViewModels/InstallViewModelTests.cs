using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class InstallViewModelTests
{
    [Fact]
    public void Categories_start_with_all_and_include_browsers()
    {
        var vm = new InstallViewModel(new FakeWingetService());
        Assert.Equal("All", vm.Categories[0]);
        Assert.Contains("Browsers", vm.Categories);
    }

    [Fact]
    public void Toggle_selects_and_counts()
    {
        var vm = new InstallViewModel(new FakeWingetService());
        vm.ToggleAppCommand.Execute("Google.Chrome");
        Assert.Equal(1, vm.SelectedCount);
        vm.ToggleAppCommand.Execute("Google.Chrome");
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public void Search_filters_rows()
    {
        var vm = new InstallViewModel(new FakeWingetService());
        vm.SearchText = "steam";
        Assert.Contains(vm.Apps, r => r.WingetId == "Valve.Steam");
        Assert.DoesNotContain(vm.Apps, r => r.WingetId == "Google.Chrome");
    }

    [Fact]
    public void InstallSelected_installs_each_selected_app()
    {
        var winget = new FakeWingetService();
        var vm = new InstallViewModel(winget);
        vm.ToggleAppCommand.Execute("Google.Chrome");
        vm.ToggleAppCommand.Execute("Valve.Steam");

        var logs = new List<string>();
        int ok = vm.InstallSelected(logs.Add);

        Assert.Equal(2, ok);
        Assert.Contains("Google.Chrome", winget.Installed);
        Assert.Contains("Valve.Steam", winget.Installed);
        Assert.Contains(logs, l => l.Contains("Google Chrome"));
    }

    [Fact]
    public void WingetAvailable_reflects_service()
    {
        Assert.False(new InstallViewModel(new FakeWingetService { Available = false }).WingetAvailable);
        Assert.True(new InstallViewModel(new FakeWingetService { Available = true }).WingetAvailable);
    }
}
