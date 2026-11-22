using System.Threading.Tasks;
using System.Windows.Input;
using Tweaker.Services;

namespace Tweaker.ViewModels;

public sealed class DpcSampleViewModel : ObservableObject
{
    private readonly IDpcDiagnostic _dpc;
    private string _dpcTime = "—", _interruptTime = "—", _dpcsQueued = "—", _interruptsRate = "—";
    private string _buttonLabel = "Sample";
    private bool _isRunning;

    public DpcSampleViewModel(IDpcDiagnostic dpc)
    {
        _dpc = dpc;
        SampleCommand = new RelayCommand(async _ => { await SampleAsync(); CommandManager.InvalidateRequerySuggested(); },
            _ => !IsRunning);
    }

    public string DpcTime { get => _dpcTime; private set => SetField(ref _dpcTime, value); }
    public string InterruptTime { get => _interruptTime; private set => SetField(ref _interruptTime, value); }
    public string DpcsQueued { get => _dpcsQueued; private set => SetField(ref _dpcsQueued, value); }
    public string InterruptsRate { get => _interruptsRate; private set => SetField(ref _interruptsRate, value); }
    public string ButtonLabel { get => _buttonLabel; private set => SetField(ref _buttonLabel, value); }
    public bool IsRunning { get => _isRunning; private set => SetField(ref _isRunning, value); }
    public ICommand SampleCommand { get; }

    public async Task SampleAsync()
    {
        if (IsRunning) return;
        IsRunning = true;
        ButtonLabel = "Sampling…";
        try
        {
            DpcResult r = await _dpc.SampleAsync();
            DpcTime = DiagnosticsFormat.Pct(r.DpcTimePct);
            InterruptTime = DiagnosticsFormat.Pct(r.InterruptTimePct);
            DpcsQueued = DiagnosticsFormat.Rate(r.DpcsQueuedPerSec);
            InterruptsRate = DiagnosticsFormat.Rate(r.InterruptsPerSec);
        }
        finally
        {
            IsRunning = false;
            ButtonLabel = "Sample";
        }
    }
}
