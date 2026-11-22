using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tweaker.Services;

public sealed record PresetKey(string Key, EntryShape Shape);

public sealed record GamePreset(string Name, string ProcessExe, IReadOnlyList<PresetKey> Keys, string? AltProcessExe = null)
{
    public EntryShape? ShapeFor(string key) =>
        Keys.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase))?.Shape;
}

public static class GamePresets
{
    private static readonly ChoiceOption[] Quality =
    {
        new("0", "Low"), new("1", "Medium"), new("2", "High"), new("3", "Epic"), new("4", "Cinematic"),
    };

    private static readonly ChoiceOption[] WindowMode =
    {
        new("0", "Fullscreen"), new("1", "Windowed Fullscreen"), new("2", "Windowed"),
    };

    private static readonly ChoiceOption[] OffOn = { new("0", "Off"), new("1", "On") };
    private static readonly ChoiceOption[] WindowedFullscreen = { new("0", "Windowed"), new("1", "Fullscreen") };

    private static EntryShape Slider(double min, double max, double step, string hint = "") =>
        new(EditorKind.Slider, min, max, step, false, null, hint);

    private static EntryShape Choice(IReadOnlyList<ChoiceOption> options, string hint = "") =>
        new(EditorKind.Choice, Options: options, Hint: hint);

    private static IEnumerable<PresetKey> Unreal() => new[]
    {
        new PresetKey("FrameRateLimit", Slider(0, 360, 1, "0 = unlimited")),
        new PresetKey("ResolutionSizeX", Slider(640, 7680, 1)),
        new PresetKey("ResolutionSizeY", Slider(480, 4320, 1)),
        new PresetKey("FullscreenMode", Choice(WindowMode)),
        new PresetKey("LastConfirmedFullscreenMode", Choice(WindowMode)),
        new PresetKey("PreferredFullscreenMode", Choice(WindowMode)),
        new PresetKey("DisplayGamma", Slider(1.5, 3, 0.05)),
        new PresetKey("sg.ResolutionQuality", Slider(50, 100, 1, "3D resolution %")),
        new PresetKey("sg.ViewDistanceQuality", Choice(Quality)),
        new PresetKey("sg.AntiAliasingQuality", Choice(Quality)),
        new PresetKey("sg.ShadowQuality", Choice(Quality)),
        new PresetKey("sg.GlobalIlluminationQuality", Choice(Quality)),
        new PresetKey("sg.ReflectionQuality", Choice(Quality)),
        new PresetKey("sg.PostProcessQuality", Choice(Quality)),
        new PresetKey("sg.TextureQuality", Choice(Quality)),
        new PresetKey("sg.EffectsQuality", Choice(Quality)),
        new PresetKey("sg.FoliageQuality", Choice(Quality)),
        new PresetKey("sg.ShadingQuality", Choice(Quality)),
    };

    public static readonly GamePreset Fortnite = new("Fortnite", "FortniteClient-Win64-Shipping.exe", Unreal()
        .Append(new PresetKey("PreferredRHI", Choice(new[] { new ChoiceOption("dx11", "DirectX 11"), new ChoiceOption("dx12", "DirectX 12") })))
        .ToList());

    public static readonly GamePreset Valorant = new("VALORANT", "VALORANT-Win64-Shipping.exe", Unreal().ToList());

    public static readonly GamePreset CounterStrike2 = new("Counter-Strike 2", "cs2.exe", new[]
    {
        new PresetKey("setting.defaultres", Slider(640, 7680, 1)),
        new PresetKey("setting.defaultresheight", Slider(480, 4320, 1)),
        new PresetKey("setting.fullscreen", Choice(WindowedFullscreen)),
        new PresetKey("setting.nowindowborder", Choice(OffOn, "borderless window")),
        new PresetKey("setting.mat_vsync", Choice(OffOn, "V-Sync")),
        new PresetKey("setting.r_low_latency", Choice(new[] { new ChoiceOption("0", "Off"), new ChoiceOption("1", "On"), new ChoiceOption("2", "On + Boost") }, "NVIDIA Reflex")),
    });

    public static readonly GamePreset Apex = new("Apex Legends", "r5apex.exe", new[]
    {
        new PresetKey("setting.defaultres", Slider(640, 7680, 1)),
        new PresetKey("setting.defaultresheight", Slider(480, 4320, 1)),
        new PresetKey("setting.fullscreen", Choice(WindowedFullscreen)),
        new PresetKey("setting.nowindowborder", Choice(OffOn, "borderless window")),
        new PresetKey("setting.dvs_enable", Choice(OffOn, "adaptive resolution")),
    }, AltProcessExe: "r5apex_dx12.exe");

    public static IReadOnlyList<GamePreset> All { get; } = new[] { Fortnite, Valorant, CounterStrike2, Apex };

    public static GamePreset? For(string path)
    {
        string p = path.Replace('/', '\\');
        string file = Path.GetFileName(p);
        if (file.Equals("GameUserSettings.ini", StringComparison.OrdinalIgnoreCase))
        {
            if (p.Contains(@"\FortniteGame\", StringComparison.OrdinalIgnoreCase)) return Fortnite;
            if (p.Contains(@"\VALORANT\", StringComparison.OrdinalIgnoreCase)) return Valorant;
            return null;
        }
        if (file.Equals("cs2_video.txt", StringComparison.OrdinalIgnoreCase)) return CounterStrike2;
        if (file.Equals("videoconfig.txt", StringComparison.OrdinalIgnoreCase)
            && p.Contains(@"\Respawn\Apex\", StringComparison.OrdinalIgnoreCase)) return Apex;
        return null;
    }

    public static GamePreset? ForGame(string game) =>
        All.FirstOrDefault(p => string.Equals(p.Name, game, StringComparison.OrdinalIgnoreCase));
}
