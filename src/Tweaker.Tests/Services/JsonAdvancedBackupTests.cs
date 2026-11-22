using System.IO;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class JsonAdvancedBackupTests
{
    [Fact]
    public void Only_the_first_original_is_kept_and_it_survives_a_restart()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "Tweaker", "advanced-backup.json");
        var backup = new JsonAdvancedBackup(path);

        backup.Remember(@"msi|PCI\A", "");
        backup.Remember(@"MSI|pci\a", "1");
        backup.Remember(@"nic|PCI\B|*EEE", "1");

        var reloaded = new JsonAdvancedBackup(path);
        Assert.Equal("", reloaded.Originals[@"msi|PCI\A"]);
        Assert.Equal("1", reloaded.Originals[@"nic|PCI\B|*EEE"]);

        reloaded.Forget(@"msi|pci\a");
        Assert.Single(new JsonAdvancedBackup(path).Originals);
    }

    [Fact]
    public void Remember_reports_success_for_a_new_and_an_already_stored_key()
    {
        using var dir = new TempDir();
        var backup = new JsonAdvancedBackup(Path.Combine(dir.Path, "advanced-backup.json"));

        Assert.True(backup.Remember("k", "v"));
        Assert.True(backup.Remember("k", "other"));
        Assert.Equal("v", backup.Originals["k"]);
    }

    [Fact]
    public void Remember_fails_and_keeps_nothing_when_the_file_cant_be_saved()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "advanced-backup.json");
        Directory.CreateDirectory(path);
        var backup = new JsonAdvancedBackup(path);

        Assert.False(backup.Remember("k", "v"));
        Assert.Empty(backup.Originals);
    }

    [Fact]
    public void Null_values_in_the_file_are_dropped()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "advanced-backup.json");
        File.WriteAllText(path, "{ \"a\": null, \"b\": \"1\" }");

        var backup = new JsonAdvancedBackup(path);

        Assert.Equal("b", Assert.Single(backup.Originals).Key);
    }

    [Fact]
    public void A_corrupt_file_is_kept_aside_and_reads_as_empty()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "advanced-backup.json");
        File.WriteAllText(path, "{ nope");

        var backup = new JsonAdvancedBackup(path);
        Assert.Empty(backup.Originals);
        backup.Remember("k", "v");

        Assert.Equal("{ nope", File.ReadAllText(path + ".corrupt"));
        Assert.Equal("v", new JsonAdvancedBackup(path).Originals["k"]);
    }
}
