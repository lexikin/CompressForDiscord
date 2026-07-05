using System;
using LibVLCSharp.Shared;

namespace CompressForDiscord.Services;

internal interface IVlcService : IDisposable
{
    /// <summary>Lazily initializes libvlc once; false means "use the thumbnail fallback".</summary>
    bool TryInitialize();

    LibVLC? LibVlc { get; }
}

/// <summary>
/// On Windows the native payload ships via VideoLAN.LibVLC.Windows; on Linux libvlc comes from
/// the distro (AppImage) or the flatpak build. Missing/broken libvlc must never crash the app —
/// the preview degrades to a thumbnail instead.
/// </summary>
internal sealed class VlcService : IVlcService
{
    private bool? _available;

    public LibVLC? LibVlc { get; private set; }

    public bool TryInitialize()
    {
        if (_available.HasValue)
        {
            return _available.Value;
        }

        try
        {
            Core.Initialize();
            LibVlc = new LibVLC("--no-video-title-show");
            _available = true;
        }
        catch (Exception e) when (
            e is DllNotFoundException or VLCException or TypeInitializationException or BadImageFormatException)
        {
            Infrastructure.AppLog.Write($"libvlc unavailable, preview degrades to thumbnail: {e.Message}");
            _available = false;
        }

        return _available.Value;
    }

    public void Dispose()
    {
        LibVlc?.Dispose();
        LibVlc = null;
    }
}
