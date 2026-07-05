using System;
using Avalonia;
using Avalonia.Controls;
using CompressForDiscord.ViewModels;

namespace CompressForDiscord.Views;

public partial class PreviewWindow : Window
{
    public PreviewWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosingWindow;
    }

    private PreviewWindowViewModel? ViewModel => DataContext as PreviewWindowViewModel;

    private void OnOpened(object? sender, EventArgs e)
    {
        SizeToMedia();
        // The native VideoView handle only exists once the window is open.
        ViewModel?.StartVideoPlayback();
    }

    private void SizeToMedia()
    {
        if (ViewModel is not { MediaWidth: > 0 and var w, MediaHeight: > 0 and var h })
        {
            return;
        }

        var workingArea = Screens.ScreenFromWindow(this)?.WorkingArea
                          ?? new PixelRect(0, 0, 1920, 1080);
        double scaling = RenderScaling;
        double maxW = workingArea.Width / scaling * 0.7;
        double maxH = workingArea.Height / scaling * 0.7;

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
        ViewModel?.Shutdown();
    }
}
