using System.Globalization;
using Tweaker.Controls;
using Tweaker.Converters;
using Tweaker.Core.Catalog;
using Xunit;

namespace Tweaker.Tests.Views;

public class ImpactToBadgeToneConverterTests
{
    [Fact]
    public void Impacts_use_tones_that_are_not_risk_colours()
    {
        var converter = new ImpactToBadgeToneConverter();
        object Tone(Impact i) => converter.Convert(i, typeof(BadgeTone), null!, CultureInfo.InvariantCulture);

        Assert.Equal(BadgeTone.Purple, Tone(Impact.High));
        Assert.Equal(BadgeTone.Info, Tone(Impact.Medium));
        Assert.Equal(BadgeTone.Neutral, Tone(Impact.Low));
    }
}
