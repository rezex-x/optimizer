using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Tweaker.Core.Models;

namespace Tweaker.Converters;

public sealed class RiskToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        RiskLevel.Low => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
        RiskLevel.Medium => new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x00)),
        RiskLevel.High => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
        _ => Brushes.Gray
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
