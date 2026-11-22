using Microsoft.Win32;
using Tweaker.Core.Catalog;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class WindowsUpdateServiceTests
{
    [Fact]
    public void Ops_disable_all_sets_no_auto_update()
    {
        var ops = UpdatePresets.Ops(UpdatePreset.DisableAll);
        Assert.Contains(ops, o => o.Name == "NoAutoUpdate" && o.Value.GetInt32() == 1);
    }

    [Fact]
    public void Ops_security_only_defers_feature_updates_365()
    {
        var ops = UpdatePresets.Ops(UpdatePreset.SecurityOnly);
        Assert.Contains(ops, o => o.Name == "DeferFeatureUpdates" && o.Value.GetInt32() == 1);
        Assert.Contains(ops, o => o.Name == "DeferFeatureUpdatesPeriodInDays" && o.Value.GetInt32() == 365);
    }

    [Fact]
    public void ServiceCommand_disable_all_disables_wuauserv()
    {
        Assert.Contains("wuauserv", UpdatePresets.ServiceCommand(UpdatePreset.DisableAll));
        Assert.Contains("disabled", UpdatePresets.ServiceCommand(UpdatePreset.DisableAll));
    }

    [Fact]
    public void Apply_backs_up_writes_ops_and_runs_service_command()
    {
        var registry = new FakeRegistryService();
        var backup = new FakeBackupService();
        var runner = new FakeProcessRunner();
        var svc = new WindowsUpdateService(registry, backup, runner);

        UpdateApplyResult r = svc.Apply(UpdatePreset.DisableAll);

        Assert.True(r.Success);
        Assert.Contains("windows-update", backup.Captured);
        Assert.Contains(registry.Writes, w => w.name == "NoAutoUpdate");
        Assert.Equal("cmd.exe", runner.Calls[0].fileName);
        Assert.Contains("wuauserv", runner.Calls[0].arguments);
    }

    [Fact]
    public void DetectActive_reads_policy_values()
    {
        var registry = new FakeRegistryService();
        registry.Set("HKLM", UpdatePresets.AuPath, "NoAutoUpdate", RegistryValueKind.DWord, 1);
        var svc = new WindowsUpdateService(registry, new FakeBackupService(), new FakeProcessRunner());
        Assert.Equal(UpdatePreset.DisableAll, svc.DetectActive());
    }
}
