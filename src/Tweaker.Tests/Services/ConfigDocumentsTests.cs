using System.Linq;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ConfigDocumentsTests
{
    private const string Ini =
        "; comment\r\n" +
        "[/Script/FortniteGame.FortGameUserSettings]\r\n" +
        "FrameRateLimit=240.000000\r\n" +
        "bUseVSync=False\r\n" +
        " ResolutionSizeX = 1920 \r\n" +
        "Name=\"Player One\"\r\n" +
        "\r\n" +
        "[ScalabilityGroups]\r\n" +
        "sg.ShadowQuality=0\r\n" +
        "+Paths=A\r\n" +
        "+Paths=B\r\n";

    [Fact]
    public void Ini_entries_keep_sections_order_quotes_and_duplicates()
    {
        IConfigDocument doc = IniDocument.Parse(Ini);

        Assert.Equal(ConfigFormat.Ini, doc.Format);
        Assert.Equal(new[]
        {
            ("/Script/FortniteGame.FortGameUserSettings", "FrameRateLimit", "240.000000"),
            ("/Script/FortniteGame.FortGameUserSettings", "bUseVSync", "False"),
            ("/Script/FortniteGame.FortGameUserSettings", "ResolutionSizeX", "1920"),
            ("/Script/FortniteGame.FortGameUserSettings", "Name", "Player One"),
            ("ScalabilityGroups", "sg.ShadowQuality", "0"),
            ("ScalabilityGroups", "+Paths", "A"),
            ("ScalabilityGroups", "+Paths", "B"),
        }, doc.Entries.Select(e => (e.Section, e.Key, e.Value)));
    }

    [Theory]
    [InlineData(Ini)]
    [InlineData("a=1\nb=2")]
    [InlineData("top=1\n[S]\nk=v\n")]
    [InlineData("")]
    [InlineData("a=1\nb=2\r\nc=3")]
    [InlineData("a=1\r\n\r\nb=2\n")]
    public void An_unchanged_ini_round_trips_exactly(string text)
    {
        Assert.Equal(text, IniDocument.Parse(text).Serialize());
    }

    [Fact]
    public void Setting_ini_values_rewrites_only_the_value_characters()
    {
        IConfigDocument doc = IniDocument.Parse(Ini);

        doc.Set(0, "144.000000");
        doc.Set(2, "2560");
        doc.Set(3, "Two");

        Assert.Equal(Ini
            .Replace("FrameRateLimit=240.000000", "FrameRateLimit=144.000000")
            .Replace(" ResolutionSizeX = 1920 ", " ResolutionSizeX = 2560 ")
            .Replace("Name=\"Player One\"", "Name=\"Two\""), doc.Serialize());
        Assert.Equal("2560", doc.Entries[2].Value);
    }

    [Fact]
    public void A_value_never_gets_a_line_break()
    {
        IConfigDocument doc = IniDocument.Parse("k=v\r\nz=1\r\n");
        doc.Set(0, "a\r\nb");
        Assert.Equal("k=ab\r\nz=1\r\n", doc.Serialize());
        Assert.Equal("ab", doc.Entries[0].Value);
    }

    [Fact]
    public void Mixed_line_endings_survive_an_edit()
    {
        IConfigDocument doc = IniDocument.Parse("a=1\nb=2\r\nc=3");
        doc.Set(1, "20");
        Assert.Equal("a=1\nb=20\r\nc=3", doc.Serialize());
    }

    [Fact]
    public void A_stray_byte_order_mark_does_not_hide_the_first_section()
    {
        IConfigDocument doc = IniDocument.Parse("\uFEFF[S]\nk=v\n");
        Assert.Equal(("S", "k"), (doc.Entries[0].Section, doc.Entries[0].Key));
        Assert.Equal("\uFEFF[S]\nk=v\n", doc.Serialize());
    }

    [Fact]
    public void Ini_keys_before_any_section_have_an_empty_section()
    {
        IConfigDocument doc = IniDocument.Parse("top=1\n[S]\nk=v\n");
        Assert.Equal(("", "top"), (doc.Entries[0].Section, doc.Entries[0].Key));
        Assert.Equal(("S", "k"), (doc.Entries[1].Section, doc.Entries[1].Key));
    }

    private const string Kv =
        "\"video.cfg\"\n" +
        "{\n" +
        "\t\"Version\"\t\t\"15\"\n" +
        "\t\"setting.defaultres\"\t\t\"1920\"   // width\n" +
        "\t\"setting.fullscreen\"\t\t\"1\"\n" +
        "}\n";

    [Fact]
    public void KeyValues_pairs_use_the_innermost_block_as_section_and_keep_comments()
    {
        IConfigDocument doc = KeyValuesDocument.Parse(Kv);

        Assert.Equal(ConfigFormat.KeyValues, doc.Format);
        Assert.Equal(new[] { ("video.cfg", "Version", "15"), ("video.cfg", "setting.defaultres", "1920"), ("video.cfg", "setting.fullscreen", "1") },
            doc.Entries.Select(e => (e.Section, e.Key, e.Value)));
        Assert.Equal(Kv, doc.Serialize());

        doc.Set(1, "2560");
        Assert.Equal(Kv.Replace("\"1920\"   // width", "\"2560\"   // width"), doc.Serialize());
    }

    [Fact]
    public void KeyValues_blocks_nest_and_several_pairs_can_share_a_line()
    {
        IConfigDocument doc = KeyValuesDocument.Parse("\"a\" { \"b\" { \"k\" \"v\" \"k2\" \"long value\" } }");

        Assert.Equal(new[] { ("b", "k", "v"), ("b", "k2", "long value") }, doc.Entries.Select(e => (e.Section, e.Key, e.Value)));
        doc.Set(0, "longer");
        doc.Set(1, "x");
        Assert.Equal("\"a\" { \"b\" { \"k\" \"longer\" \"k2\" \"x\" } }", doc.Serialize());
    }

    private const string Cfg =
        "// my config\n" +
        "fps_max 400\n" +
        "cl_showfps \"1\"\n" +
        "bind \"space\" \"+jump\"\n" +
        "sensitivity \"1.25\" // mouse\n";

    [Fact]
    public void Cfg_name_value_lines_are_entries_and_other_lines_stay_untouched()
    {
        IConfigDocument doc = CfgDocument.Parse(Cfg);

        Assert.Equal(ConfigFormat.Cfg, doc.Format);
        Assert.Equal(new[] { ("", "fps_max", "400"), ("", "cl_showfps", "1"), ("", "sensitivity", "1.25") },
            doc.Entries.Select(e => (e.Section, e.Key, e.Value)));
        Assert.Equal(Cfg, doc.Serialize());

        doc.Set(2, "1.5");
        doc.Set(0, "240");
        Assert.Equal(Cfg.Replace("fps_max 400", "fps_max 240").Replace("\"1.25\" // mouse", "\"1.5\" // mouse"), doc.Serialize());
    }

    [Fact]
    public void Cfg_commands_are_not_values_and_a_semicolon_ends_a_value()
    {
        const string text = "fps_max 0;\nexec autoexec\nunbind mouse1\nvolume \"0.5\"\n";
        IConfigDocument doc = CfgDocument.Parse(text);

        Assert.Equal(new[] { ("", "fps_max", "0"), ("", "volume", "0.5") }, doc.Entries.Select(e => (e.Section, e.Key, e.Value)));
        Assert.Equal(text, doc.Serialize());

        doc.Set(0, "240");
        Assert.Equal(text.Replace("fps_max 0;", "fps_max 240;"), doc.Serialize());
        Assert.Empty(CfgDocument.Parse("EXEC autoexec\nBind x y\n").Entries);
    }

    [Fact]
    public void Tokens_are_quoted_or_bare_and_stop_at_a_comment()
    {
        var tokens = LineTokens.Read("  \"a b\" c{ } // \"ignored\"");
        Assert.Equal(4, tokens.Count);
        Assert.Equal((3, 3, true, false), (tokens[0].Start, tokens[0].Length, tokens[0].Quoted, tokens[0].IsBrace));
        Assert.Equal((8, 1, false, false), (tokens[1].Start, tokens[1].Length, tokens[1].Quoted, tokens[1].IsBrace));
        Assert.True(tokens[2].IsBrace);
        Assert.True(tokens[3].IsBrace);
    }
}
