using Tweaker.Core.Catalog;
using Xunit;

namespace Tweaker.Tests.Catalog;

public class MythNotesTests
{
    [Theory]
    [InlineData("hpet", "HPET")]
    [InlineData("disable HPET", "HPET")]
    [InlineData("windows defender", "Defender")]
    [InlineData("Windows Update", "Updates tab")]
    [InlineData("realtime priority", "Realtime")]
    [InlineData("spectre", "mitigations")]
    [InlineData("dep", "Data Execution Prevention")]
    [InlineData("LargeSystemCache", "Windows XP")]
    [InlineData("irq8priority", "NT era")]
    [InlineData("tcp auto-tuning", "auto-tuning")]
    [InlineData("write cache", "write-cache")]
    [InlineData("ram cleaner", "RAM cleaners")]
    [InlineData("Anti-Lag+", "Anti-Lag 2")]
    [InlineData("gaming dns", "DNS")]
    public void A_myth_search_explains_why_it_is_left_out(string search, string expected)
    {
        Assert.Contains(expected, MythNotes.For(search));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mouse")]
    [InlineData("depth")]
    [InlineData("hpet2")]
    [InlineData("update")]
    public void Ordinary_searches_show_no_note(string? search)
    {
        Assert.Null(MythNotes.For(search));
    }

    [Fact]
    public void Keywords_match_whole_words_only()
    {
        Assert.True(MythNotes.ContainsWord("turn off DEP now", "dep"));
        Assert.False(MythNotes.ContainsWord("depth", "dep"));
        Assert.True(MythNotes.ContainsWord("amd anti-lag+", "anti-lag+"));
        Assert.False(MythNotes.ContainsWord("xhpet", "hpet"));
    }
}
