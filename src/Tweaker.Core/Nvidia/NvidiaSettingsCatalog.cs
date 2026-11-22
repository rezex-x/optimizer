using System.Collections.Generic;
using Tweaker.Core.Models;

namespace Tweaker.Core.Nvidia;

public static class NvidiaSettingsCatalog
{
    public static IReadOnlyList<NvidiaSetting> Items() => new[]
    {
        new NvidiaSetting
        {
            Id = "power-management", Name = "Power Management Mode",
            Description = "Sets the GPU power mode to Prefer Maximum Performance so it doesn't downclock between frames.",
            SettingId = 0x1057EB71,
            RecommendedValue = 0x00000001,
            DefaultValue = 0x00000005,
            Benefit = "Real fps gains in CPU-limited games (esports, strategy); keeps clocks high.",
            Downsides = "Higher idle power draw and heat; little effect in GPU-bound AAA games.",
            Risk = RiskLevel.Medium,
            Category = "Performance",
        },
        new NvidiaSetting
        {
            Id = "low-latency", Name = "Low Latency Mode (On)",
            Description = "Sets Low Latency Mode to On (max pre-rendered frames = 1).",
            SettingId = 0x007BA09E,
            RecommendedValue = 0x00000001,
            DefaultValue = 0x00000000,
            Benefit = "Lower input latency and better 1% lows.",
            Downsides = "Rarely raises average fps; in-game NVIDIA Reflex is better where a game supports it.",
            Risk = RiskLevel.Low,
            Category = "Latency",
        },
        new NvidiaSetting
        {
            Id = "texture-filtering", Name = "Texture Filtering: High Performance",
            Description = "Sets Texture Filtering Quality to High Performance.",
            SettingId = 0x00CE2691,
            RecommendedValue = 0x00000014,
            DefaultValue = 0x00000000,
            Benefit = "A few extra fps.",
            Downsides = "Slightly softer or shimmery textures.",
            Risk = RiskLevel.Low,
            Category = "Image quality",
        },
        new NvidiaSetting
        {
            Id = "vsync-off", Name = "Vertical Sync: Force Off",
            Description = "Forces Vertical Sync off for all games.",
            SettingId = 0x00A879CF,
            RecommendedValue = 0x08416747,
            DefaultValue = 0x60925292,
            Benefit = "Uncapped fps with no V-Sync input lag.",
            Downsides = "Screen tearing. Leave this off (skip it) if you use G-Sync / a VRR monitor.",
            Risk = RiskLevel.Low,
            Category = "Sync",
        },
        new NvidiaSetting
        {
            Id = "threaded-optimization", Name = "Threaded Optimization: On",
            Description = "Enables multi-threaded driver optimization.",
            SettingId = 0x20C1221E,
            RecommendedValue = 0x00000001,
            DefaultValue = 0x00000000,
            Benefit = "Lets some (mostly OpenGL) games use more CPU cores.",
            Downsides = "Minimal for most modern DX12/Vulkan games; can rarely misbehave in a few titles.",
            Risk = RiskLevel.Low,
            Category = "Performance",
        },
        new NvidiaSetting
        {
            Id = "shader-cache", Name = "Shader Cache Size: Unlimited",
            Description = "Removes the shader disk-cache size limit (Unlimited).",
            SettingId = 0x00AC8497,
            RecommendedValue = 0xffffffff,
            DefaultValue = 0x00004000,
            Benefit = "Fewer shader-compilation micro-stutters in open-world games.",
            Downsides = "Modern drivers already default this high, so the effect is often small; uses more disk.",
            Risk = RiskLevel.Low,
            Category = "Performance",
        },
        new NvidiaSetting
        {
            Id = "ogl-triple-buffer", Name = "Triple Buffering (OpenGL): On",
            Description = "Enables triple buffering for OpenGL titles.",
            SettingId = 0x20FDD1F9,
            RecommendedValue = 0x00000001,
            DefaultValue = 0x00000000,
            Benefit = "Smoother frame delivery in OpenGL games when V-Sync is on.",
            Downsides = "Only affects OpenGL titles; adds a frame of latency vs double buffering.",
            Risk = RiskLevel.Low,
            Category = "Sync",
        },
        new NvidiaSetting
        {
            Id = "aniso-sample-optimization", Name = "Anisotropic Sample Optimization: On",
            Description = "Enables the anisotropic sample optimization for texture filtering.",
            SettingId = 0x00E73211,
            RecommendedValue = 0x00000001,
            DefaultValue = 0x00000000,
            Benefit = "A few extra fps by reducing anisotropic-filtering work.",
            Downsides = "Very slightly softer textures at oblique angles.",
            Risk = RiskLevel.Low,
            Category = "Image quality",
        },
        new NvidiaSetting
        {
            Id = "negative-lod-bias-clamp", Name = "Negative LOD Bias: Clamp",
            Description = "Clamps negative LOD bias to reduce texture shimmering.",
            SettingId = 0x0019BB68,
            RecommendedValue = 0x00000001,
            DefaultValue = 0x00000000,
            Benefit = "Steadier textures in motion; less shimmering when apps force sharpening.",
            Downsides = "Textures can look marginally less sharp in a few older games.",
            Risk = RiskLevel.Low,
            Category = "Image quality",
        },
    };
}
