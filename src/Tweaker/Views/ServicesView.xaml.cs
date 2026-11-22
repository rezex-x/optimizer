using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Tweaker.ViewModels;

namespace Tweaker.Views;

public partial class ServicesView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };

    public ServicesView()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) =>
        {
            if (DataContext is ServicesViewModel vm) await vm.RefreshAsync();
        };
        Loaded += async (_, _) =>
        {
            _timer.Start();
            if (DataContext is not ServicesViewModel vm) return;
            ICollectionView view = CollectionViewSource.GetDefaultView(vm.Rows);
            if (view.GroupDescriptions.Count == 0)
            {
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ServiceRowViewModel.GroupTitle)));
                view.SortDescriptions.Add(new SortDescription(nameof(ServiceRowViewModel.GroupOrder), ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription(nameof(ServiceRowViewModel.DisplayName), ListSortDirection.Ascending));
            }
            if (vm.Rows.Count == 0) await vm.RefreshAsync();
            else _ = vm.RefreshAsync();
            Table.ItemsSource = view;
            RefitColumns();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void RefitColumns()
    {
        foreach (DataGridColumn column in Table.Columns)
        {
            DataGridLength width = column.Width;
            column.Width = new DataGridLength(0);
            column.Width = width;
        }
        Table.UpdateLayout();
    }
}
