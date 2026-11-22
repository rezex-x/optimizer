using System.IO;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class JsonDevicePowerBackupTests
{
    [Fact]
    public void Only_the_first_original_is_remembered_and_it_survives_a_restart()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "Tweaker", "device-power-backup.json");
        var backup = new JsonDevicePowerBackup(path);

        backup.Remember(@"USB\VID_1&PID_2\A", true);
        backup.Remember(@"usb\vid_1&pid_2\a", false);

        var reloaded = new JsonDevicePowerBackup(path);
        Assert.Single(reloaded.Originals);
        Assert.True(reloaded.Originals[@"USB\VID_1&PID_2\A"]);
    }

    [Fact]
    public void Forget_removes_the_device()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "backup.json");
        var backup = new JsonDevicePowerBackup(path);
        backup.Remember("A", true);
        backup.Remember("B", false);

        backup.Forget("a");

        Assert.Equal(new[] { "B" }, new JsonDevicePowerBackup(path).Originals.Keys);
    }

    [Fact]
    public void A_missing_or_corrupt_file_is_empty()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "backup.json");
        Assert.Empty(new JsonDevicePowerBackup(path).Originals);

        File.WriteAllText(path, "{ nope");
        Assert.Empty(new JsonDevicePowerBackup(path).Originals);
    }

    [Fact]
    public void A_corrupt_file_is_backed_up_and_replaced_instead_of_losing_new_writes()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "backup.json");
        File.WriteAllText(path, "{ nope");

        var backup = new JsonDevicePowerBackup(path);
        backup.Remember("X", true);

        Assert.Equal("{ nope", File.ReadAllText(path + ".corrupt"));
        Assert.True(backup.Originals["X"]);
    }

    [Fact]
    public void A_locked_file_is_not_cached_empty_and_is_retried_next_time()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "backup.json");
        new JsonDevicePowerBackup(path).Remember("A", true);

        var reader = new JsonDevicePowerBackup(path);
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Empty(reader.Originals);
        }

        Assert.True(reader.Originals["A"]);
    }
}
