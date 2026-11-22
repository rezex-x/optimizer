using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Tweaker.Core.Services;
using Tweaker.Views;

namespace Tweaker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string flag = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tweaker", "disclaimer-ack");
        var gate = new DisclaimerGate(flag);

        if (gate.ShouldShow())
        {
            var dialog = new DisclaimerDialog();
            bool? ok = dialog.ShowDialog();
            if (ok != true) { Shutdown(); return; }
            if (dialog.DontShowAgain) gate.Acknowledge();
        }

        var main = new MainWindow();
        Application.Current.MainWindow = main;
        main.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        MessageBox.Show(
            "Something went wrong on that screen. It was logged and the app will keep running.\n\n" + e.Exception.Message,
            "Tweaker", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) Log(ex);
    }

    private static void Log(Exception ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {ex}\n\n");
        }
        catch { }
    }
}
