namespace CompressForDiscord.Models;

/// <summary>Progress reported to the UI while a job runs. Percent &lt; 0 = indeterminate.</summary>
internal sealed record CompressionProgress(double Percent, string Phase);

/// <summary>Outcome of a successful compression job.</summary>
internal sealed record CompressionResult(
    string OutputPath,
    long OutputBytes,
    MediaKind Kind,
    bool WasSkipped,
    int Attempts,
    bool UsedFallbackDirectory,
    int? Width,
    int? Height);
