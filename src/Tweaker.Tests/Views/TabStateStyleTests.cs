using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace Tweaker.Tests.Views;

public class TabStateStyleTests
{
    [Theory]
    [InlineData("TabButton")]
    [InlineData("SubTabButton")]
    [InlineData("SegmentedButton")]
    public void An_active_button_looks_different_from_an_inactive_one(string styleKey)
    {
        (string active, string inactive) = RunSta(() =>
        {
            _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
            var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/tweaker;component/Themes/Theme.xaml", UriKind.Relative));
            return (Look(theme, styleKey, true), Look(theme, styleKey, false));
        });

        Assert.NotEqual(inactive, active);
    }

    private static string Look(ResourceDictionary theme, string styleKey, bool tag)
    {
        var button = new Button { Content = "X", Style = (Style)theme[styleKey], Tag = tag };
        var window = new Window
        {
            Content = button, Width = 200, Height = 60, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            Left = -10000, Top = -10000, ShowActivated = false,
        };
        window.Show();
        try
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var border = (Border)button.Template.FindName("b", button);
            return $"{Describe(border.Background)}|{Describe(border.BorderBrush)}|{Describe(button.Foreground)}";
        }
        finally
        {
            window.Close();
        }
    }

    private static string Describe(Brush? brush) => brush switch
    {
        SolidColorBrush s => s.Color.ToString(),
        null => "null",
        _ => brush.GetType().Name,
    };

    private static T RunSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new InvalidOperationException("STA body failed", error);
        return result;
    }
}
