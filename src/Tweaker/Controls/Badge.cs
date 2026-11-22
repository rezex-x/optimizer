using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tweaker.Controls;

public enum BadgeTone { Low, Medium, High, Neutral, Info, Purple }

public sealed class Badge : Border
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(Badge),
        new PropertyMetadata("", (d, e) => ((Badge)d)._label.Text = (string)e.NewValue));

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(BadgeTone), typeof(Badge),
        new PropertyMetadata(BadgeTone.Neutral, (d, _) => ((Badge)d).ApplyTone()));

    private readonly TextBlock _label = new() { FontSize = 9, VerticalAlignment = VerticalAlignment.Center };

    public Badge()
    {
        CornerRadius = new CornerRadius(2);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(5, 0, 5, 0);
        Child = _label;
        ApplyTone();
        Loaded += (_, _) => ApplyTone();
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public BadgeTone Tone { get => (BadgeTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    private void ApplyTone()
    {
        string tone = Tone.ToString();
        _label.Foreground = ToneBrush("Tone" + tone + "Fg");
        Background = ToneBrush("Tone" + tone + "Bg");
        BorderBrush = ToneBrush("Tone" + tone + "Border");
    }

    private Brush ToneBrush(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;
}
