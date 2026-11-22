using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Tweaker.Controls;

public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SlotsProperty = DependencyProperty.Register(
        nameof(Slots), typeof(int), typeof(Sparkline),
        new FrameworkPropertyMetadata(40, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SeriesBrushProperty = DependencyProperty.Register(
        nameof(SeriesBrush), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public Sparkline() => RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int Slots { get => (int)GetValue(SlotsProperty); set => SetValue(SlotsProperty, value); }
    public Brush SeriesBrush { get => (Brush)GetValue(SeriesBrushProperty); set => SetValue(SeriesBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        IReadOnlyList<double>? values = Values;
        if (values is null) return;
        foreach (SparkBar bar in SparklineLayout.Compute(values, Maximum, Slots, ActualWidth, ActualHeight))
        {
            dc.PushOpacity(bar.Opacity);
            dc.DrawRectangle(SeriesBrush, null, new Rect(bar.X, bar.Y, bar.Width, bar.Height));
            dc.Pop();
        }
    }
}
