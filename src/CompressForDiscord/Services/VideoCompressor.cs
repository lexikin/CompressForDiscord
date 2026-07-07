using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Infrastructure;
using CompressForDiscord.Models;
using CompressForDiscord.Services.Planning;

namespace CompressForDiscord.Services;

/// <summary>
/// Single-pass H.264/AAC encoder with a verify-and-retry loop. Uses the fastest working encoder
/// on the machine (GPU if available, else x264 — see <see cref="IVideoEncoderSelector"/>). Single
/// pass keeps it fast; the retry loop tightens the bitrate margin whenever VBR overshoots the limit.
/// </summary>
internal interface IVideoCompressor : IMediaCompressor;

internal sealed class VideoCompressor(IFfmpegRunner runner, IVideoEncoderSelector encoderSelector)
    : IVideoCompressor
{
    public async Task<CompressorOutput> CompressAsync(
        MediaInfo media, long limitBytes, string jobTempDir,
        IProgress<CompressionProgress> progress, CancellationToken ct)
    {
        var encoder = await encoderSelector.SelectAsync(ct);

        for (int attempt = 0; attempt < VideoPlanner.MaxAttempts; attempt++)
        {
            var planResult = VideoPlanner.Plan(media, limitBytes, attempt);
            if (planResult.Plan is not { } plan)
            {
                throw new CannotFitException(planResult.CannotFitReason!);
            }

            string attemptLabel = attempt == 0 ? "" : $"Attempt {attempt + 1} — ";

            // Keep the bar animated from the instant the encoder is spawned — the first real
            // -progress packet can be a second or two away while the encoder/GPU spins up.
            progress.Report(new CompressionProgress(-1, $"{attemptLabel}Preparing…"));

            string output = Path.Combine(jobTempDir, $"out-a{attempt}.mp4");
            var args = FfmpegArgsBuilder.BuildVideoArgs(media.FilePath, plan, encoder, output);
            var result = await runner.RunFfmpegAsync(
                args, media.DurationSeconds,
                MapProgress(progress, media, $"{attemptLabel}Compressing"),
                ct);
            ThrowIfFailed(result, "encoding");

            progress.Report(new CompressionProgress(99, "Verifying size…"));
            long actualBytes = new FileInfo(output).Length;
            if (actualBytes <= limitBytes)
            {
                return new CompressorOutput(output, attempt + 1, plan.Width, plan.Height);
            }

            File.Delete(output); // overshoot — tighten the margin and go again
        }

        throw new CannotFitException(
            $"The encoder kept overshooting the limit after {VideoPlanner.MaxAttempts} attempts. " +
            "Try a slightly larger size preset.");
    }

    private static IProgress<ProgressUpdate> MapProgress(
        IProgress<CompressionProgress> progress, MediaInfo media, string phase)
    {
        // Constructed on the caller's context; Progress<T> marshals to it automatically.
        return new Progress<ProgressUpdate>(update =>
        {
            // Until there's measurable output, keep the bar marquee-animated rather than
            // freezing it at a determinate 0% while the encoder warms up.
            if (update.Fraction <= 0 && !update.Ended)
            {
                progress.Report(new CompressionProgress(-1, phase + "…"));
                return;
            }

            double percent = Math.Min(99, update.Fraction * 100);
            string text = phase;
            if (update.Speed is { } speed && media.DurationSeconds is { } duration && !update.Ended)
            {
                double wallSecondsLeft = duration * (1 - update.Fraction) / speed;
                text = $"{phase} ({FormatEta(wallSecondsLeft)} left)";
            }

            progress.Report(new CompressionProgress(percent, text + "…"));
        });
    }

    private static string FormatEta(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{ts.Minutes}:{ts.Seconds:00}");
    }

    private static void ThrowIfFailed(FfmpegResult result, string stage)
    {
        if (!result.Success)
        {
            throw new CompressionFailedException(
                $"ffmpeg failed during {stage} (exit code {result.ExitCode}).",
                result.StderrTail);
        }
    }
}
