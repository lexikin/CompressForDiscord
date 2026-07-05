using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompressForDiscord.Models;

namespace CompressForDiscord.ViewModels;

internal sealed partial class ProgressWindowViewModel : ObservableObject
{
    private readonly CancellationTokenSource _cts;

    [ObservableProperty]
    private string _fileName;

    [ObservableProperty]
    private string _phase = "Starting…";

    [ObservableProperty]
    private double _percent;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    private bool _canCancel = true;

    public ProgressWindowViewModel(string fileName, CancellationTokenSource cts)
    {
        _fileName = fileName;
        _cts = cts;
    }

    public void Report(CompressionProgress progress)
    {
        IsIndeterminate = progress.Percent < 0;
        if (progress.Percent >= 0)
        {
            Percent = progress.Percent;
        }

        if (CanCancel) // don't overwrite "Cancelling…"
        {
            Phase = progress.Phase;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CanCancel = false;
        Phase = "Cancelling…";
        _cts.Cancel();
    }
}
