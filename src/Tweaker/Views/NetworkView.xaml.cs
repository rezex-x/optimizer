using System;
using System.Windows.Controls;
using System.Windows.Threading;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class NetworkView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public NetworkView()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) =>
        {
            if (DataContext is NetworkViewModel vm) await vm.RefreshAsync();
        };
        Loaded += (_, _) =>
        {
            _timer.Start();
            if (DataContext is NetworkViewModel vm) _ = vm.RefreshAsync();
        };
        Unloaded += (_, _) => _timer.Stop();
    }
}
