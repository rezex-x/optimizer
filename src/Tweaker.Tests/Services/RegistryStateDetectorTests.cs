using System.Text.Json;
using Microsoft.Win32;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class RegistryStateDetectorTests
{
    private static Tweak RegTweak(RegistrySandbox s, params (string name, int val)[] ops) => new()
    {
        Id = "t",
        Type = TweakType.Registry,
        Apply = ops.Select(o => new RegistryOp
        {
            Hive = s.Hive,
            Path = s.RelativePath,
            Name = o.name,
            Kind = "DWord",
            Value = JsonSerializer.SerializeToElement(o.val)
        }).ToList()
    };

    [Fact]
    public void All_targets_match_is_Applied()
    {
        using var sandbox = new RegistrySandbox();
        var registry = new RegistryService();
        registry.Set(sandbox.Hive, sandbox.RelativePath, "X", RegistryValueKind.DWord, 2);
        var detector = new RegistryStateDetector(registry);

        Assert.Equal(TweakState.Applied, detector.Detect(RegTweak(sandbox, ("X", 2))));
    }

    [Fact]
    public void No_targets_match_is_NotApplied()
    {
        using var sandbox = new RegistrySandbox();
        var registry = new RegistryService();
        var detector = new RegistryStateDetector(registry);

        Assert.Equal(TweakState.NotApplied, detector.Detect(RegTweak(sandbox, ("X", 2))));
    }

    [Fact]
    public void Some_targets_match_is_Partial()
    {
        using var sandbox = new RegistrySandbox();
        var registry = new RegistryService();
        registry.Set(sandbox.Hive, sandbox.RelativePath, "X", RegistryValueKind.DWord, 2);
        var detector = new RegistryStateDetector(registry);

        Assert.Equal(TweakState.Partial, detector.Detect(RegTweak(sandbox, ("X", 2), ("Y", 5))));
    }

    [Fact]
    public void Batch_tweak_is_Unknown()
    {
        var detector = new RegistryStateDetector(new RegistryService());
        Assert.Equal(TweakState.Unknown, detector.Detect(new Tweak { Type = TweakType.Batch }));
    }

    [Fact]
    public void Type_mismatch_string_vs_dword_is_NotApplied()
    {
        using var sandbox = new RegistrySandbox();
        var registry = new RegistryService();
        registry.Set(sandbox.Hive, sandbox.RelativePath, "X", RegistryValueKind.String, "1");
        var detector = new RegistryStateDetector(registry);

        var tweak = new Tweak
        {
            Id = "t",
            Type = TweakType.Registry,
            Apply = new List<RegistryOp>
            {
                new()
                {
                    Hive = sandbox.Hive,
                    Path = sandbox.RelativePath,
                    Name = "X",
                    Kind = "DWord",
                    Value = JsonSerializer.SerializeToElement(1)
                }
            }
        };

        Assert.Equal(TweakState.NotApplied, detector.Detect(tweak));
    }

    [Fact]
    public void Detect_ops_are_used_for_batch_tweaks()
    {
        using var sandbox = new RegistrySandbox();
        var registry = new RegistryService();
        registry.Set(sandbox.Hive, sandbox.RelativePath, "Start", RegistryValueKind.DWord, 4);
        var detector = new RegistryStateDetector(registry);

        var batchApplied = new Tweak
        {
            Id = "b", Type = TweakType.Batch, ApplyScript = "sc ...",
            Detect = new List<RegistryOp>
            {
                new() { Hive = sandbox.Hive, Path = sandbox.RelativePath, Name = "Start",
                        Kind = "DWord", Value = JsonSerializer.SerializeToElement(4) }
            }
        };
        Assert.Equal(TweakState.Applied, detector.Detect(batchApplied));

        var batchNotApplied = new Tweak
        {
            Id = "b2", Type = TweakType.Batch, ApplyScript = "sc ...",
            Detect = new List<RegistryOp>
            {
                new() { Hive = sandbox.Hive, Path = sandbox.RelativePath, Name = "Start",
                        Kind = "DWord", Value = JsonSerializer.SerializeToElement(2) }
            }
        };
        Assert.Equal(TweakState.NotApplied, detector.Detect(batchNotApplied));
    }

    [Fact]
    public void Batch_tweak_without_detect_stays_Unknown()
    {
        var detector = new RegistryStateDetector(new RegistryService());
        Assert.Equal(TweakState.Unknown,
            detector.Detect(new Tweak { Type = TweakType.Batch, ApplyScript = "x" }));
    }
}
