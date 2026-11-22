using System;
using System.Collections.Generic;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class FreedResourcesViewModelTests
{
    [Fact]
    public void Nothing_freed_shows_no_text()
    {
        var vm = new FreedResourcesViewModel();

        Assert.Equal(0, vm.TotalMb);
        Assert.Equal("", vm.TotalText);
        Assert.False(vm.HasTotal);
    }

    [Fact]
    public void Under_a_gigabyte_shows_whole_megabytes()
    {
        var vm = new FreedResourcesViewModel();

        vm.RecordStopped("service|Spooler", 250.4);

        Assert.Equal("Freed: ~250 MB RAM", vm.TotalText);
        Assert.True(vm.HasTotal);
    }

    [Fact]
    public void A_gigabyte_or_more_shows_gigabytes_with_one_decimal()
    {
        var vm = new FreedResourcesViewModel(() => 1036);

        vm.RecordStopped("process|42:7", 500);

        Assert.Equal(1536, vm.TotalMb);
        Assert.Equal("Freed: ~1.5 GB RAM", vm.TotalText);
    }

    [Fact]
    public void Tooltip_splits_the_estimate_from_the_measured_part()
    {
        var vm = new FreedResourcesViewModel(() => 150);

        vm.RecordStopped("service|Spooler", 12.6);

        Assert.Equal("Applied tweaks (estimated): ~150 MB\nStopped or ended this session (measured): ~13 MB", vm.TooltipText);
    }

    [Fact]
    public void Recording_the_same_key_again_replaces_it()
    {
        var vm = new FreedResourcesViewModel();

        vm.RecordStopped("service|Spooler", 40);
        vm.RecordStopped("service|Spooler", 60);

        Assert.Equal(60, vm.MeasuredMb);
    }

    [Fact]
    public void Forget_removes_the_entry()
    {
        var vm = new FreedResourcesViewModel();
        vm.RecordStopped("service|Spooler", 40);
        vm.RecordStopped("process|42:7", 10);

        vm.Forget("service|Spooler");

        Assert.Equal(10, vm.MeasuredMb);
    }

    [Fact]
    public void Refresh_rereads_the_estimate_and_raises_every_property()
    {
        int estimate = 0;
        var vm = new FreedResourcesViewModel(() => estimate);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        estimate = 300;
        vm.Refresh();

        Assert.Equal(300, vm.EstimatedMb);
        Assert.Equal("Freed: ~300 MB RAM", vm.TotalText);
        Assert.Contains(nameof(FreedResourcesViewModel.EstimatedMb), raised);
        Assert.Contains(nameof(FreedResourcesViewModel.MeasuredMb), raised);
        Assert.Contains(nameof(FreedResourcesViewModel.TotalMb), raised);
        Assert.Contains(nameof(FreedResourcesViewModel.TotalText), raised);
        Assert.Contains(nameof(FreedResourcesViewModel.HasTotal), raised);
        Assert.Contains(nameof(FreedResourcesViewModel.TooltipText), raised);
    }

    [Fact]
    public void A_stopped_service_an_applied_estimate_covers_is_not_counted_twice()
    {
        var vm = new FreedResourcesViewModel(() => 100,
            () => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SysMain" });

        vm.RecordStopped("service|sysmain", 80);
        vm.RecordStopped("service|Spooler", 12);
        vm.RecordStopped("process|42:7", 8);

        Assert.Equal(20, vm.MeasuredMb);
        Assert.Equal(120, vm.TotalMb);

        vm.Forget("service|Spooler");
        vm.Forget("process|42:7");
        Assert.Equal(0, vm.MeasuredMb);
        Assert.Equal(100, vm.TotalMb);
    }

    [Fact]
    public void Refresh_rereads_the_covered_services()
    {
        IReadOnlySet<string> covered = new HashSet<string>();
        var vm = new FreedResourcesViewModel(() => 100, () => covered);
        vm.RecordStopped("service|SysMain", 80);
        Assert.Equal(180, vm.TotalMb);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SysMain" };
        vm.Refresh();

        Assert.Equal(100, vm.TotalMb);
        Assert.Contains(nameof(FreedResourcesViewModel.TotalText), raised);
    }

    [Fact]
    public void Recording_raises_the_total()
    {
        var vm = new FreedResourcesViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.RecordStopped("service|Spooler", 40);

        Assert.Contains(nameof(FreedResourcesViewModel.TotalText), raised);
        Assert.Contains(nameof(FreedResourcesViewModel.TooltipText), raised);
    }
}
