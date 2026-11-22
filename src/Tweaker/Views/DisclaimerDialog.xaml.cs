using System.Windows;
using Tweaker.Core.Services;

namespace Tweaker.Views;

public partial class DisclaimerDialog : Window
{
    private readonly RestorePointService _restore;

    public bool DontShowAgain { get; private set; }

    public DisclaimerDialog()
    {
        InitializeComponent();
        _restore = new RestorePointService(new ProcessRunner());
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        RestoreButton.IsEnabled = false;
        RestoreStatus.Text = "Creating restore point…";
        var result = _restore.Create("Tweaker — before changes");
        RestoreStatus.Text = result.Message;
        if (!result.Success) RestoreButton.IsEnabled = true;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        DontShowAgain = DontShow.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
