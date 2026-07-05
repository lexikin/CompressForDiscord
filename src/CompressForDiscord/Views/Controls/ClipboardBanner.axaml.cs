using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using CompressForDiscord.ViewModels;

namespace CompressForDiscord.Views.Controls;

public partial class ClipboardBanner : UserControl
{
    private readonly ScaleTransform _countdownScale = new(1, 1);
    private PreviewWindowViewModel? _viewModel;

    public ClipboardBanner()
    {
        InitializeComponent();
        CountdownBar.RenderTransform = _countdownScale;
        DataContextChanged += (_, _) => HookViewModel();
    }

    private void HookViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PreviewWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _countdownScale.ScaleX = _viewModel.BannerRemaining;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PreviewWindowViewModel.BannerRemaining) && _viewModel is not null)
        {
            // Transforms aren't part of the logical tree, so a XAML binding can't reach the
            // DataContext — drive the scale directly instead.
            _countdownScale.ScaleX = _viewModel.BannerRemaining;
        }
    }
}
