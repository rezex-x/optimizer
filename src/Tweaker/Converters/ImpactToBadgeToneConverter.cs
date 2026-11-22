using System.Globalization;
using System.Windows.Data;
using Tweaker.Controls;
using Tweaker.Core.Catalog;

namespace Tweaker.Converters;

public sealed class ImpactToBadgeToneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Impact impact
            ? impact switch
            {
                Impact.High => BadgeTone.Purple,
                Impact.Medium => BadgeTone.Info,
                _ => BadgeTone.Neutral,
            }
            : BadgeTone.Neutral;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
