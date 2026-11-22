using System.Threading.Tasks;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class StorageViewModelTests
{
    [Fact]
    public async Task Refresh_builds_drive_and_volume_rows()
    {
        var storage = new FakeStorageDiagnostic
        {
            Next = new StorageResult(
                new[] { new DriveHealth("Samsung SSD 980", DriveKind.Ssd, SmartHealth.Ok, 931.5) },
                new[] { new VolumeUsage("C:", 233.0, 500.0) }),
        };
        var vm = new StorageViewModel(storage);

        await vm.RefreshAsync();

        Assert.Single(vm.Drives);
        Assert.Equal("Samsung SSD 980", vm.Drives[0].Model);
        Assert.Equal("SSD", vm.Drives[0].Kind);
        Assert.Equal("Healthy", vm.Drives[0].Smart);
        Assert.Equal("931.5 GB", vm.Drives[0].Capacity);
        Assert.Single(vm.Volumes);
        Assert.Equal("C:", vm.Volumes[0].Letter);
        Assert.Equal("267.0 / 500.0 GB", vm.Volumes[0].Usage);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task An_empty_result_after_a_full_one_clears_the_rows()
    {
        var storage = new FakeStorageDiagnostic
        {
            Next = new StorageResult(
                new[] { new DriveHealth("Samsung SSD 980", DriveKind.Ssd, SmartHealth.Ok, 931.5) },
                new[] { new VolumeUsage("C:", 233.0, 500.0) }),
        };
        var vm = new StorageViewModel(storage);
        await vm.RefreshAsync();

        storage.Next = StorageResult.Empty;
        await vm.RefreshAsync();

        Assert.Empty(vm.Drives);
        Assert.Empty(vm.Volumes);
    }
}
