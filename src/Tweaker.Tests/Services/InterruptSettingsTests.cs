using System.Linq;
using Microsoft.Win32;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class InterruptSettingsTests
{
    private const string Id = @"PCI\VEN_10DE&DEV_2803\4&1";
    private readonly FakeRegistryService _registry = new();
    private InterruptSettings Settings() => new(_registry);

    private void Put(string path, string name, RegistryValueKind kind, object value) =>
        _registry.Values["HKLM|" + path + "|" + name] = (kind, value);

    [Fact]
    public void Msi_reads_default_on_or_off()
    {
        Assert.Equal(MsiMode.Default, Settings().ReadMsi(Id));
        Put(InterruptRules.MsiPath(Id), "MSISupported", RegistryValueKind.DWord, 1);
        Assert.Equal(MsiMode.On, Settings().ReadMsi(Id));
        Put(InterruptRules.MsiPath(Id), "MSISupported", RegistryValueKind.DWord, 0);
        Assert.Equal(MsiMode.Off, Settings().ReadMsi(Id));
    }

    [Fact]
    public void Msi_writes_a_dword_or_deletes_it_for_windows_default()
    {
        Assert.Null(Settings().WriteMsi(Id, MsiMode.On));
        Assert.Null(Settings().WriteMsi(Id, MsiMode.Off));
        Assert.Null(Settings().WriteMsi(Id, MsiMode.Default));

        Assert.Equal(new object[] { 1, 0 }, _registry.Writes.Select(w => w.value));
        Assert.All(_registry.Writes, w => Assert.Equal(("HKLM", InterruptRules.MsiPath(Id), "MSISupported", RegistryValueKind.DWord),
            (w.hive, w.path, w.name, w.kind)));
        Assert.Contains(("HKLM", InterruptRules.MsiPath(Id), (string?)"MSISupported"), _registry.Deletes);
    }

    [Fact]
    public void Affinity_reads_the_policy_and_mask()
    {
        Assert.Equal((-1, 0UL), Settings().ReadAffinity(Id));
        Put(InterruptRules.AffinityPath(Id), "DevicePolicy", RegistryValueKind.DWord, 4);
        Put(InterruptRules.AffinityPath(Id), "AssignmentSetOverride", RegistryValueKind.Binary, new byte[] { 8, 0, 0, 0, 0, 0, 0, 0 });
        Assert.Equal((4, 8UL), Settings().ReadAffinity(Id));
    }

    [Fact]
    public void Affinity_writes_policy_and_mask_or_removes_both()
    {
        string path = InterruptRules.AffinityPath(Id);

        Assert.Null(Settings().WriteAffinity(Id, 4, 1UL << 3));
        Assert.Contains(_registry.Writes, w => w.path == path && w.name == "DevicePolicy" && (int)w.value == 4);
        Assert.Contains(_registry.Writes, w => w.path == path && w.name == "AssignmentSetOverride" && w.kind == RegistryValueKind.Binary
                                               && ((byte[])w.value).SequenceEqual(new byte[] { 8, 0, 0, 0, 0, 0, 0, 0 }));

        _registry.Writes.Clear();
        Assert.Null(Settings().WriteAffinity(Id, 5, 0));
        Assert.Contains(_registry.Writes, w => w.name == "DevicePolicy" && (int)w.value == 5);
        Assert.Contains((("HKLM", path, (string?)"AssignmentSetOverride")), _registry.Deletes);

        _registry.Deletes.Clear();
        Assert.Null(Settings().WriteAffinity(Id, -1, 0));
        Assert.Contains(("HKLM", path, (string?)"DevicePolicy"), _registry.Deletes);
        Assert.Contains(("HKLM", path, (string?)"AssignmentSetOverride"), _registry.Deletes);
    }
}
