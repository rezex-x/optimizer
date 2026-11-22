using System.Text.Json;
using Microsoft.Win32;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class TweakEngineTests
{
    private static Tweak RegTweak(RegistrySandbox s, string name, int target) => new()
    {
        Id = "reg-tweak",
        Type = TweakType.Registry,
        Apply = new List<RegistryOp>
        {
            new()
            {
                Hive = s.Hive, Path = s.RelativePath, Name = name, Kind = "DWord",
                Value = JsonSerializer.SerializeToElement(target)
            }
        }
    };

    private static TweakEngine BuildEngine(TempDir temp, IProcessRunner runner, out RegistryService registry,
        IAppliedTweakStore? applied = null)
    {
        registry = new RegistryService();
        var backup = new BackupService(registry, temp.Path);
        var batch = new BatchService(runner);
        var detector = new RegistryStateDetector(registry);
        return new TweakEngine(registry, backup, batch, detector, applied);
    }

    [Fact]
    public void Apply_registry_writes_target_and_GetState_is_Applied()
    {
        using var sandbox = new RegistrySandbox();
        using var temp = new TempDir();
        TweakEngine engine = BuildEngine(temp, new FakeProcessRunner(), out RegistryService registry);
        Tweak tweak = RegTweak(sandbox, "X", 2);

        engine.Apply(tweak);

        Assert.Equal(2, Convert.ToInt32(registry.Read(sandbox.Hive, sandbox.RelativePath, "X").value));
        Assert.Equal(TweakState.Applied, engine.GetState(tweak));
    }

    [Fact]
    public void Revert_registry_restores_prior_value_from_backup()
    {
        using var sandbox = new RegistrySandbox();
        using var temp = new TempDir();
        TweakEngine engine = BuildEngine(temp, new FakeProcessRunner(), out RegistryService registry);
        registry.Set(sandbox.Hive, sandbox.RelativePath, "X", RegistryValueKind.DWord, 1);
        Tweak tweak = RegTweak(sandbox, "X", 2);

        engine.Apply(tweak);
        engine.Revert(tweak);

        Assert.Equal(1, Convert.ToInt32(registry.Read(sandbox.Hive, sandbox.RelativePath, "X").value));
    }

    [Fact]
    public void Apply_batch_runs_apply_script()
    {
        using var temp = new TempDir();
        var runner = new FakeProcessRunner();
        TweakEngine engine = BuildEngine(temp, runner, out _);
        var tweak = new Tweak { Id = "b", Type = TweakType.Batch, ApplyScript = "echo hi" };

        engine.Apply(tweak);

        Assert.Contains("echo hi", runner.Calls[0].arguments);
    }

    [Fact]
    public void Revert_batch_runs_revert_script()
    {
        using var temp = new TempDir();
        var runner = new FakeProcessRunner();
        TweakEngine engine = BuildEngine(temp, runner, out _);
        var tweak = new Tweak { Id = "b", Type = TweakType.Batch, RevertScript = "echo bye" };

        engine.Revert(tweak);

        Assert.Contains("echo bye", runner.Calls[0].arguments);
    }

    [Fact]
    public void Revert_registry_with_no_backup_applies_declared_revert_ops()
    {
        using var sandbox = new RegistrySandbox();
        using var temp = new TempDir();
        TweakEngine engine = BuildEngine(temp, new FakeProcessRunner(), out RegistryService registry);
        var tweak = new Tweak
        {
            Id = "no-backup",
            Type = TweakType.Registry,
            Apply = new List<RegistryOp>(),
            Revert = new List<RegistryOp>
            {
                new() { Hive = sandbox.Hive, Path = sandbox.RelativePath, Name = "R", Kind = "DWord", Value = JsonSerializer.SerializeToElement(5) }
            }
        };

        engine.Revert(tweak);

        Assert.Equal(5, Convert.ToInt32(registry.Read(sandbox.Hive, sandbox.RelativePath, "R").value));
    }

    private static Tweak ScriptTweak(string id = "batch-tweak", bool runOnce = false) => new()
    {
        Id = id, Type = TweakType.Batch, ApplyScript = "echo on", RevertScript = "echo off", RunOnce = runOnce,
    };

    [Fact]
    public void Script_tweak_without_detection_follows_the_remembered_state()
    {
        using var temp = new TempDir();
        var store = new InMemoryAppliedTweakStore();
        TweakEngine engine = BuildEngine(temp, new FakeProcessRunner(), out _, store);
        Tweak tweak = ScriptTweak();

        Assert.Equal(TweakState.NotApplied, engine.GetState(tweak));

        engine.Apply(tweak);
        Assert.Equal(TweakState.Applied, engine.GetState(tweak));
        Assert.True(store.IsApplied(tweak.Id));

        engine.Revert(tweak);
        Assert.Equal(TweakState.NotApplied, engine.GetState(tweak));
        Assert.False(store.IsApplied(tweak.Id));
    }

    [Fact]
    public void Script_tweak_state_survives_a_restart_through_the_file_store()
    {
        using var temp = new TempDir();
        string file = System.IO.Path.Combine(temp.Path, "applied-tweaks.json");
        Tweak tweak = ScriptTweak();
        BuildEngine(temp, new FakeProcessRunner(), out _, new AppliedTweakStore(file)).Apply(tweak);

        TweakEngine restarted = BuildEngine(temp, new FakeProcessRunner(), out _, new AppliedTweakStore(file));

        Assert.Equal(TweakState.Applied, restarted.GetState(tweak));
    }

    [Fact]
    public void Detection_rule_wins_over_the_remembered_state()
    {
        using var sandbox = new RegistrySandbox();
        using var temp = new TempDir();
        var store = new InMemoryAppliedTweakStore();
        store.SetApplied("batch-tweak", true);
        TweakEngine engine = BuildEngine(temp, new FakeProcessRunner(), out _, store);
        Tweak tweak = ScriptTweak();
        tweak.Detect = new List<RegistryOp>
        {
            new() { Hive = sandbox.Hive, Path = sandbox.RelativePath, Name = "Probe", Kind = "DWord",
                    Value = JsonSerializer.SerializeToElement(1) }
        };

        Assert.Equal(TweakState.NotApplied, engine.GetState(tweak));
    }

    [Fact]
    public void Run_once_script_is_never_remembered_as_applied()
    {
        using var temp = new TempDir();
        var store = new InMemoryAppliedTweakStore();
        TweakEngine engine = BuildEngine(temp, new FakeProcessRunner(), out _, store);
        Tweak tweak = ScriptTweak("clean-caches", runOnce: true);

        engine.Apply(tweak);

        Assert.False(store.IsApplied("clean-caches"));
        Assert.NotEqual(TweakState.Applied, engine.GetState(tweak));
    }

    [Fact]
    public void Value_tweaks_are_never_applied_by_the_engine()
    {
        var registry = new FakeRegistryService();
        var engine = new TweakEngine(registry, new FakeBackupService(), new BatchService(new FakeProcessRunner()),
            new RegistryStateDetector(registry));
        var tweak = new Tweak { Id = "v", Type = TweakType.Value, Power = new PowerSettingSpec() };

        Assert.Equal(TweakState.Unknown, engine.GetState(tweak));
        Assert.Throws<NotSupportedException>(() => engine.Apply(tweak));
        Assert.Throws<NotSupportedException>(() => engine.Revert(tweak));
        Assert.Empty(registry.Writes);
    }
}
