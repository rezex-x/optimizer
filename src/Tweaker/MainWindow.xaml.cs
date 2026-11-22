using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Tweaker.Composition;
using Tweaker.Core.Services;

namespace Tweaker;

public partial class MainWindow : Window
{
    private readonly WindowStateStore _windowStore = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker", "window.json"));

    private readonly DispatcherTimer _profileWatch = new() { Interval = TimeSpan.FromSeconds(2) };

    private readonly DispatcherTimer _liveStats = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = AppServices.BuildShell();
        OsInfoText.Text = DescribeOs();
        RestorePlacement();
        Closing += (_, _) => SavePlacement();
        Closing += (_, _) => (DataContext as Tweaker.Shell.ShellViewModel)?.Resources.Dispose();
        _profileWatch.Tick += async (_, _) =>
        {
            if (DataContext is Tweaker.Shell.ShellViewModel shell) await shell.Resources.Profiles.Manager.TickAsync();
        };
        _profileWatch.Start();
        Closing += (_, _) => _profileWatch.Stop();
        _liveStats.Tick += async (_, _) =>
        {
            if (WindowState != WindowState.Minimized && DataContext is Tweaker.Shell.ShellViewModel shell)
                await shell.Resources.Performance.RefreshAsync();
        };
        Loaded += (_, _) =>
        {
            if (DataContext is Tweaker.Shell.ShellViewModel shell) _ = shell.Resources.Performance.RefreshAsync();
            _liveStats.Start();
        };
        Closing += (_, _) => _liveStats.Stop();
    }

    private void RestorePlacement()
    {
        WindowPlacement? p = _windowStore.Load();
        if (p is null) return;
        if (!WindowStateStore.IsReasonablyOnScreen(p,
                SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight))
            return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = p.Left;
        Top = p.Top;
        Width = p.Width;
        Height = p.Height;
        if (p.Maximized) WindowState = WindowState.Maximized;
    }

    private void SavePlacement()
    {
        Rect b = RestoreBounds.IsEmpty ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        _windowStore.Save(new WindowPlacement(b.Left, b.Top, b.Width, b.Height,
            WindowState == WindowState.Maximized));
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        RootBorder.Margin = WindowState == WindowState.Maximized
            ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + SystemParameters.FixedFrameHorizontalBorderHeight)
            : new Thickness(0);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxRestore_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static string DescribeOs()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string product = key?.GetValue("ProductName") as string ?? "Windows";
            string display = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string ?? "";
            if (int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out int build) && build >= 22000 && product.Contains("Windows 10"))
                product = product.Replace("Windows 10", "Windows 11");
            return string.IsNullOrEmpty(display) ? product : $"{product} · {display}";
        }
        catch { return "Windows"; }
    }
}
