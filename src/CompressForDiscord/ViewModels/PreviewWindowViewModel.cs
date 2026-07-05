using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompressForDiscord.Models;
using CompressForDiscord.Services;
using CompressForDiscord.Services.Clipboard;
using LibVLCSharp.Shared;

namespace CompressForDiscord.ViewModels;

internal sealed partial class PreviewWindowViewModel : ObservableObject
{
    private static readonly TimeSpan BannerDuration = TimeSpan.FromSeconds(5);
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#2E7D46"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#9A6A00"));
    private static readonly IBrush FailureBrush = new SolidColorBrush(Color.Parse("#A33B3B"));

    private readonly CompressionResult _result;
    private readonly IVlcService _vlc;
    private readonly IThumbnailService _thumbnails;
    private readonly Func<Task<ClipboardOutcome>> _recopy;
    private readonly DispatcherTimer _bannerTimer;

    private Media? _media;
    private DateTime _bannerStartedUtc;

    // ---- banner ----
    [ObservableProperty]
    private bool _bannerVisible;

    [ObservableProperty]
    private string _bannerText = "";

    [ObservableProperty]
    private double _bannerRemaining = 1.0;

    [ObservableProperty]
    private IBrush _bannerBackground = new SolidColorBrush(Color.Parse("#2E7D46"));

    // ---- media area ----
    [ObservableProperty]
    private bool _showImage;

    [ObservableProperty]
    private bool _showVideo;

    [ObservableProperty]
    private bool _showThumbnail;

    [ObservableProperty]
    private Bitmap? _imageSource;

    [ObservableProperty]
    private Bitmap? _thumbnailSource;

    [ObservableProperty]
    private MediaPlayer? _mediaPlayer;

    public PreviewWindowViewModel(
        CompressionResult result,
        long limitBytes,
        IVlcService vlc,
        IThumbnailService thumbnails,
        Func<Task<ClipboardOutcome>> recopy)
    {
        _result = result;
        _vlc = vlc;
        _thumbnails = thumbnails;
        _recopy = recopy;

        OutputFileName = Path.GetFileName(result.OutputPath);
        SizeText = string.Create(CultureInfo.InvariantCulture,
            $"{result.OutputBytes / (double)AppSettings.BytesPerUnit:0.0} MB of " +
            $"{limitBytes / (double)AppSettings.BytesPerUnit:0.#} MB limit" +
            $"{(result.WasSkipped ? " — already small enough, nothing re-encoded" : "")}");
        FallbackNote = result.UsedFallbackDirectory
            ? $"Saved to {Path.GetDirectoryName(result.OutputPath)} (original folder wasn't writable)"
            : null;

        _bannerTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(50), DispatcherPriority.Render, OnBannerTick);
    }

    public string OutputFileName { get; }
    public string SizeText { get; }
    public string? FallbackNote { get; }
    public bool HasFallbackNote => FallbackNote is not null;
    public string OutputPath => _result.OutputPath;
    public int? MediaWidth => _result.Width;
    public int? MediaHeight => _result.Height;

    /// <summary>Picks the media mode and loads what it needs. Call before showing the window.</summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_result.Kind == MediaKind.StaticImage)
        {
            ImageSource = await Task.Run(() =>
            {
                using var stream = File.OpenRead(_result.OutputPath);
                return Bitmap.DecodeToWidth(stream, 1600); // bounds memory on huge PNGs
            }, ct);
            ShowImage = true;
            return;
        }

        if (_vlc.TryInitialize())
        {
            // Loop via input-repeat instead of EndReached-replay (threading trap).
            _media = new Media(_vlc.LibVlc!, _result.OutputPath, FromType.FromPath, ":input-repeat=65535");
            MediaPlayer = new MediaPlayer(_vlc.LibVlc!) { Mute = true };
            ShowVideo = true;
            return;
        }

        if (await _thumbnails.TryCreateThumbnailAsync(_result.OutputPath, null, ct) is { } thumbPath)
        {
            ThumbnailSource = await Task.Run(() =>
            {
                using var stream = File.OpenRead(thumbPath);
                return Bitmap.DecodeToWidth(stream, 1280);
            }, ct);
        }

        ShowThumbnail = true;
    }

    /// <summary>Called by the view once the native VideoView handle exists (Window.Opened).</summary>
    public void StartVideoPlayback()
    {
        if (MediaPlayer is { } player && _media is { } media)
        {
            player.Play(media);
        }
    }

    public void ShowBannerFor(ClipboardOutcome outcome)
    {
        (BannerText, BannerBackground) = outcome switch
        {
            ClipboardOutcome.FileCopied => (
                "Copied to clipboard — press Ctrl+V in Discord to send it", SuccessBrush),
            ClipboardOutcome.TextFallback => (
                OperatingSystem.IsLinux()
                    ? "Saved — link copied as text. Install wl-clipboard or xclip for real file copy."
                    : "Saved — link copied as text (clipboard was busy).", WarningBrush),
            _ => ($"Couldn't copy — file saved as {OutputFileName}", FailureBrush),
        };

        BannerRemaining = 1.0;
        BannerVisible = true;
        _bannerStartedUtc = DateTime.UtcNow;
        _bannerTimer.Start();
    }

    private void OnBannerTick(object? sender, EventArgs e)
    {
        double elapsed = (DateTime.UtcNow - _bannerStartedUtc).TotalSeconds;
        BannerRemaining = Math.Max(0, 1.0 - elapsed / BannerDuration.TotalSeconds);
        if (BannerRemaining <= 0)
        {
            DismissBanner();
        }
    }

    [RelayCommand]
    private void DismissBanner()
    {
        _bannerTimer.Stop();
        BannerVisible = false;
    }

    [RelayCommand]
    private async Task CopyAgainAsync()
    {
        ShowBannerFor(await _recopy());
    }

    [RelayCommand]
    private void ShowInFolder() => ShellOpener.ShowInFolder(_result.OutputPath);

    [RelayCommand]
    private void OpenInPlayer() => ShellOpener.OpenInDefaultApp(_result.OutputPath);

    /// <summary>
    /// Call on window close. The view must detach VideoView.MediaPlayer first; stopping and
    /// disposing happen off the UI thread (stopping libvlc from the UI thread is a known
    /// LibVLCSharp deadlock trap).
    /// </summary>
    public void Shutdown()
    {
        _bannerTimer.Stop();

        var player = MediaPlayer;
        var media = _media;
        MediaPlayer = null;
        _media = null;
        if (player is not null || media is not null)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    player?.Stop();
                    player?.Dispose();
                    media?.Dispose();
                }
                catch (Exception e)
                {
                    Infrastructure.AppLog.Write("vlc dispose", e);
                }
            });
        }

        ImageSource?.Dispose();
        ThumbnailSource?.Dispose();
    }
}
