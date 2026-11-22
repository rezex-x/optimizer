using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class ProcessesView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.5) };

    public ProcessesView()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) =>
        {
            if (DataContext is ProcessesViewModel vm) await vm.RefreshAsync();
        };
        Loaded += (_, _) =>
        {
            _timer.Start();
            if (DataContext is ProcessesViewModel vm)
            {
                ShowSortArrow(vm);
                _ = vm.RefreshAsync();
            }
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Table_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not ProcessesViewModel vm) return;
        vm.SortBy(e.Column.SortMemberPath);
        ShowSortArrow(vm);
    }

    private void ShowSortArrow(ProcessesViewModel vm)
    {
        foreach (DataGridColumn column in Table.Columns)
        {
            column.SortDirection = column.SortMemberPath == vm.SortColumn
                ? (vm.SortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending)
                : null;
        }
    }
}
