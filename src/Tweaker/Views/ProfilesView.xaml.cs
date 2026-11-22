using System.Windows;
using System.Windows.Controls;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ProfilesViewModel vm) await vm.RefreshAsync();
        };
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProfilesViewModel vm) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pick the game's exe",
            Filter = "Programs (*.exe)|*.exe",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) vm.Trigger = dialog.FileName;
    }

    private void TriggerBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && sender is TextBox box)
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
