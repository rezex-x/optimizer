using System.Linq;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Services;

public class ConfigFormatsTests
{
    private const string Json =
        "{\n" +
        "  // tolerated comment\n" +
        "  \"fps\": 144,\n" +
        "  \"graphics\": { \"vsync\": true, \"quality\": \"high\", \"scale\": 0.75, \"list\": [1, 2] },\n" +
        "  \"name\": null\n" +
        "}";

    [Fact]
    public void Json_leaves_become_entries_with_the_top_level_key_as_section()
    {
        JsonConfigDocument doc = JsonConfigDocument.Parse(Json)!;

        Assert.Equal(ConfigFormat.Json, doc.Format);
        Assert.Equal(new[]
        {
            ("", "fps", "144"),
            ("graphics", "vsync", "true"),
            ("graphics", "quality", "high"),
            ("graphics", "scale", "0.75"),
            ("graphics", "list[0]", "1"),
            ("graphics", "list[1]", "2"),
            ("", "name", "null"),
        }, doc.Entries.Select(e => (e.Section, e.Key, e.Value)));
    }

    [Fact]
    public void Json_values_keep_their_type_and_key_order_on_save()
    {
        JsonConfigDocument doc = JsonConfigDocument.Parse(Json)!;

        doc.Set(0, "240");
        doc.Set(1, "false");
        doc.Set(2, "low");
        doc.Set(3, "1.5");
        string text = doc.Serialize();

        Assert.Contains("\"fps\": 240", text);
        Assert.Contains("\"vsync\": false", text);
        Assert.Contains("\"quality\": \"low\"", text);
        Assert.Contains("\"scale\": 1.5", text);
        Assert.True(text.IndexOf("\"fps\"") < text.IndexOf("\"graphics\"") && text.IndexOf("\"graphics\"") < text.IndexOf("\"name\""));
        Assert.Equal("240", doc.Entries[0].Value);
    }

    [Fact]
    public void Invalid_json_is_refused()
    {
        Assert.Null(JsonConfigDocument.Parse("{ nope"));
        Assert.Null(JsonConfigDocument.Parse("42"));
        Assert.Null(ConfigFormats.Open(@"C:\x\settings.json", "{ nope"));
    }

    [Theory]
    [InlineData(@"C:\g\GameUserSettings.ini", "[S]\na=1", ConfigFormat.Ini)]
    [InlineData(@"C:\g\cs2_video.txt", "\"video.cfg\"\n{\n\t\"a\"\t\"1\"\n}", ConfigFormat.KeyValues)]
    [InlineData(@"C:\g\notes.txt", "a=1\nb=2", ConfigFormat.Ini)]
    [InlineData(@"C:\g\autoexec.cfg", "fps_max 400", ConfigFormat.Cfg)]
    [InlineData(@"C:\g\settings.json", "{ \"a\": 1 }", ConfigFormat.Json)]
    [InlineData(@"C:\g\settings.conf", "{ \"a\": 1 }", ConfigFormat.Json)]
    [InlineData(@"C:\g\settings.conf", "[S]\na=1", ConfigFormat.Ini)]
    [InlineData(@"C:\g\settings.conf", "fps_max 1", ConfigFormat.Cfg)]
    [InlineData(@"C:\g\settings.conf", "[1, 2, 3]", ConfigFormat.Json)]
    [InlineData(@"C:\g\readme.txt", "See https://example.com/dl?v=2 for details", ConfigFormat.Cfg)]
    public void The_format_comes_from_the_extension_or_the_content(string path, string text, ConfigFormat expected)
    {
        Assert.Equal(expected, ConfigFormats.Open(path, text)!.Format);
    }
}
