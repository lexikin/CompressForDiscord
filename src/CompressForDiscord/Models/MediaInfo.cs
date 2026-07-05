namespace CompressForDiscord.Models;

/// <summary>
/// Everything the planner needs to know about an input file. Width/Height are
/// display-oriented (rotation side data already applied — ffmpeg autorotates on decode).
/// </summary>
internal sealed record MediaInfo(
    string FilePath,
    long FileSizeBytes,
    MediaKind Kind,
    string? ContainerFormat,
    double? DurationSeconds,
    VideoStreamInfo? Video,
    AudioStreamInfo? Audio);

internal sealed record VideoStreamInfo(
    string CodecName,
    int Width,
    int Height,
    string? PixelFormat,
    double? Fps,
    bool HasAlpha);

internal sealed record AudioStreamInfo(
    string CodecName,
    int Channels);
