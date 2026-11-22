using System.Windows;
using System.Windows.Controls;

namespace Tweaker.Views;

public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as Tweaker.ViewModels.ConfigViewModel)?.RefreshStates();
    }

    private void RestorePointButton_Click(object sender, RoutedEventArgs e) => RestorePointHelper.CreateWithFeedback();
}
