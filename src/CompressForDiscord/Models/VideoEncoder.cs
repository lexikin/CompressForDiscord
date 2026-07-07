namespace CompressForDiscord.Models;

/// <summary>
/// A selectable H.264 encoder. All targets are H.264 (Discord-friendly, plays inline
/// everywhere); they differ only in the ffmpeg codec and whether they run on the GPU.
/// </summary>
internal sealed record VideoEncoder(string DisplayName, string CodecArg, bool IsHardware)
{
    public static readonly VideoEncoder Software = new("x264 (CPU)", "libx264", IsHardware: false);

    /// <summary>Hardware encoders in preference order (best quality-per-bit and speed first).</summary>
    public static readonly VideoEncoder[] HardwareCandidates =
    [
        new("NVENC (NVIDIA)", "h264_nvenc", IsHardware: true),
        new("Quick Sync (Intel)", "h264_qsv", IsHardware: true),
        new("AMF (AMD)", "h264_amf", IsHardware: true),
    ];
}
