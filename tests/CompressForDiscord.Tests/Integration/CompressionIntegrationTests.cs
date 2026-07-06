using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Models;
using CompressForDiscord.Services;
using Xunit;

namespace CompressForDiscord.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class CompressionIntegrationTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cfd-it-").FullName;

    private static CompressionOrchestrator NewOrchestrator()
    {
        var runner = new FfmpegRunner(new FfmpegLocator());
        return new CompressionOrchestrator(
            new MediaProber(runner),
            new VideoCompressor(runner),
            new ImageCompressor(runner));
    }

    private static (IProgress<CompressionProgress> Progress, Func<double> MaxPercent) TrackingProgress()
    {
        double max = 0;
        // Direct (non-marshaling) progress sink — no SynchronizationContext in tests.
        var progress = new DirectProgress(p => max = Math.Max(max, p.Percent));
        return (progress, () => max);
    }

    private sealed class DirectProgress(Action<CompressionProgress> handler) : IProgress<CompressionProgress>
    {
        public void Report(CompressionProgress value) => handler(value);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [IntegrationFact]
    public async Task OversizedMp4WithAudio_BecomesMp4UnderTheLimit()
    {
        string input = FixtureFactory.CreateMp4(_dir, seconds: 8); // 2000 kbps ≈ 2 MB
        const long limit = 1024 * 1024; // 1 MiB forces real bitrate math on an 8 s clip
        var (progress, maxPercent) = TrackingProgress();

        var result = await NewOrchestrator().RunAsync(input, limit, progress, CancellationToken.None);

        Assert.False(result.WasSkipped);
        Assert.EndsWith(".discord.mp4", result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        Assert.InRange(new FileInfo(result.OutputPath).Length, 1, limit);
        Assert.Equal(100, maxPercent());

        // Re-probe: really mp4/h264 with aac audio.
        var probe = await new MediaProber(new FfmpegRunner(new FfmpegLocator()))
            .ProbeAsync(result.OutputPath, CancellationToken.None);
        Assert.Contains("mp4", probe.ContainerFormat);
        Assert.Equal("h264", probe.Video!.CodecName);
        Assert.Equal("aac", probe.Audio!.CodecName);
    }

    [IntegrationFact]
    public async Task AnimatedGif_BecomesMp4()
    {
        string input = FixtureFactory.CreateAnimatedGif(_dir);
        var (progress, _) = TrackingProgress();

        var result = await NewOrchestrator().RunAsync(
            input, 10 * 1024 * 1024, progress, CancellationToken.None);

        Assert.Equal(MediaKind.AnimatedImage, result.Kind);
        Assert.EndsWith(".discord.mp4", result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
    }

    [IntegrationFact]
    public async Task NoisePng_IsDownscaledUnderTheLimit()
    {
        string input = FixtureFactory.CreateNoisePng(_dir);
        long inputSize = new FileInfo(input).Length;
        long limit = Math.Min(512 * 1024, inputSize - 1); // always force at least one downscale
        var (progress, _) = TrackingProgress();

        var result = await NewOrchestrator().RunAsync(input, limit, progress, CancellationToken.None);

        Assert.EndsWith(".discord.png", result.OutputPath);
        Assert.InRange(new FileInfo(result.OutputPath).Length, 1, limit);
    }

    [IntegrationFact]
    public async Task TinyWebm_IsSkippedUntouched()
    {
        string input = FixtureFactory.CreateTinyWebm(_dir);
        var (progress, _) = TrackingProgress();

        var result = await NewOrchestrator().RunAsync(
            input, 10 * 1024 * 1024, progress, CancellationToken.None);

        Assert.True(result.WasSkipped);
        Assert.Equal(input, result.OutputPath);
    }

    [IntegrationFact]
    public async Task TinyH264Mp4_IsSkippedUntouched()
    {
        string input = FixtureFactory.CreateTinyMp4(_dir);
        var (progress, _) = TrackingProgress();

        var result = await NewOrchestrator().RunAsync(
            input, 10 * 1024 * 1024, progress, CancellationToken.None);

        Assert.True(result.WasSkipped);
        Assert.Equal(input, result.OutputPath);
    }

    [IntegrationFact]
    public async Task Cancellation_KillsFfmpegQuickly_AndCleansTemp()
    {
        // Big enough that pass 1 takes a while on any machine.
        string input = FixtureFactory.CreateMp4(_dir, seconds: 20, size: "1920x1080", audio: false);
        using var cts = new CancellationTokenSource();

        int reports = 0;
        var progress = new DirectProgress(_ =>
        {
            // Cancel once the encoder is demonstrably running.
            if (Interlocked.Increment(ref reports) == 3)
            {
                cts.Cancel();
            }
        });

        string tempRoot = CompressionOrchestrator.JobTempRoot;
        int dirsBefore = Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot).Length : 0;

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            NewOrchestrator().RunAsync(input, 1024 * 1024, progress, cts.Token));
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"cancel took {stopwatch.Elapsed} — process tree kill is too slow");

        int dirsAfter = Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot).Length : 0;
        Assert.True(dirsAfter <= dirsBefore, "job temp directory leaked after cancel");

        // No stray output next to the input either.
        Assert.Empty(Directory.GetFiles(_dir, "*.discord.*"));
    }
}
