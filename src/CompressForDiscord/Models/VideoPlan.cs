using System;

namespace CompressForDiscord.Models;

/// <summary>Encoder parameters for one two-pass H.264/AAC attempt.</summary>
internal sealed record VideoPlan(
    int VideoKbps,
    int? AudioKbps,
    int AudioChannels,
    int Width,
    int Height,
    double? Fps)
{
    /// <summary>
    /// True when this plan produces the same pass-1 filter chain as <paramref name="other"/>
    /// (same dims + fps). x264 pass-1 stats are per-frame complexity data that rate control
    /// rescales to any target bitrate (the classic bitrate-ladder workflow), so a retry with
    /// an unchanged chain can skip pass 1 and reuse the stats.
    /// </summary>
    public bool SameVideoChainAs(VideoPlan other) =>
        Width == other.Width && Height == other.Height && Nullable.Equals(Fps, other.Fps);
}

/// <summary>Planner outcome: either a plan, or an actionable reason nothing can fit.</summary>
internal sealed record VideoPlanResult(VideoPlan? Plan, string? CannotFitReason)
{
    public static VideoPlanResult Fits(VideoPlan plan) => new(plan, null);
    public static VideoPlanResult CannotFit(string reason) => new(null, reason);
}
