using System;
using CompressForDiscord.Services;
using Xunit;

namespace CompressForDiscord.Tests.Integration;

/// <summary>
/// A [Fact] that runs only when ffmpeg/ffprobe are resolvable (FFMPEG_PATH → exe dir → PATH).
/// CI always fetches ffmpeg; on dev machines without it these tests skip instead of failing.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    private static readonly string? SkipReason = Probe();

    public IntegrationFactAttribute()
    {
        Skip = SkipReason;
        Timeout = 300_000; // VP9 encodes are slow on small CI runners
    }

    private static string? Probe()
    {
        try
        {
            _ = new FfmpegLocator().Resolve();
            return null;
        }
        catch (Exception)
        {
            return "ffmpeg/ffprobe not found (set FFMPEG_PATH or add to PATH)";
        }
    }
}
