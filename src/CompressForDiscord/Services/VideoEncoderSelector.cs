using System;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Infrastructure;
using CompressForDiscord.Models;

namespace CompressForDiscord.Services;

internal interface IVideoEncoderSelector
{
    /// <summary>
    /// The best working H.264 encoder on this machine — a GPU encoder if one is functional,
    /// otherwise x264. Probed once and cached for the process lifetime.
    /// </summary>
    Task<VideoEncoder> SelectAsync(CancellationToken ct);
}

/// <summary>
/// Picks a hardware encoder by <em>functionally probing</em> it, not by trusting ffmpeg's
/// encoder list: h264_nvenc / h264_qsv / h264_amf are always listed regardless of the hardware
/// present, so the only reliable test is a tiny real encode. Whatever fails to initialise
/// (no GPU, no driver, no free encode session, remote session, …) is skipped, and we fall back
/// to x264. This makes hardware acceleration a safe, invisible speed-up with a guaranteed floor.
/// </summary>
internal sealed class VideoEncoderSelector(IFfmpegRunner runner) : IVideoEncoderSelector
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private VideoEncoder? _cached;

    public async Task<VideoEncoder> SelectAsync(CancellationToken ct)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cached is { } already)
            {
                return already;
            }

            foreach (var candidate in VideoEncoder.HardwareCandidates)
            {
                if (await ProbeAsync(candidate, ct).ConfigureAwait(false))
                {
                    AppLog.Write($"video encoder: {candidate.DisplayName}");
                    return _cached = candidate;
                }
            }

            AppLog.Write("video encoder: x264 (no working hardware encoder found)");
            return _cached = VideoEncoder.Software;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> ProbeAsync(VideoEncoder encoder, CancellationToken ct)
    {
        // Encode a fraction of a second of black through the encoder; exit 0 means it initialised.
        string[] args =
        [
            "-f", "lavfi", "-i", "color=c=black:s=64x64:r=5:d=0.2",
            "-vf", "format=yuv420p",
            "-c:v", encoder.CodecArg,
            "-f", "null", "-",
        ];

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            var result = await runner.RunFfmpegAsync(args, null, null, timeout.Token).ConfigureAwait(false);
            return result.Success;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false; // probe timed out — treat as unavailable
        }
    }
}
