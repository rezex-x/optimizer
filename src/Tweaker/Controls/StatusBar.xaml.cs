using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Tweaker.Controls;

public partial class StatusBar : UserControl
{
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(
        nameof(Lines), typeof(IEnumerable), typeof(StatusBar), new PropertyMetadata(null, OnLinesChanged));

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(StatusBar),
        new PropertyMetadata(false, (d, _) => ((StatusBar)d).OnIsExpandedChanged()));

    private static readonly DependencyPropertyKey LastLinePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(LastLine), typeof(string), typeof(StatusBar), new PropertyMetadata("Ready"));
    public static readonly DependencyProperty LastLineProperty = LastLinePropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ToggleTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ToggleText), typeof(string), typeof(StatusBar), new PropertyMetadata("Log (0) ▴"));
    public static readonly DependencyProperty ToggleTextProperty = ToggleTextPropertyKey.DependencyProperty;

    public StatusBar() => InitializeComponent();

    public IEnumerable? Lines { get => (IEnumerable?)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public string LastLine => (string)GetValue(LastLineProperty);
    public string ToggleText => (string)GetValue(ToggleTextProperty);

    private static void OnLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bar = (StatusBar)d;
        if (e.OldValue is INotifyCollectionChanged oldLines) CollectionChangedEventManager.RemoveHandler(oldLines, bar.OnLinesCollectionChanged);
        if (e.NewValue is INotifyCollectionChanged newLines) CollectionChangedEventManager.AddHandler(newLines, bar.OnLinesCollectionChanged);
        bar.UpdateText();
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Dispatcher.CheckAccess()) UpdateText();
        else Dispatcher.BeginInvoke(new Action(UpdateText));
    }

    private void OnIsExpandedChanged()
    {
        UpdateText();
        if (IsExpanded) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ScrollToEnd));
    }

    private void ScrollToEnd() => (LogList?.Template?.FindName("LogScroll", LogList) as ScrollViewer)?.ScrollToEnd();

    private void UpdateText()
    {
        int count;
        string last;
        try
        {
            if (Lines is IList list)
            {
                count = list.Count;
                last = count > 0 ? list[count - 1]?.ToString() ?? "" : "";
            }
            else
            {
                List<string> lines = Lines?.Cast<object?>().Select(o => o?.ToString() ?? "").ToList() ?? new List<string>();
                count = lines.Count;
                last = count > 0 ? lines[^1] : "";
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            Dispatcher.BeginInvoke(new Action(UpdateText));
            return;
        }
        SetValue(LastLinePropertyKey, count > 0 ? last : "Ready");
        SetValue(ToggleTextPropertyKey, $"Log ({count}) {(IsExpanded ? "▾" : "▴")}");
        if (IsExpanded) ScrollToEnd();
    }
}
