using CompressForDiscord.Models;
using CompressForDiscord.Services;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class FfmpegArgsBuilderTests
{
    private static readonly VideoPlan Plan1080 = new(
        VideoKbps: 1253, AudioKbps: 96, AudioChannels: 2, Width: 1920, Height: 1080, Fps: null);

    private static readonly VideoPlan Plan360NoAudio = new(
        VideoKbps: 85, AudioKbps: null, AudioChannels: 0, Width: 640, Height: 360, Fps: 24);

    [Fact]
    public void Pass1_GoldenArguments()
    {
        var args = FfmpegArgsBuilder.BuildVideoPass1Args(
            @"C:\in\Ünïcode video.mp4", Plan1080, @"C:\tmp\x264stats", "NUL");

        Assert.Equal(
        [
            "-y", "-loglevel", "error", "-progress", "pipe:1",
            "-i", @"C:\in\Ünïcode video.mp4",
            "-map", "0:v:0", "-map_metadata", "-1", "-sn", "-dn",
            "-vf", "scale=1920:1080:flags=lanczos,format=yuv420p",
            "-c:v", "libx264", "-b:v", "1253k", "-maxrate", "1879k", "-bufsize", "2506k",
            "-preset", "veryfast",
            "-pass", "1", "-passlogfile", @"C:\tmp\x264stats",
            "-an", "-f", "null", "NUL",
        ], args);
    }

    [Fact]
    public void Pass2_WithAudio_GoldenArguments()
    {
        var args = FfmpegArgsBuilder.BuildVideoPass2Args(
            "in.mp4", Plan1080, "log", "out.mp4");

        Assert.Equal(
        [
            "-y", "-loglevel", "error", "-progress", "pipe:1",
            "-i", "in.mp4",
            "-map", "0:v:0", "-map", "0:a:0", "-map_metadata", "-1", "-sn", "-dn",
            "-vf", "scale=1920:1080:flags=lanczos,format=yuv420p",
            "-c:v", "libx264", "-b:v", "1253k", "-maxrate", "1879k", "-bufsize", "2506k",
            "-preset", "veryfast",
            "-pass", "2", "-passlogfile", "log",
            "-c:a", "aac", "-b:a", "96k", "-ac", "2",
            "-movflags", "+faststart", "-f", "mp4", "out.mp4",
        ], args);
    }

    [Fact]
    public void Pass2_WithoutAudio_MapsNoAudioStream()
    {
        var args = FfmpegArgsBuilder.BuildVideoPass2Args("in.gif", Plan360NoAudio, "log", "out.mp4");

        Assert.Contains("-an", args);
        Assert.DoesNotContain("0:a:0", args);
        Assert.DoesNotContain("aac", args);
        Assert.Contains("fps=24,scale=640:360:flags=lanczos,format=yuv420p", args);
    }

    [Fact]
    public void FilterChain_IsByteIdenticalAcrossPasses()
    {
        var pass1 = FfmpegArgsBuilder.BuildVideoPass1Args("in.mp4", Plan360NoAudio, "log", "NUL");
        var pass2 = FfmpegArgsBuilder.BuildVideoPass2Args("in.mp4", Plan360NoAudio, "log", "out.mp4");

        string Vf(string[] args) => args[System.Array.IndexOf(args, "-vf") + 1];
        Assert.Equal(Vf(pass1), Vf(pass2));
    }

    [Fact]
    public void FractionalFps_FormatsInvariantWithThreeDecimals()
    {
        var plan = Plan360NoAudio with { Fps = 23.976 };

        Assert.Equal("fps=23.976,scale=640:360:flags=lanczos,format=yuv420p",
            FfmpegArgsBuilder.BuildVideoFilterChain(plan));
    }

    [Fact]
    public void Png_WithScaleAndAlpha_GoldenArguments()
    {
        var args = FfmpegArgsBuilder.BuildPngArgs("in.png", 800, 600, hasAlpha: true, "out.png");

        Assert.Equal(
        [
            "-y", "-loglevel", "error", "-progress", "pipe:1",
            "-i", "in.png",
            "-map", "0:v:0", "-frames:v", "1",
            "-vf", "scale=800:600:flags=lanczos,format=rgba",
            "-c:v", "png", "-compression_level", "9", "-pred", "mixed",
            "out.png",
        ], args);
    }

    [Fact]
    public void Png_NativeResolutionOpaque_SkipsScaleUsesRgb24()
    {
        var args = FfmpegArgsBuilder.BuildPngArgs("in.jpg", null, null, hasAlpha: false, "out.png");

        Assert.Contains("format=rgb24", args);
        Assert.DoesNotContain(args, a => a.Contains("scale="));
    }

    [Fact]
    public void Thumbnail_SeeksBeforeInput()
    {
        var args = FfmpegArgsBuilder.BuildThumbnailArgs("in.webm", 2.5, "thumb.png");

        int ss = System.Array.IndexOf(args, "-ss");
        int i = System.Array.IndexOf(args, "-i");
        Assert.True(ss >= 0 && ss < i, "-ss must precede -i for fast seek");
        Assert.Equal("2.5", args[ss + 1]);
    }
}
