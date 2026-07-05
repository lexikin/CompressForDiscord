using CompressForDiscord.Models;
using CompressForDiscord.Services;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class FfprobeParserTests
{
    private const string Mp4WithAudio = """
        {
          "streams": [
            {
              "codec_type": "video", "codec_name": "h264",
              "width": 1920, "height": 1080, "pix_fmt": "yuv420p",
              "avg_frame_rate": "30000/1001", "r_frame_rate": "30000/1001",
              "duration": "5.305300", "nb_frames": "159"
            },
            {
              "codec_type": "audio", "codec_name": "aac", "channels": 2
            }
          ],
          "format": { "format_name": "mov,mp4,m4a,3gp,3g2,mj2", "duration": "5.312000", "size": "12345678" }
        }
        """;

    private const string RotatedPhoneVideo = """
        {
          "streams": [
            {
              "codec_type": "video", "codec_name": "hevc",
              "width": 1920, "height": 1080, "pix_fmt": "yuv420p10le",
              "avg_frame_rate": "60/1",
              "side_data_list": [ { "side_data_type": "Display Matrix", "rotation": -90 } ]
            }
          ],
          "format": { "format_name": "mov,mp4,m4a,3gp,3g2,mj2", "duration": "10.0" }
        }
        """;

    private const string GifNoDuration = """
        {
          "streams": [
            {
              "codec_type": "video", "codec_name": "gif",
              "width": 480, "height": 270, "pix_fmt": "bgra",
              "avg_frame_rate": "12/1", "nb_frames": "48", "duration": "N/A"
            }
          ],
          "format": { "format_name": "gif" }
        }
        """;

    private const string AudioOnly = """
        {
          "streams": [ { "codec_type": "audio", "codec_name": "mp3", "channels": 2 } ],
          "format": { "format_name": "mp3", "duration": "180.5" }
        }
        """;

    [Fact]
    public void Mp4_ParsesFormatStreamsAndDuration()
    {
        var probe = FfprobeParser.Parse(Mp4WithAudio);

        Assert.Equal("mov,mp4,m4a,3gp,3g2,mj2", probe.FormatName);
        Assert.Equal(5.312, probe.DurationSeconds!.Value, precision: 3);
        Assert.Equal("h264", probe.Video!.CodecName);
        Assert.Equal(1920, probe.Video.Width);
        Assert.Equal(1080, probe.Video.Height);
        Assert.Equal(29.97, probe.Video.Fps!.Value, precision: 2);
        Assert.Equal(0, probe.Video.RotationDegrees);
        Assert.Equal("aac", probe.Audio!.CodecName);
        Assert.Equal(2, probe.Audio.Channels);
    }

    [Fact]
    public void RotationSideData_IsExtracted()
    {
        var probe = FfprobeParser.Parse(RotatedPhoneVideo);

        Assert.Equal(-90, probe.Video!.RotationDegrees);
    }

    [Fact]
    public void NaDuration_FallsBackToFrameCountOverFps()
    {
        var probe = FfprobeParser.Parse(GifNoDuration);

        // 48 frames at 12 fps = 4 s.
        Assert.Equal(4.0, probe.DurationSeconds!.Value, precision: 3);
    }

    [Fact]
    public void PacketCount_ParsesStringCounter()
    {
        const string json = """{ "streams": [ { "nb_read_packets": "42" } ] }""";

        Assert.Equal(42, FfprobeParser.ParsePacketCount(json));
    }

    [Theory]
    [InlineData("gif", 48L, MediaKind.AnimatedImage)]
    [InlineData("gif", 1L, MediaKind.StaticImage)]
    [InlineData("webp_pipe", 10L, MediaKind.AnimatedImage)]
    [InlineData("apng", 1L, MediaKind.StaticImage)]
    [InlineData("png_pipe", null, MediaKind.StaticImage)]
    [InlineData("image2", null, MediaKind.StaticImage)]
    [InlineData("jpeg_pipe", null, MediaKind.StaticImage)]
    [InlineData("bmp_pipe", null, MediaKind.StaticImage)]
    [InlineData("matroska,webm", null, MediaKind.Video)]
    [InlineData("mov,mp4,m4a,3gp,3g2,mj2", null, MediaKind.Video)]
    public void Classification_ByContainerAndPacketCount(string format, long? packets, MediaKind expected)
    {
        var probe = new RawProbe(format, 1.0,
            new RawVideoStream("x", 100, 100, "yuv420p", 30, null, 0),
            Audio: null);

        Assert.Equal(expected, FfprobeParser.Classify(probe, packets));
    }

    [Fact]
    public void AudioOnly_IsUnsupported()
    {
        var probe = FfprobeParser.Parse(AudioOnly);

        Assert.Null(probe.Video);
        Assert.Equal(MediaKind.Unsupported, FfprobeParser.Classify(probe, null));
    }

    [Theory]
    [InlineData("gif", true)]
    [InlineData("webp_pipe", true)]
    [InlineData("apng", true)]
    [InlineData("png_pipe", false)]
    [InlineData("mov,mp4,m4a,3gp,3g2,mj2", false)]
    public void OnlyPotentiallyAnimatedContainers_NeedTheSecondProbe(string format, bool expected)
    {
        Assert.Equal(expected, FfprobeParser.RequiresAnimationProbe(format));
    }

    [Theory]
    [InlineData("30000/1001", 29.97)]
    [InlineData("25/1", 25.0)]
    [InlineData("30", 30.0)]
    [InlineData("0/0", null)]
    [InlineData("N/A", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void FrameRateFractions_AreParsed(string? input, double? expected)
    {
        double? actual = FfprobeParser.ParseFrameRate(input);

        if (expected is null)
        {
            Assert.Null(actual);
        }
        else
        {
            Assert.Equal(expected.Value, actual!.Value, precision: 2);
        }
    }

    [Theory]
    [InlineData("yuva420p", true)]
    [InlineData("rgba", true)]
    [InlineData("bgra", true)]
    [InlineData("argb", true)]
    [InlineData("gbrap12le", true)]
    [InlineData("ya8", true)]
    [InlineData("pal8", true)]
    [InlineData("yuv420p", false)]
    [InlineData("yuv444p10le", false)]
    [InlineData("gray", false)]
    [InlineData("gbrp", false)]
    [InlineData("rgb24", false)]
    [InlineData(null, false)]
    public void AlphaDetection_ByPixelFormat(string? pixFmt, bool expected)
    {
        Assert.Equal(expected, FfprobeParser.PixFmtHasAlpha(pixFmt));
    }
}
