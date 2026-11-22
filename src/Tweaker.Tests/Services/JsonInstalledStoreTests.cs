using System;
using System.IO;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class JsonInstalledStoreTests
{
    [Fact]
    public void Installs_are_remembered_with_their_first_date()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "Tweaker", "installed.json");
        var store = new JsonInstalledStore(path);

        store.Record("Discord.Discord", new DateTime(2026, 9, 27, 12, 0, 0));
        store.Record("discord.discord", new DateTime(2026, 9, 28, 12, 0, 0));

        Assert.Equal(new DateTime(2026, 9, 27, 12, 0, 0), new JsonInstalledStore(path).Installed["Discord.Discord"]);
    }
}
