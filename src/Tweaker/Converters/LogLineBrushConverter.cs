using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Tweaker.Converters;

public sealed class LogLineBrushConverter : IValueConverter
{
    public Brush Normal { get; set; } = Brushes.Gray;
    public Brush Danger { get; set; } = Brushes.IndianRed;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        IsRevertOrError(value as string) ? Danger : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public static bool IsRevertOrError(string? line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        int bracket = line.IndexOf("] ", StringComparison.Ordinal);
        string message = bracket >= 0 ? line[(bracket + 2)..] : line;
        int gap = message.IndexOf("  ", StringComparison.Ordinal);
        if (bracket < 0 && gap >= 0) message = message[(gap + 2)..];
        return message.StartsWith("Reverted", StringComparison.Ordinal)
               || message.StartsWith("ERROR", StringComparison.Ordinal)
               || message.Contains(" — failed", StringComparison.Ordinal);
    }
}
