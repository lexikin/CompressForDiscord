using CompressForDiscord.Models;
using CompressForDiscord.Services.Planning;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class VideoPlannerTests
{
    private const long TenMiB = 10 * 1024 * 1024;

    private static MediaInfo Media(
        int width = 1920, int height = 1080, double? fps = 30, double? duration = 60,
        bool audio = true) =>
        new(
            FilePath: "in.mp4",
            FileSizeBytes: 100_000_000,
            Kind: MediaKind.Video,
            ContainerFormat: "mov,mp4,m4a,3gp,3g2,mj2",
            DurationSeconds: duration,
            Video: new VideoStreamInfo("h264", width, height, "yuv420p", fps, HasAlpha: false),
            Audio: audio ? new AudioStreamInfo("aac", 2) : null);

    [Fact]
    public void SixtySecond1080p_At10MiB_KeepsNativeResolutionAndStereoAudio()
    {
        var result = VideoPlanner.Plan(Media(), TenMiB, attempt: 0);

        Assert.NotNull(result.Plan);
        var plan = result.Plan!;
        // 10 MiB * 8 * 0.97 / 60 s = 1356.16 kbps total; 96k audio; ~6.78k mux overhead.
        Assert.Equal(1253, plan.VideoKbps);
        Assert.Equal(96, plan.AudioKbps);
        Assert.Equal(2, plan.AudioChannels);
        Assert.Equal(1920, plan.Width);
        Assert.Equal(1080, plan.Height);
        Assert.Null(plan.Fps); // source 30 fps is under the cap — no fps filter
    }

    [Fact]
    public void NoAudioSource_GivesWholeBudgetToVideo()
    {
        var result = VideoPlanner.Plan(Media(audio: false), TenMiB, attempt: 0);

        Assert.Equal(1349, result.Plan!.VideoKbps);
        Assert.Null(result.Plan.AudioKbps);
    }

    [Fact]
    public void TighterMarginOnRetry_LowersBitrate()
    {
        var attempt0 = VideoPlanner.Plan(Media(), TenMiB, attempt: 0).Plan!;
        var attempt1 = VideoPlanner.Plan(Media(), TenMiB, attempt: 1).Plan!;
        var attempt2 = VideoPlanner.Plan(Media(), TenMiB, attempt: 2).Plan!;

        Assert.True(attempt1.VideoKbps < attempt0.VideoKbps);
        Assert.True(attempt2.VideoKbps < attempt1.VideoKbps);
        // Same rung across margins here — pass-1 stats stay reusable.
        Assert.True(attempt1.SameVideoChainAs(attempt0));
    }

    [Fact]
    public void TenMinute4K60_At10MiB_DropsTo360p24AndMonoAudio()
    {
        var media = Media(width: 3840, height: 2160, fps: 59.94, duration: 600);

        var plan = VideoPlanner.Plan(media, TenMiB, attempt: 0).Plan!;

        Assert.Equal(48, plan.AudioKbps);
        Assert.Equal(1, plan.AudioChannels);
        Assert.Equal(640, plan.Width);
        Assert.Equal(360, plan.Height);
        Assert.Equal(24, plan.Fps);
    }

    [Fact]
    public void PortraitVideo_LaddersOnShortSide()
    {
        // 5 MiB / 60 s leaves ~578 kbps video: too thin for 1080x1920, fine for 720x1280.
        var media = Media(width: 1080, height: 1920, fps: 30, duration: 60);

        var plan = VideoPlanner.Plan(media, 5 * 1024 * 1024, attempt: 0).Plan!;

        Assert.Equal(720, plan.Width);
        Assert.Equal(1280, plan.Height);
    }

    [Fact]
    public void TwoHourVideo_At10MiB_CannotFit_WithActionableMessage()
    {
        var result = VideoPlanner.Plan(Media(duration: 7200), TenMiB, attempt: 0);

        Assert.Null(result.Plan);
        Assert.Contains("minutes", result.CannotFitReason);
        Assert.Contains("preset", result.CannotFitReason);
    }

    [Fact]
    public void VeryTightBudget_StripsAudioBeforeGivingUp()
    {
        // ~60 kbps total: with audio the video floor (40 kbps) is unreachable; without it, it isn't.
        var result = VideoPlanner.Plan(Media(duration: 1356), TenMiB, attempt: 0);

        var plan = result.Plan!;
        Assert.Null(plan.AudioKbps);
        Assert.True(plan.VideoKbps >= VideoPlanner.MinVideoKbps);
    }

    [Fact]
    public void SmallSource_IsNeverUpscaled()
    {
        var media = Media(width: 320, height: 240, fps: 30, duration: 10);

        var plan = VideoPlanner.Plan(media, TenMiB, attempt: 0).Plan!;

        Assert.Equal(320, plan.Width);
        Assert.Equal(240, plan.Height);
    }

    [Fact]
    public void SourceSmallerThanLadder_UsesSourceDimensions()
    {
        var media = Media(width: 100, height: 80, fps: 30, duration: 10);

        var plan = VideoPlanner.Plan(media, TenMiB, attempt: 0).Plan!;

        Assert.Equal(100, plan.Width);
        Assert.Equal(80, plan.Height);
        Assert.Equal(24, plan.Fps); // sub-360p rung caps fps at 24
    }

    [Fact]
    public void OddSourceDimensions_AreFlooredToEven()
    {
        var media = Media(width: 1281, height: 721, fps: 30, duration: 3600);

        var plan = VideoPlanner.Plan(media, 500L * 1024 * 1024, attempt: 0).Plan!;

        Assert.Equal(0, plan.Width % 2);
        Assert.Equal(0, plan.Height % 2);
    }

    [Fact]
    public void UnknownDuration_CannotFit()
    {
        var result = VideoPlanner.Plan(Media(duration: null), TenMiB, attempt: 0);

        Assert.Null(result.Plan);
        Assert.Contains("duration", result.CannotFitReason);
    }

    [Fact]
    public void UnknownSourceFps_GetsExplicitFpsInPlan()
    {
        var media = Media(fps: null);

        var plan = VideoPlanner.Plan(media, TenMiB, attempt: 0).Plan!;

        Assert.NotNull(plan.Fps); // pass 1/2 need a deterministic frame count
    }

    [Fact]
    public void HighFpsSource_IsCappedAt30()
    {
        var media = Media(fps: 120, duration: 30);

        var plan = VideoPlanner.Plan(media, TenMiB, attempt: 0).Plan!;

        Assert.Equal(30, plan.Fps);
    }
}
