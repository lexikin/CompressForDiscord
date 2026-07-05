using CompressForDiscord.Services;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class FfmpegProgressParserTests
{
    [Fact]
    public void OnlyProgressLines_EmitUpdates()
    {
        var parser = new FfmpegProgressParser(100);

        Assert.Null(parser.ParseLine("frame=123"));
        Assert.Null(parser.ParseLine("fps=42.5"));
        Assert.Null(parser.ParseLine("out_time_us=50000000"));
        Assert.Null(parser.ParseLine(""));
        Assert.Null(parser.ParseLine(null));
        Assert.Null(parser.ParseLine("garbage without equals"));

        var update = parser.ParseLine("progress=continue");
        Assert.NotNull(update);
        Assert.Equal(0.5, update!.Fraction, precision: 6);
        Assert.False(update.Ended);
    }

    [Fact]
    public void OutTimeUs_IsPreferredOverOutTimeMs()
    {
        var parser = new FfmpegProgressParser(100);

        parser.ParseLine("out_time_us=50000000");
        parser.ParseLine("out_time_ms=999");            // would be nonsense either way
        var update = parser.ParseLine("progress=continue");

        Assert.Equal(0.5, update!.Fraction, precision: 6);
    }

    [Fact]
    public void OutTimeMs_IsActuallyMicroseconds()
    {
        var parser = new FfmpegProgressParser(100);

        // 50,000,000 "ms" would be 50,000 s; as µs it's the correct 50 s.
        parser.ParseLine("out_time_ms=50000000");
        var update = parser.ParseLine("progress=continue");

        Assert.Equal(0.5, update!.Fraction, precision: 6);
    }

    [Fact]
    public void OutTimeString_IsTheLastFallback()
    {
        var parser = new FfmpegProgressParser(60);

        parser.ParseLine("out_time=00:00:30.000000");
        var update = parser.ParseLine("progress=continue");

        Assert.Equal(0.5, update!.Fraction, precision: 6);
    }

    [Fact]
    public void FractionIsClampedTo99Percent_UntilEnd()
    {
        var parser = new FfmpegProgressParser(100);

        parser.ParseLine("out_time_us=200000000"); // 200 s of a "100 s" file
        var running = parser.ParseLine("progress=continue");
        Assert.Equal(0.99, running!.Fraction);

        var ended = parser.ParseLine("progress=end");
        Assert.Equal(1.0, ended!.Fraction);
        Assert.True(ended.Ended);
    }

    [Fact]
    public void Speed_IsParsed_AndNaIsNull()
    {
        var parser = new FfmpegProgressParser(100);

        parser.ParseLine("speed=2.5x");
        Assert.Equal(2.5, parser.ParseLine("progress=continue")!.Speed);

        parser.ParseLine("speed=N/A");
        Assert.Null(parser.ParseLine("progress=continue")!.Speed);
    }

    [Fact]
    public void MicrosecondPreference_ResetsPerBlock()
    {
        var parser = new FfmpegProgressParser(100);

        parser.ParseLine("out_time_us=10000000");
        parser.ParseLine("progress=continue");

        // New block carries only out_time_ms — it must be honored now.
        parser.ParseLine("out_time_ms=50000000");
        var update = parser.ParseLine("progress=continue");

        Assert.Equal(0.5, update!.Fraction, precision: 6);
    }

    [Fact]
    public void UnknownDuration_ReportsZeroUntilEnd()
    {
        var parser = new FfmpegProgressParser(null);

        parser.ParseLine("out_time_us=50000000");
        Assert.Equal(0.0, parser.ParseLine("progress=continue")!.Fraction);
        Assert.Equal(1.0, parser.ParseLine("progress=end")!.Fraction);
    }

    [Fact]
    public void NaOutTime_IsIgnored()
    {
        var parser = new FfmpegProgressParser(100);

        parser.ParseLine("out_time_us=N/A");
        parser.ParseLine("out_time_ms=N/A");
        var update = parser.ParseLine("progress=continue");

        Assert.Equal(0.0, update!.Fraction);
    }
}
