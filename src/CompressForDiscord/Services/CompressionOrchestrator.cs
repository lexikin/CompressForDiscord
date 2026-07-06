using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Infrastructure;
using CompressForDiscord.Models;
using CompressForDiscord.Services.Planning;

namespace CompressForDiscord.Services;

internal interface ICompressionOrchestrator
{
    /// <summary>
    /// Full pipeline: probe → classify → skip-or-compress → verify → move next to the original.
    /// Progress percent &lt; 0 means indeterminate.
    /// </summary>
    Task<CompressionResult> RunAsync(
        string inputPath, long limitBytes,
        IProgress<CompressionProgress> progress, CancellationToken ct);
}

internal sealed class CompressionOrchestrator(
    IMediaProber prober,
    IVideoCompressor videoCompressor,
    IImageCompressor imageCompressor) : ICompressionOrchestrator
{
    private static readonly string[] SkippableWebmCodecs = ["vp8", "vp9", "av1"];

    // hevc deliberately absent: Discord's Chromium-based clients don't play HEVC inline.
    private static readonly string[] SkippableMp4Codecs = ["h264", "av1"];

    internal static string JobTempRoot =>
        Path.Combine(Path.GetTempPath(), "CompressForDiscord");

    public async Task<CompressionResult> RunAsync(
        string inputPath, long limitBytes,
        IProgress<CompressionProgress> progress, CancellationToken ct)
    {
        string jobTempDir = Path.Combine(JobTempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(jobTempDir);

        try
        {
            progress.Report(new CompressionProgress(-1, "Analyzing…"));
            var media = await prober.ProbeAsync(inputPath, ct);

            if (media.Kind == MediaKind.Unsupported)
            {
                throw new UnsupportedInputException(
                    "This file doesn't contain a picture or video that can be compressed.");
            }

            if (CanSkip(media, limitBytes))
            {
                progress.Report(new CompressionProgress(100, "Already fits!"));
                return new CompressionResult(
                    inputPath, media.FileSizeBytes, media.Kind,
                    WasSkipped: true, Attempts: 0, UsedFallbackDirectory: false,
                    media.Video?.Width, media.Video?.Height);
            }

            IMediaCompressor compressor = media.Kind == MediaKind.StaticImage
                ? imageCompressor
                : videoCompressor;
            string targetExtension = media.Kind == MediaKind.StaticImage ? "png" : "mp4";

            var output = await compressor.CompressAsync(media, limitBytes, jobTempDir, progress, ct);

            (string finalPath, bool usedFallback) = MoveIntoPlace(output.Path, inputPath, targetExtension);

            progress.Report(new CompressionProgress(100, "Done"));
            return new CompressionResult(
                finalPath, new FileInfo(finalPath).Length, media.Kind,
                WasSkipped: false, Attempts: output.Attempts, UsedFallbackDirectory: usedFallback,
                output.Width, output.Height);
        }
        finally
        {
            DeleteJobDirWithRetries(jobTempDir);
        }
    }

    /// <summary>Already under the limit and already Discord-friendly → just hand back the original.</summary>
    private static bool CanSkip(MediaInfo media, long limitBytes)
    {
        if (media.FileSizeBytes > limitBytes)
        {
            return false;
        }

        string extension = Path.GetExtension(media.FilePath).ToLowerInvariant();
        return media.Kind switch
        {
            MediaKind.StaticImage => extension == ".png",
            MediaKind.Video or MediaKind.AnimatedImage when media.Video is not null =>
                (extension == ".webm" && Array.IndexOf(SkippableWebmCodecs, media.Video.CodecName) >= 0) ||
                (extension == ".mp4" && Array.IndexOf(SkippableMp4Codecs, media.Video.CodecName) >= 0),
            _ => false,
        };
    }

    private static (string FinalPath, bool UsedFallback) MoveIntoPlace(
        string producedPath, string inputPath, string targetExtension)
    {
        string inputFileName = Path.GetFileName(inputPath);
        string destDir = Path.GetDirectoryName(Path.GetFullPath(inputPath))!;

        try
        {
            return (ClaimAndMove(producedPath, destDir, inputFileName, targetExtension), false);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            // Read-only folder, full disk on that volume, UNC share without write access, …
            // → fall back to ~/Downloads and tell the user via the preview window.
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloads);
            return (ClaimAndMove(producedPath, downloads, inputFileName, targetExtension), true);
        }
    }

    private static string ClaimAndMove(string sourcePath, string destDir, string inputFileName, string ext)
    {
        int probes = 0;
        foreach (string candidate in OutputNamer.CandidateFileNames(inputFileName, ext))
        {
            if (++probes > 1000)
            {
                break; // something is deeply wrong with this directory
            }

            string destPath = Path.Combine(destDir, candidate);
            if (File.Exists(destPath))
            {
                continue;
            }

            try
            {
                File.Move(sourcePath, destPath, overwrite: false);
                return destPath;
            }
            catch (IOException) when (File.Exists(destPath))
            {
                // Lost a race for this name — try the next candidate.
            }
        }

        throw new IOException($"Couldn't claim an output file name in {destDir}.");
    }

    private static void DeleteJobDirWithRetries(string jobTempDir)
    {
        // Windows can briefly hold handles after a process-tree kill.
        for (int i = 0; i < 3; i++)
        {
            try
            {
                if (Directory.Exists(jobTempDir))
                {
                    Directory.Delete(jobTempDir, recursive: true);
                }

                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }
        // Leftovers are caught by the startup sweep.
    }

    /// <summary>Best-effort cleanup of job dirs left behind by crashes. Call once at startup.</summary>
    internal static void SweepStaleJobDirectories()
    {
        try
        {
            if (!Directory.Exists(JobTempRoot))
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-7);
            foreach (string dir in Directory.EnumerateDirectories(JobTempRoot))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Skip busy/locked leftovers; next launch tries again.
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Sweeping is best-effort by design.
        }
    }
}
