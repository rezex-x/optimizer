using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tweaker.Converters;

public sealed class VerticalThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Thickness t ? new Thickness(0, t.Top, 0, t.Bottom) : new Thickness(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
