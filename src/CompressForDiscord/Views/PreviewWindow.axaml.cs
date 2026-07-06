using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CompressForDiscord.ViewModels;

namespace CompressForDiscord.Views;

public partial class PreviewWindow : Window
{
    private PreviewWindowViewModel? _viewModel;

    public PreviewWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosingWindow;
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = DataContext as PreviewWindowViewModel;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        };
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        SizeToMedia();

        if (_viewModel is { ShowVideo: true } vm)
        {
            await AttachAndPlayAsync(vm);
        }
    }

    /// <summary>
    /// Attach the MediaPlayer to the native host only after the host handle exists, and only
    /// Play once libvlc has received the surface — playing earlier makes vlc open its own
    /// top-level window instead of embedding.
    /// </summary>
    private async Task AttachAndPlayAsync(PreviewWindowViewModel vm)
    {
        // Let the first layout pass finish so the NativeControlHost creates its handle.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        VideoView.MediaPlayer = vm.MediaPlayer;

        for (int i = 0; i < 50 && !vm.HasVideoSurface(); i++)
        {
            await Task.Delay(20);
        }

        if (!vm.HasVideoSurface())
        {
            Infrastructure.AppLog.Write("VideoView surface never attached; playing anyway (may float).");
        }

        vm.StartVideoPlayback();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PreviewWindowViewModel.IsFullscreen) && _viewModel is not null)
        {
            WindowState = _viewModel.IsFullscreen ? WindowState.FullScreen : WindowState.Normal;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel is { IsFullscreen: true } vm)
        {
            vm.IsFullscreen = false;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void SizeToMedia()
    {
        if (_viewModel is not { MediaWidth: > 0 and var w, MediaHeight: > 0 and var h })
        {
            return;
        }

        var workingArea = Screens.ScreenFromWindow(this)?.WorkingArea
                          ?? new PixelRect(0, 0, 1920, 1080);
        double scaling = RenderScaling;

        // Default footprint ≈ a fifth of the screen (√0.2 ≈ 0.45 of each axis);
        // resizable + fullscreen button cover everything else.
        double maxW = workingArea.Width / scaling * 0.45;
        double maxH = workingArea.Height / scaling * 0.45;

        const double chromeHeight = 130; // banner + footer allowance
        double mediaW = Math.Clamp(w, 480, maxW);
        double mediaH = mediaW * h / w;
        if (mediaH > maxH - chromeHeight)
        {
            mediaH = maxH - chromeHeight;
            mediaW = Math.Max(480, mediaH * w / h);
        }

        Width = mediaW;
        Height = mediaH + chromeHeight;
    }

    private void OnClosingWindow(object? sender, WindowClosingEventArgs e)
    {
        // Detach the native view first; the VM then stops/disposes libvlc OFF the UI thread
        // (stopping from the UI thread is a known LibVLCSharp deadlock).
        VideoView.MediaPlayer = null;
        _viewModel?.Shutdown();
    }
}
