namespace CompressForDiscord.Models;

/// <summary>Encoder parameters for one single-pass H.264/AAC attempt.</summary>
internal sealed record VideoPlan(
    int VideoKbps,
    int? AudioKbps,
    int AudioChannels,
    int Width,
    int Height,
    double? Fps);

/// <summary>Planner outcome: either a plan, or an actionable reason nothing can fit.</summary>
internal sealed record VideoPlanResult(VideoPlan? Plan, string? CannotFitReason)
{
    public static VideoPlanResult Fits(VideoPlan plan) => new(plan, null);
    public static VideoPlanResult CannotFit(string reason) => new(null, reason);
}
