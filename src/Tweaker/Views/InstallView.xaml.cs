using System.Windows.Controls;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class InstallView : UserControl
{
    public InstallView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is InstallViewModel vm) _ = vm.EnsureDetectedAsync();
    }
}
