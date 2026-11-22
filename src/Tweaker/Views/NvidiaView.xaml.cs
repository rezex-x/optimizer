using System.Windows;
using System.Windows.Controls;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class NvidiaView : UserControl
{
    public NvidiaView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as NvidiaViewModel)?.Refresh();
    }

    private void RestorePointButton_Click(object sender, RoutedEventArgs e) => RestorePointHelper.CreateWithFeedback();
}
