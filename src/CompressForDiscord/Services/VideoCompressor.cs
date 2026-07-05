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
/// Two-pass VP9/Opus encoder with a verify-and-retry loop. Pass 1 maps to 0–50 % progress,
/// pass 2 to 50–100 %. When a retry keeps the same filter chain, the libvpx pass-1 stats are
/// reused and only pass 2 reruns (stats are complexity data, independent of target bitrate).
/// </summary>
internal interface IVideoCompressor : IMediaCompressor;

internal sealed class VideoCompressor(IFfmpegRunner runner) : IVideoCompressor
{
    public async Task<CompressorOutput> CompressAsync(
        MediaInfo media, long limitBytes, string jobTempDir,
        IProgress<CompressionProgress> progress, CancellationToken ct)
    {
        string passLogPrefix = Path.Combine(jobTempDir, "vp9stats");
        string nullSink = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

        VideoPlan? statsPlan = null; // plan the current pass-1 stats were generated with

        for (int attempt = 0; attempt < VideoPlanner.MaxAttempts; attempt++)
        {
            var planResult = VideoPlanner.Plan(media, limitBytes, attempt);
            if (planResult.Plan is not { } plan)
            {
                throw new CannotFitException(planResult.CannotFitReason!);
            }

            string attemptLabel = attempt == 0 ? "" : $"Attempt {attempt + 1} — ";
            bool reuseStats = statsPlan is not null && plan.SameVideoChainAs(statsPlan);

            if (!reuseStats)
            {
                var pass1Args = FfmpegArgsBuilder.BuildVideoPass1Args(
                    media.FilePath, plan, passLogPrefix, nullSink);
                var pass1Result = await runner.RunFfmpegAsync(
                    pass1Args, media.DurationSeconds,
                    MapPass(progress, media, basePercent: 0, $"{attemptLabel}Pass 1 of 2"),
                    ct);
                ThrowIfFailed(pass1Result, "pass 1");
                statsPlan = plan;
            }

            string output = Path.Combine(jobTempDir, $"out-a{attempt}.webm");
            var pass2Args = FfmpegArgsBuilder.BuildVideoPass2Args(
                media.FilePath, plan, passLogPrefix, output);
            var pass2Result = await runner.RunFfmpegAsync(
                pass2Args, media.DurationSeconds,
                MapPass(progress, media, basePercent: 50, $"{attemptLabel}Pass 2 of 2"),
                ct);
            ThrowIfFailed(pass2Result, "pass 2");

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

    private static IProgress<ProgressUpdate> MapPass(
        IProgress<CompressionProgress> progress, MediaInfo media, int basePercent, string phase)
    {
        // Constructed on the caller's context; Progress<T> marshals to it automatically.
        return new Progress<ProgressUpdate>(update =>
        {
            double percent = basePercent + update.Fraction * 50;
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
