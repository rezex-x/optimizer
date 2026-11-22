using System;
using System.IO;
using System.Linq;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class JsonProfileStoreTests
{
    [Fact]
    public void No_file_loads_the_built_ins()
    {
        using var dir = new TempDir();
        var store = new JsonProfileStore(dir.Path);

        Assert.Equal(ProfileDefaults.BuiltIns().Select(p => p.Id), store.LoadProfiles().Select(p => p.Id));
        Assert.Null(store.LoadSession());
    }

    [Fact]
    public void Profiles_round_trip()
    {
        using var dir = new TempDir();
        var store = new JsonProfileStore(dir.Path);
        var custom = new AppProfile("abc", "Fortnite", "Only Discord stays open.", "FortniteClient-Win64-Shipping.exe", true, false,
            ProfileMode.AllowList, new[] { "Discord.exe", "EpicGamesLauncher.exe" }, new[] { "WSearch" }, false);

        store.SaveProfiles(new[] { ProfileDefaults.BuiltIns()[0], custom });
        var loaded = new JsonProfileStore(dir.Path).LoadProfiles();

        Assert.Equal(2, loaded.Count);
        AppProfile back = loaded[1];
        Assert.Equal("abc", back.Id);
        Assert.Equal("Fortnite", back.Name);
        Assert.Equal("Only Discord stays open.", back.Description);
        Assert.Equal("FortniteClient-Win64-Shipping.exe", back.Trigger);
        Assert.True(back.AutoActivate);
        Assert.False(back.RestoreServices);
        Assert.Equal(ProfileMode.AllowList, back.Mode);
        Assert.Equal(new[] { "Discord.exe", "EpicGamesLauncher.exe" }, back.Apps);
        Assert.Equal(new[] { "WSearch" }, back.Services);
        Assert.False(back.IsBuiltIn);
        Assert.Contains("\"AllowList\"", File.ReadAllText(Path.Combine(dir.Path, "profiles.json")));
    }

    [Fact]
    public void A_corrupt_or_partial_file_never_breaks_loading()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "profiles.json");

        File.WriteAllText(path, "{ not json");
        Assert.Equal(3, new JsonProfileStore(dir.Path).LoadProfiles().Count);

        File.WriteAllText(path, "[]");
        Assert.Equal(3, new JsonProfileStore(dir.Path).LoadProfiles().Count);

        File.WriteAllText(path, "[{\"Id\":\"x\",\"Name\":\"Bare\"}]");
        AppProfile bare = Assert.Single(new JsonProfileStore(dir.Path).LoadProfiles());
        Assert.Equal("Bare", bare.Name);
        Assert.Equal("", bare.Description);
        Assert.Equal("", bare.Trigger);
        Assert.Empty(bare.Apps);
        Assert.Empty(bare.Services);
    }

    [Fact]
    public void Null_and_blank_list_entries_are_dropped()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "profiles.json"), "[{\"Id\":\"x\",\"Name\":\"Bare\",\"Apps\":[\"a.exe\",null,\" \"]}]");

        AppProfile bare = Assert.Single(new JsonProfileStore(dir.Path).LoadProfiles());

        Assert.Equal(new[] { "a.exe" }, bare.Apps);
    }

    [Fact]
    public void A_corrupt_file_is_kept_as_profiles_json_corrupt()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "profiles.json");
        File.WriteAllText(path, "{ not json");

        Assert.Equal(3, new JsonProfileStore(dir.Path).LoadProfiles().Count);

        Assert.Equal("{ not json", File.ReadAllText(path + ".corrupt"));
    }

    [Fact]
    public void The_session_is_saved_loaded_and_cleared()
    {
        using var dir = new TempDir();
        var store = new JsonProfileStore(dir.Path);
        var session = new ProfileSession("gaming", "Gaming Mode", new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc), false, true,
            new[] { "wuauserv", "SysMain" }, new[] { "wuauserv", "WSearch", "SysMain" }, 3);

        store.SaveSession(session);
        ProfileSession? back = new JsonProfileStore(dir.Path).LoadSession();

        Assert.NotNull(back);
        Assert.Equal("gaming", back!.ProfileId);
        Assert.Equal("Gaming Mode", back.ProfileName);
        Assert.True(back.TriggerSeen);
        Assert.Equal(new[] { "wuauserv", "SysMain" }, back.StoppedServices);
        Assert.Equal(new[] { "wuauserv", "WSearch", "SysMain" }, back.ManagedServices);
        Assert.Equal(3, back.ClosedApps);

        store.SaveSession(null);
        Assert.Null(new JsonProfileStore(dir.Path).LoadSession());
        Assert.False(File.Exists(Path.Combine(dir.Path, "profile-session.json")));
    }

    [Fact]
    public void Saving_into_a_missing_directory_creates_it()
    {
        using var dir = new TempDir();
        string nested = Path.Combine(dir.Path, "Tweaker");
        var store = new JsonProfileStore(nested);

        store.SaveProfiles(ProfileDefaults.BuiltIns());

        Assert.True(File.Exists(Path.Combine(nested, "profiles.json")));
    }
}
