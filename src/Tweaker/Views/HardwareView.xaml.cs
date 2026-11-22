using System.Windows.Controls;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class HardwareView : UserControl
{
    public HardwareView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is not HardwareViewModel vm) return;
            _ = vm.RefreshAsync();
            _ = vm.Storage.RefreshAsync();
        };
    }
}
