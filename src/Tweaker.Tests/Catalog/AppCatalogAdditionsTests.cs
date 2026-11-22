using System.Linq;
using Tweaker.Core.Catalog;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class AppCatalogAdditionsTests
{
    [Theory]
    [InlineData("WinDirStat.WinDirStat")]
    [InlineData("AntibodySoftware.WizTree")]
    [InlineData("CPUID.CPU-Z")]
    [InlineData("TechPowerUp.GPU-Z")]
    [InlineData("REALiX.HWiNFO")]
    [InlineData("CPUID.HWMonitor")]
    [InlineData("CrystalDewWorld.CrystalDiskInfo")]
    [InlineData("CrystalDewWorld.CrystalDiskMark")]
    [InlineData("Microsoft.Sysinternals.ProcessExplorer")]
    [InlineData("Microsoft.Sysinternals.ProcessMonitor")]
    [InlineData("Microsoft.Sysinternals.Autoruns")]
    [InlineData("Resplendence.LatencyMon")]
    [InlineData("Wagnardsoft.DisplayDriverUninstaller")]
    [InlineData("TechPowerUp.NVCleanstall")]
    [InlineData("Rufus.Rufus")]
    [InlineData("RevoUninstaller.RevoUninstaller")]
    [InlineData("Klocman.BulkCrapUninstaller")]
    [InlineData("OO-Software.ShutUp10")]
    [InlineData("BleachBit.BleachBit")]
    public void System_tools_are_offered(string id)
    {
        InstallApp app = Assert.Single(AppCatalog.Apps, a => a.WingetId == id);
        Assert.Equal("System tools", app.Category);
        Assert.False(string.IsNullOrWhiteSpace(app.Description));
    }

    [Fact]
    public void ShareX_is_in_media_and_ids_are_unique()
    {
        Assert.Equal("Media", Assert.Single(AppCatalog.Apps, a => a.WingetId == "ShareX.ShareX").Category);
        Assert.Equal(AppCatalog.Apps.Count, AppCatalog.Apps.Select(a => a.WingetId).Distinct().Count());
    }
}
