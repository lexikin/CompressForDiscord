using System;
using System.IO;

namespace CompressForDiscord.Services;

internal sealed record FfmpegPaths(string FfmpegPath, string FfprobePath, string Origin);

internal interface IFfmpegLocator
{
    /// <summary>Resolves ffmpeg/ffprobe once; throws <see cref="Infrastructure.FfmpegNotFoundException"/>.</summary>
    FfmpegPaths Resolve();
}

/// <summary>
/// Resolution order (contract shared with the installers, which stage the binaries
/// side-by-side with the exe): FFMPEG_PATH env dir → AppContext.BaseDirectory → PATH.
/// </summary>
internal sealed class FfmpegLocator : IFfmpegLocator
{
    internal const string EnvVar = "FFMPEG_PATH";

    private readonly Lazy<FfmpegPaths> _resolved;

    public FfmpegLocator() => _resolved = new Lazy<FfmpegPaths>(ResolveCore);

    public FfmpegPaths Resolve() => _resolved.Value;

    private static FfmpegPaths ResolveCore()
    {
        string exe = OperatingSystem.IsWindows() ? ".exe" : "";

        if (Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } envDir &&
            TryDirectory(envDir, exe) is { } fromEnv)
        {
            return fromEnv with { Origin = $"{EnvVar} environment variable" };
        }

        if (TryDirectory(AppContext.BaseDirectory, exe) is { } bundled)
        {
            return bundled with { Origin = "bundled" };
        }

        string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryDirectory(dir.Trim(), exe) is { } fromPath)
            {
                return fromPath with { Origin = "PATH" };
            }
        }

        throw new Infrastructure.FfmpegNotFoundException(
            "ffmpeg and ffprobe were not found. Reinstalling Compress for Discord should fix this; " +
            $"alternatively set {EnvVar} to a directory containing them.");
    }

    private static FfmpegPaths? TryDirectory(string directory, string exe)
    {
        try
        {
            string ffmpeg = Path.Combine(directory, "ffmpeg" + exe);
            string ffprobe = Path.Combine(directory, "ffprobe" + exe);
            return File.Exists(ffmpeg) && File.Exists(ffprobe)
                ? new FfmpegPaths(ffmpeg, ffprobe, directory)
                : null;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null; // malformed PATH entries etc. — keep scanning
        }
    }
}
