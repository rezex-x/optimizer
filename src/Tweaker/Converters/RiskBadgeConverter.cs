using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Tweaker.Core.Models;

namespace Tweaker.Converters;

public sealed class RiskBadgeConverter : IValueConverter
{
    private static SolidColorBrush Brush(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string part = (parameter as string ?? "fg").ToLowerInvariant();
        var risk = value is RiskLevel r ? r : RiskLevel.Low;

        (string fg, string bg, string border) = risk switch
        {
            RiskLevel.Low    => ("#7AB87A", "#1A2E1A", "#2A462A"),
            RiskLevel.Medium => ("#C39A52", "#2A1F0E", "#48381A"),
            RiskLevel.High   => ("#C05A5A", "#2A1010", "#4A1C1C"),
            _                => ("#7AB87A", "#1A2E1A", "#2A462A"),
        };

        return part switch
        {
            "bg" => Brush(bg),
            "border" => Brush(border),
            _ => Brush(fg),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
