using System;
using System.IO;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class JsonPowerBackupTests
{
    [Fact]
    public void Originals_survive_a_restart_and_only_the_first_counts()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "Tweaker", "power-backup.json");
        var backup = new JsonPowerBackup(path);

        Assert.True(backup.Remember("a|b|c", 10));
        Assert.True(backup.Remember("A|B|C", 99));
        Assert.Equal(10u, new JsonPowerBackup(path).Originals["a|b|c"]);

        backup.Forget("a|b|c");
        Assert.Empty(new JsonPowerBackup(path).Originals);
    }

    [Fact]
    public void Keys_name_the_plan_subgroup_and_setting()
    {
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e|54533251-82be-4824-96c1-47b60b740d00|0cc5b647-c1df-4637-891a-dec35c318583",
            PowerKeys.Key(new Guid("381B4222-F694-41F0-9685-FF5BB260DF2E"), new Guid("54533251-82be-4824-96c1-47b60b740d00"),
                new Guid("0cc5b647-c1df-4637-891a-dec35c318583")));
    }
}
