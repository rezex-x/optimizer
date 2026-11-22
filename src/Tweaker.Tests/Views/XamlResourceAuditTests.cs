using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Tweaker.Tests.Views;

public class XamlResourceAuditTests
{
    private static readonly Regex KeyDefinition = new(@"x:Key=""([^""]+)""");
    private static readonly Regex KeyUse = new(@"\{StaticResource\s+([^\s}]+)\}");

    private static string AppSourceDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Tweaker", "Themes")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not find src/Tweaker above the test output folder");
        return Path.Combine(dir!.FullName, "src", "Tweaker");
    }

    private static IEnumerable<string> Keys(string xaml) => KeyDefinition.Matches(xaml).Select(m => m.Groups[1].Value);

    [Fact]
    public void Every_StaticResource_key_used_in_xaml_is_defined()
    {
        string src = AppSourceDir();
        var shared = new HashSet<string>(Keys(File.ReadAllText(Path.Combine(src, "Themes", "Theme.xaml"))));
        shared.UnionWith(Keys(File.ReadAllText(Path.Combine(src, "App.xaml"))));

        string sep = Path.DirectorySeparatorChar.ToString();
        var missing = new List<string>();
        foreach (string file in Directory.EnumerateFiles(src, "*.xaml", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep)))
        {
            string text = File.ReadAllText(file);
            var local = new HashSet<string>(Keys(text));
            foreach (Match use in KeyUse.Matches(text))
            {
                string key = use.Groups[1].Value;
                if (!shared.Contains(key) && !local.Contains(key))
                    missing.Add(Path.GetFileName(file) + ": " + key);
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void Theme_xaml_defines_every_key_before_its_first_use()
    {
        string theme = File.ReadAllText(Path.Combine(AppSourceDir(), "Themes", "Theme.xaml"));
        var definedAt = new Dictionary<string, int>();
        foreach (Match m in KeyDefinition.Matches(theme)) definedAt.TryAdd(m.Groups[1].Value, m.Index);

        var forward = new List<string>();
        foreach (Match use in KeyUse.Matches(theme))
        {
            string key = use.Groups[1].Value;
            if (definedAt.TryGetValue(key, out int at) && at > use.Index) forward.Add(key);
        }

        Assert.Empty(forward);
    }

    [Fact]
    public void Theme_defines_every_badge_tone_brush()
    {
        var keys = new HashSet<string>(Keys(File.ReadAllText(Path.Combine(AppSourceDir(), "Themes", "Theme.xaml"))));
        var missing = Enum.GetNames<Tweaker.Controls.BadgeTone>()
            .SelectMany(t => new[] { "Tone" + t + "Fg", "Tone" + t + "Bg", "Tone" + t + "Border" })
            .Where(k => !keys.Contains(k))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Theme_defines_every_usage_level_brush()
    {
        var keys = new HashSet<string>(Keys(File.ReadAllText(Path.Combine(AppSourceDir(), "Themes", "Theme.xaml"))));
        var missing = Enum.GetValues<Tweaker.Services.UsageLevel>()
            .Select(Tweaker.Converters.UsageLevelBrushConverter.ResourceKey)
            .Where(k => !keys.Contains(k))
            .ToList();

        Assert.Empty(missing);
    }
}
