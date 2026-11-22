using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Tweaker.Services;

namespace Tweaker.Converters;

public sealed class UsageLevelBrushConverter : IValueConverter
{
    public static string ResourceKey(UsageLevel level) => level switch
    {
        UsageLevel.Hot => "LevelHot",
        UsageLevel.Warm => "LevelWarm",
        _ => "UsageNormal",
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Application.Current?.TryFindResource(ResourceKey(value is UsageLevel level ? level : UsageLevel.Normal)) as Brush
        ?? Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
