using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ValueTypingTests
{
    [Fact]
    public void Booleans_become_switches_and_numbers_become_guessed_sliders()
    {
        Assert.Equal(EditorKind.Toggle, ValueTyping.Infer("False").Kind);
        Assert.Equal(EditorKind.Toggle, ValueTyping.Infer(" true ").Kind);
        Assert.Equal(EditorKind.Text, ValueTyping.Infer("high").Kind);
        Assert.Equal(EditorKind.Text, ValueTyping.Infer("(X=1,Y=2)").Kind);

        EntryShape fps = ValueTyping.Infer("240");
        Assert.Equal((EditorKind.Slider, 0d, 500d, 1d, false, true), (fps.Kind, fps.Min, fps.Max, fps.Step, fps.IsFloat, fps.Guessed));

        EntryShape gamma = ValueTyping.Infer("2.200000");
        Assert.Equal((0d, 10d, 0.05, true), (gamma.Min, gamma.Max, gamma.Step, gamma.IsFloat));

        EntryShape scale = ValueTyping.Infer("0.75");
        Assert.Equal((0d, 1d, 0.01), (scale.Min, scale.Max, scale.Step));

        Assert.Equal((-10d, 10d), ValueTyping.GuessRange(-3));
        Assert.Equal((0d, 100d), ValueTyping.GuessRange(50));
        Assert.Equal((0d, 5000d), ValueTyping.GuessRange(1920));
        Assert.Equal(200d, ValueTyping.NiceCeil(101));
    }

    [Fact]
    public void Huge_integers_are_ids_not_sliders()
    {
        Assert.Equal(EditorKind.Text, ValueTyping.Infer("76561198000000000").Kind);
        Assert.Equal(EditorKind.Text, ValueTyping.Infer("-2000000000").Kind);
        Assert.Equal(EditorKind.Slider, ValueTyping.Infer("1000000000").Kind);
    }

    [Fact]
    public void Written_values_keep_the_original_style()
    {
        Assert.Equal("True", ValueTyping.FormatBool(true, "False"));
        Assert.Equal("false", ValueTyping.FormatBool(false, "true"));
        Assert.Equal("TRUE", ValueTyping.FormatBool(true, "FALSE"));
        Assert.Equal("144.000000", ValueTyping.FormatNumber(144, true, "240.000000"));
        Assert.Equal("2.4", ValueTyping.FormatNumber(2.4, true, "2.2"));
        Assert.Equal("1440", ValueTyping.FormatNumber(1440.4, false, "1920"));
        Assert.Equal("-5", ValueTyping.FormatNumber(-5, false, "3"));
        Assert.Equal("2.25", ValueTyping.FormatNumber(2.25, true, "2.2", 0.05));
        Assert.Equal("2.4", ValueTyping.FormatNumber(2.4, true, "2.2", 0.1));
        Assert.Equal("144.000000", ValueTyping.FormatNumber(144, true, "240.000000", 0.05));
    }

    [Fact]
    public void Presets_replace_the_guess_with_real_ranges_and_choices()
    {
        GamePreset fn = GamePresets.Fortnite;

        EntryShape shadows = ValueTyping.ShapeFor("sg.ShadowQuality", "2", fn);
        Assert.Equal(EditorKind.Choice, shadows.Kind);
        Assert.Equal(new[] { "Low", "Medium", "High", "Epic", "Cinematic" }, shadows.Options!.Select(o => o.Label));

        EntryShape odd = ValueTyping.ShapeFor("SG.SHADOWQUALITY", "7", fn);
        Assert.Equal("7 (current)", odd.Options!.Last().Label);

        EntryShape fps = ValueTyping.ShapeFor("FrameRateLimit", "240.000000", fn);
        Assert.Equal((EditorKind.Slider, 0d, 360d, true, false, "0 = unlimited"), (fps.Kind, fps.Min, fps.Max, fps.IsFloat, fps.Guessed, fps.Hint));

        Assert.True(ValueTyping.ShapeFor("SomethingElse", "12", fn).Guessed);
        Assert.Equal(EditorKind.Toggle, ValueTyping.ShapeFor("bUseVSync", "False", fn).Kind);
    }

    [Fact]
    public void A_preset_applies_by_path()
    {
        Assert.Same(GamePresets.Fortnite, GamePresets.For(@"C:\Users\A\AppData\Local\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini"));
        Assert.Same(GamePresets.Valorant, GamePresets.For(@"C:\Users\A\AppData\Local\VALORANT\Saved\Config\abc\Windows\GameUserSettings.ini"));
        Assert.Same(GamePresets.CounterStrike2, GamePresets.For(@"C:\Steam\userdata\1\730\local\cfg\cs2_video.txt"));
        Assert.Same(GamePresets.Apex, GamePresets.For(@"C:\Users\A\Saved Games\Respawn\Apex\local\videoconfig.txt"));
        Assert.Null(GamePresets.For(@"C:\Other\GameUserSettings.ini"));
        Assert.Null(GamePresets.For(@"C:\Other\videoconfig.txt"));
        Assert.Same(GamePresets.Apex, GamePresets.ForGame("Apex Legends"));
    }

    [Fact]
    public void Known_game_configs_are_found_where_they_exist()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"L:\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini",
            @"L:\VALORANT\Saved\Config\acc1\WindowsClient\GameUserSettings.ini",
            @"S:\Steam\userdata\42\730\local\cfg\cs2_video.txt",
        };
        var dirs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [@"L:\VALORANT\Saved\Config"] = new[] { @"L:\VALORANT\Saved\Config\acc1", @"L:\VALORANT\Saved\Config\acc2" },
            [@"S:\Steam\userdata"] = new[] { @"S:\Steam\userdata\42" },
        };
        Func<string, string?> env = name => name switch { "LOCALAPPDATA" => @"L:\", "USERPROFILE" => @"U:\", _ => null };

        IReadOnlyList<DetectedConfig> found = ConfigDetector.Find(env,
            d => dirs.TryGetValue(d, out string[]? sub) ? sub : throw new DirectoryNotFoundException(d),
            existing.Contains, @"S:\Steam");

        Assert.Equal(new[] { "Fortnite", "VALORANT", "Counter-Strike 2" }, found.Select(f => f.Game));
        Assert.Equal("cs2_video.txt", found[2].FileName);
        Assert.Equal(@"L:\VALORANT\Saved\Config\acc1\WindowsClient\GameUserSettings.ini", found[1].Path);
        Assert.Equal(@"%LOCALAPPDATA%\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini", ConfigDetector.Display(found[0].Path, env));
    }
}
