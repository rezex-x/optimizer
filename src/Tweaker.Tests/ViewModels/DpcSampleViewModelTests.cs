using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class DpcSampleViewModelTests
{
    [Fact]
    public async Task Sample_formats_the_counters_and_clears_the_running_flag()
    {
        var dpc = new FakeDpcDiagnostic { Next = new DpcResult(1.42, 0.8, 1234, 5678) };
        var vm = new DpcSampleViewModel(dpc);

        await vm.SampleAsync();

        Assert.Equal("1%", vm.DpcTime);
        Assert.Equal("<1%", vm.InterruptTime);
        Assert.Equal("1234 /s", vm.DpcsQueued);
        Assert.Equal("5678 /s", vm.InterruptsRate);
        Assert.False(vm.IsRunning);
        Assert.Equal("Sample", vm.ButtonLabel);
    }

    [Fact]
    public async Task An_empty_sample_shows_dashes()
    {
        var vm = new DpcSampleViewModel(new FakeDpcDiagnostic { Next = DpcResult.Empty });
        await vm.SampleAsync();
        Assert.Equal("—", vm.DpcTime);
        Assert.Equal("—", vm.InterruptTime);
        Assert.Equal("—", vm.DpcsQueued);
        Assert.Equal("—", vm.InterruptsRate);
    }

    [Fact]
    public async Task Sampling_sets_the_running_state_and_ignores_reentry()
    {
        var gated = new GatedDpcDiagnostic();
        var vm = new DpcSampleViewModel(gated);

        Task first = vm.SampleAsync();
        Assert.True(vm.IsRunning);
        Assert.Equal("Sampling…", vm.ButtonLabel);
        Assert.False(vm.SampleCommand.CanExecute(null));

        await vm.SampleAsync();
        Assert.Equal(1, gated.Calls);

        gated.Gate.SetResult(DpcResult.Empty);
        await first;
        Assert.False(vm.IsRunning);
        Assert.Equal("Sample", vm.ButtonLabel);
        Assert.True(vm.SampleCommand.CanExecute(null));
    }
}
