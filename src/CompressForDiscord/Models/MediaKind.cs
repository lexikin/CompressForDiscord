namespace CompressForDiscord.Models;

public enum MediaKind // public: used in xunit theory signatures
{
    /// <summary>png/jpg/webp/bmp or a single-frame gif — compressed to PNG.</summary>
    StaticImage,

    /// <summary>Animated gif/webp/apng — compressed to MP4 via the video path (no audio).</summary>
    AnimatedImage,

    /// <summary>Anything with a real video stream — compressed to MP4 (H.264 + AAC).</summary>
    Video,

    /// <summary>Audio-only files, no decodable streams, etc.</summary>
    Unsupported,
}
