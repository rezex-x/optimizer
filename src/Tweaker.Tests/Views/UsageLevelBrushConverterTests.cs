using Tweaker.Converters;
using Tweaker.Services;
using Xunit;

namespace Tweaker.Tests.Views;

public class UsageLevelBrushConverterTests
{
    [Fact]
    public void Each_level_maps_to_its_theme_brush()
    {
        Assert.Equal("UsageNormal", UsageLevelBrushConverter.ResourceKey(UsageLevel.Normal));
        Assert.Equal("LevelWarm", UsageLevelBrushConverter.ResourceKey(UsageLevel.Warm));
        Assert.Equal("LevelHot", UsageLevelBrushConverter.ResourceKey(UsageLevel.Hot));
    }
}
