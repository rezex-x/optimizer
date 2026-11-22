using System.Windows.Controls;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class PerformanceView : UserControl
{
    public PerformanceView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is PerformanceViewModel vm) _ = vm.LoadSystemInfoAsync();
        };
    }
}
