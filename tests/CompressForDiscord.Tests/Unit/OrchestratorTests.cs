using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Infrastructure;
using CompressForDiscord.Models;
using CompressForDiscord.Services;
using NSubstitute;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class OrchestratorTests : IDisposable
{
    private const long TenMiB = 10 * 1024 * 1024;

    private readonly string _dir = Directory.CreateTempSubdirectory("cfd-test-").FullName;
    private readonly IMediaProber _prober = Substitute.For<IMediaProber>();
    private readonly IVideoCompressor _video = Substitute.For<IVideoCompressor>();
    private readonly IImageCompressor _image = Substitute.For<IImageCompressor>();

    private CompressionOrchestrator Orchestrator => new(_prober, _video, _image);

    private static IProgress<CompressionProgress> NullProgress { get; } =
        new Progress<CompressionProgress>();

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

    private string CreateInput(string name, int bytes = 100)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private void SetupProbe(string path, MediaKind kind, long size, string codec = "vp9")
    {
        _prober.ProbeAsync(path, Arg.Any<CancellationToken>()).Returns(new MediaInfo(
            path, size, kind, "x", 10,
            new VideoStreamInfo(codec, 640, 360, "yuv420p", 30, false),
            null));
    }

    [Fact]
    public async Task SmallPng_IsSkippedWithoutCompression()
    {
        string input = CreateInput("photo.png");
        SetupProbe(input, MediaKind.StaticImage, 100);

        var result = await Orchestrator.RunAsync(input, TenMiB, NullProgress, CancellationToken.None);

        Assert.True(result.WasSkipped);
        Assert.Equal(input, result.OutputPath);
        await _image.DidNotReceiveWithAnyArgs().CompressAsync(default!, default, default!, default!, default);
    }

    [Fact]
    public async Task SmallVp9Webm_IsSkipped_ButSmallMp4IsNot()
    {
        string webm = CreateInput("clip.webm");
        SetupProbe(webm, MediaKind.Video, 100, codec: "vp9");
        var webmResult = await Orchestrator.RunAsync(webm, TenMiB, NullProgress, CancellationToken.None);
        Assert.True(webmResult.WasSkipped);

        string mp4 = CreateInput("clip.mp4");
        SetupProbe(mp4, MediaKind.Video, 100, codec: "h264");
        _video.CompressAsync(Arg.Any<MediaInfo>(), TenMiB, Arg.Any<string>(),
                Arg.Any<IProgress<CompressionProgress>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string produced = Path.Combine(call.ArgAt<string>(2), "out.webm");
                File.WriteAllBytes(produced, new byte[50]);
                return new CompressorOutput(produced, 1, 640, 360);
            });

        var mp4Result = await Orchestrator.RunAsync(mp4, TenMiB, NullProgress, CancellationToken.None);

        Assert.False(mp4Result.WasSkipped); // wrong container: converts even when small
        Assert.Equal(Path.Combine(_dir, "clip.discord.webm"), mp4Result.OutputPath);
        Assert.True(File.Exists(mp4Result.OutputPath));
    }

    [Fact]
    public async Task UnsupportedInput_Throws()
    {
        string input = CreateInput("song.mp3");
        _prober.ProbeAsync(input, Arg.Any<CancellationToken>()).Returns(
            new MediaInfo(input, 100, MediaKind.Unsupported, "mp3", 10, null, null));

        await Assert.ThrowsAsync<UnsupportedInputException>(() =>
            Orchestrator.RunAsync(input, TenMiB, NullProgress, CancellationToken.None));
    }

    [Fact]
    public async Task NameCollision_GetsNumberedSuffix()
    {
        string input = CreateInput("video.mp4");
        SetupProbe(input, MediaKind.Video, 999_999_999);
        File.WriteAllText(Path.Combine(_dir, "video.discord.webm"), "occupied");

        _video.CompressAsync(Arg.Any<MediaInfo>(), Arg.Any<long>(), Arg.Any<string>(),
                Arg.Any<IProgress<CompressionProgress>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string produced = Path.Combine(call.ArgAt<string>(2), "out.webm");
                File.WriteAllBytes(produced, new byte[50]);
                return new CompressorOutput(produced, 2, 640, 360);
            });

        var result = await Orchestrator.RunAsync(input, TenMiB, NullProgress, CancellationToken.None);

        Assert.Equal(Path.Combine(_dir, "video.discord (2).webm"), result.OutputPath);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task JobTempDirectory_IsCleanedUp_OnSuccessAndOnFailure()
    {
        string input = CreateInput("video.mp4");
        SetupProbe(input, MediaKind.Video, 999_999_999);

        string? observedTempDir = null;
        _video.CompressAsync(Arg.Any<MediaInfo>(), Arg.Any<long>(), Arg.Any<string>(),
                Arg.Any<IProgress<CompressionProgress>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                observedTempDir = call.ArgAt<string>(2);
                string produced = Path.Combine(observedTempDir, "out.webm");
                File.WriteAllBytes(produced, new byte[50]);
                return new CompressorOutput(produced, 1, 640, 360);
            });

        await Orchestrator.RunAsync(input, TenMiB, NullProgress, CancellationToken.None);
        Assert.False(Directory.Exists(observedTempDir!));

        // Failure path cleans up too.
        _video.CompressAsync(Arg.Any<MediaInfo>(), Arg.Any<long>(), Arg.Any<string>(),
                Arg.Any<IProgress<CompressionProgress>>(), Arg.Any<CancellationToken>())
            .Returns<CompressorOutput>(call =>
            {
                observedTempDir = call.ArgAt<string>(2);
                throw new CannotFitException("nope");
            });

        await Assert.ThrowsAsync<CannotFitException>(() =>
            Orchestrator.RunAsync(input, TenMiB, NullProgress, CancellationToken.None));
        Assert.False(Directory.Exists(observedTempDir!));
    }
}
