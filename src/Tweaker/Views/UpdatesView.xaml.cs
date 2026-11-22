using System.Windows;
using System.Windows.Controls;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class UpdatesView : UserControl
{
    public UpdatesView() => InitializeComponent();

    private void Preset_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { DataContext: PresetRow row } && DataContext is UpdatesViewModel vm)
            vm.SelectedPreset = row.Value;
    }
}
