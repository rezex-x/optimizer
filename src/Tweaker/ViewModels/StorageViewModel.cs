using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class DriveRowViewModel
{
    public DriveRowViewModel(DriveHealth d)
    {
        Model = d.Model;
        Kind = DiagnosticsFormat.Kind(d.Kind);
        Smart = DiagnosticsFormat.Smart(d.Smart);
        Capacity = DiagnosticsFormat.Gb(d.CapacityGb);
    }
    public string Model { get; }
    public string Kind { get; }
    public string Smart { get; }
    public string Capacity { get; }
}

public sealed class VolumeRowViewModel
{
    public VolumeRowViewModel(VolumeUsage v)
    {
        Letter = v.Letter;
        Usage = DiagnosticsFormat.UsedOfTotalGb(v.FreeGb, v.SizeGb);
    }
    public string Letter { get; }
    public string Usage { get; }
}

public sealed class StorageViewModel : ObservableObject
{
    private readonly IStorageDiagnostic _storage;
    private bool _isRunning;

    public StorageViewModel(IStorageDiagnostic storage)
    {
        _storage = storage;
        RefreshCommand = new RelayCommand(async _ => { await RefreshAsync(); CommandManager.InvalidateRequerySuggested(); },
            _ => !IsRunning);
    }

    public ObservableCollection<DriveRowViewModel> Drives { get; } = new();
    public ObservableCollection<VolumeRowViewModel> Volumes { get; } = new();
    public bool IsRunning { get => _isRunning; private set => SetField(ref _isRunning, value); }
    public ICommand RefreshCommand { get; }

    public async Task RefreshAsync()
    {
        if (IsRunning) return;
        IsRunning = true;
        try
        {
            StorageResult r = await Task.Run(() => _storage.Read());
            Drives.Clear();
            foreach (DriveHealth d in r.Drives) Drives.Add(new DriveRowViewModel(d));
            Volumes.Clear();
            foreach (VolumeUsage v in r.Volumes) Volumes.Add(new VolumeRowViewModel(v));
        }
        finally
        {
            IsRunning = false;
        }
    }
}
