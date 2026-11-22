using System.Text.Json;
using Microsoft.Win32;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class BackupServiceTests
{
    private static RegistryOp Op(RegistrySandbox s, string name, int value) => new()
    {
        Hive = s.Hive,
        Path = s.RelativePath,
        Name = name,
        Kind = "DWord",
        Value = JsonSerializer.SerializeToElement(value)
    };

    private static RegistryOp BinaryOp(RegistrySandbox s, string name) => new()
    {
        Hive = s.Hive,
        Path = s.RelativePath,
        Name = name,
        Kind = "Binary",
        Value = JsonSerializer.SerializeToElement(Array.Empty<int>())
    };

    [Fact]
    public void Capture_records_existing_and_missing_values()
    {
        using var sandbox = new RegistrySandbox();
        using var temp = new TempDir();
        var registry = new RegistryService();
        registry.Set(sandbox.Hive, sandbox.RelativePath, "Existing", RegistryValueKind.DWord, 7);
        var backup = new BackupService(registry, temp.Path);

        BackupRecord record = backup.Capture("t1", new[] { Op(sandbox, "Existing", 1), Op(sandbox, "Missing", 1) });

        BackupEntry existing = record.Entries.Single(e => e.Name == "Existing");
        Assert.True(existing.Existed);
        Assert.Equal(7, existing.Value.GetInt32());

        BackupEntry missing = record.Entries.Single(e => e.Name == "Missing");
        Assert.False(missing.Existed);
    }

    [Fact]
    public void Restore_reverts_changed_value_and_deletes_previously_missing_value()
    {
        using var sandbox = new RegistrySandbox();
        using var temp = new TempDir();
        var registry = new RegistryService();
        registry.Set(sandbox.Hive, sandbox.RelativePath, "Existing", RegistryValueKind.DWord, 7);
        var backup = new BackupService(registry, temp.Path);

        BackupRecord record = backup.Capture("t1", new[] { Op(sandbox, "Existing", 1), Op(sandbox, "Missing", 1) });

        registry.Set(sandbox.Hive, sandbox.RelativePath, "Existing", RegistryValueKind.DWord, 99);
        registry.Set(sandbox.Hive, sandbox.RelativePath, "Missing", RegistryValueKind.DWord, 99);

        backup.Restore(record);

        Assert.Equal(7, Convert.ToInt32(registry.Read(sandbox.Hive, sandbox.RelativePath, "Existing").value));
        Assert.False(registry.Read(sandbox.Hive, sandbox.RelativePath, "Missing").exists);
    }

    [Fact]
    public void Capture_and_Restore_roundtrip_a_REG_BINARY_value()
    {
        using var sandbox = new RegistrySandbox();
        using var temp = new TempDir();
        var registry = new RegistryService();
        byte[] original = { 0x01, 0x02, 0xFF, 0x00, 0x7F };
        registry.Set(sandbox.Hive, sandbox.RelativePath, "Bin", RegistryValueKind.Binary, original);
        var backup = new BackupService(registry, temp.Path);

        BackupRecord record = backup.Capture("t1", new[] { BinaryOp(sandbox, "Bin") });

        registry.Set(sandbox.Hive, sandbox.RelativePath, "Bin", RegistryValueKind.Binary, new byte[] { 0x09, 0x09 });

        backup.Restore(record);

        var restored = (byte[])registry.Read(sandbox.Hive, sandbox.RelativePath, "Bin").value!;
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Persist_then_LoadAll_roundtrips()
    {
        using var sandbox = new RegistrySandbox();
        using var temp = new TempDir();
        var registry = new RegistryService();
        var backup = new BackupService(registry, temp.Path);

        BackupRecord record = backup.Capture("t1", new[] { Op(sandbox, "A", 1) });
        backup.Persist(record);

        IReadOnlyList<BackupRecord> all = backup.LoadAll();
        Assert.Single(all);
        Assert.Equal("t1", all[0].TweakId);
    }
}
