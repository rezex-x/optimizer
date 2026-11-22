using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class InspectorView : UserControl
{
    public InspectorView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is not InspectorViewModel vm) return;
            await vm.LoadDetectedAsync();
            await vm.RefreshGameWarningAsync();
        };
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InspectorViewModel vm) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open a config file",
            Filter = "Config files (*.ini;*.cfg;*.json;*.txt;*.vdf)|*.ini;*.cfg;*.json;*.txt;*.vdf|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) await vm.OpenAsync(dialog.FileName);
    }

    private void NumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ((TextBox)sender).GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not InspectorViewModel vm || vm.IsOpen) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) await vm.OpenAsync(files[0]);
    }
}
