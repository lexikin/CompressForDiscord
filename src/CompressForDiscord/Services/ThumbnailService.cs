using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CompressForDiscord.Services;

internal interface IThumbnailService
{
    /// <summary>Extracts a preview frame (~25 % in); null on failure — callers must cope.</summary>
    Task<string?> TryCreateThumbnailAsync(string videoPath, double? durationSeconds, CancellationToken ct);
}

internal sealed class ThumbnailService(IFfmpegRunner runner) : IThumbnailService
{
    public async Task<string?> TryCreateThumbnailAsync(
        string videoPath, double? durationSeconds, CancellationToken ct)
    {
        try
        {
            // Own dir under the job-temp root: outlives the compression job (the preview window
            // needs it), reaped by the 7-day startup sweep.
            string dir = Path.Combine(
                CompressionOrchestrator.JobTempRoot, "thumb-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string output = Path.Combine(dir, "thumb.png");

            double seek = Math.Max(0, (durationSeconds ?? 0) * 0.25);
            var result = await runner.RunFfmpegAsync(
                FfmpegArgsBuilder.BuildThumbnailArgs(videoPath, seek, output),
                durationSeconds: null, progress: null, ct);

            return result.Success && File.Exists(output) ? output : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
