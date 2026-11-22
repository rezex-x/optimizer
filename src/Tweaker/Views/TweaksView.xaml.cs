using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class TweaksView : UserControl
{
    public TweaksView()
    {
        InitializeComponent();
        Loaded += (_, _) => Vm?.RefreshStates();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void RestorePointButton_Click(object sender, RoutedEventArgs e) => RestorePointHelper.CreateWithFeedback();

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is TweakPreset preset)
            Vm?.ApplyPreset(preset);
    }

    private void NumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ((TextBox)sender).GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    private void ValueCombo_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox combo || combo.IsDropDownOpen) return;
        e.Handled = true;
        if (combo.Parent is not UIElement parent) return;
        var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = sender };
        parent.RaiseEvent(args);
    }

    private void ContentScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.SelectedCategory != MainViewModel.AllCategory) return;

        string? top = null;
        double bestTop = double.NegativeInfinity;
        for (int i = 0; i < GroupsHost.Items.Count; i++)
        {
            if (GroupsHost.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement fe) continue;
            if (fe.DataContext is not CategoryGroup group) continue;
            GeneralTransform gt = fe.TransformToAncestor(ContentScroll);
            double y = gt.Transform(new Point(0, 0)).Y;
            if (y <= 24 && y > bestTop) { bestTop = y; top = group.Name; }
        }
        vm.ActiveCategory = top ?? (GroupsHost.Items.Count > 0
            && GroupsHost.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement f0
            && f0.DataContext is CategoryGroup g0 ? g0.Name : MainViewModel.AllCategory);
    }
}
