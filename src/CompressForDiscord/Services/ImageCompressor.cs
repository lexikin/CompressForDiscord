using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Infrastructure;
using CompressForDiscord.Models;
using CompressForDiscord.Services.Planning;

namespace CompressForDiscord.Services;

/// <summary>
/// PNG encoder with a downscale search: native resolution first, then a pixel-proportional
/// jump and bisection (see <see cref="PngScaleSearch"/>), max 6 encodes, never below a
/// 48 px short side.
/// </summary>
internal interface IImageCompressor : IMediaCompressor;

internal sealed class ImageCompressor(IFfmpegRunner runner) : IImageCompressor
{
    internal const int MinShortSidePixels = 48;

    public async Task<CompressorOutput> CompressAsync(
        MediaInfo media, long limitBytes, string jobTempDir,
        IProgress<CompressionProgress> progress, CancellationToken ct)
    {
        var video = media.Video ?? throw new UnsupportedInputException("The image has no decodable picture.");

        int shortSide = Math.Min(video.Width, video.Height);
        double minScale = Math.Min(1.0, (double)MinShortSidePixels / shortSide);
        var search = new PngScaleSearch(limitBytes, minScale);
        var fits = new Dictionary<double, (string Path, int Width, int Height)>();

        int encodeIndex = 0;
        while (search.NextScale is { } scale)
        {
            ct.ThrowIfCancellationRequested();
            encodeIndex++;
            progress.Report(new CompressionProgress(
                Math.Min(90, encodeIndex * 15),
                encodeIndex == 1 ? "Optimizing image…" : $"Optimizing image (try {encodeIndex})…"));

            (int? width, int? height) = ScaleToDimensions(scale, video.Width, video.Height);
            string output = Path.Combine(jobTempDir, string.Create(
                CultureInfo.InvariantCulture, $"out-e{encodeIndex}.png"));

            var args = FfmpegArgsBuilder.BuildPngArgs(media.FilePath, width, height, video.HasAlpha, output);
            var result = await runner.RunFfmpegAsync(args, durationSeconds: null, progress: null, ct);
            if (!result.Success)
            {
                throw new CompressionFailedException(
                    $"ffmpeg failed while encoding the PNG (exit code {result.ExitCode}).",
                    result.StderrTail);
            }

            long bytes = new FileInfo(output).Length;
            if (bytes <= limitBytes)
            {
                fits[scale] = (output, width ?? video.Width, height ?? video.Height);
            }

            search.Report(scale, bytes);
        }

        if (search.BestFit is { } bestFit)
        {
            var (path, w, h) = fits[bestFit];
            return new CompressorOutput(path, encodeIndex, w, h);
        }

        double limitMiB = limitBytes / (double)AppSettings.BytesPerUnit;
        throw new CannotFitException(search.MinScaleTooLarge
            ? string.Create(CultureInfo.InvariantCulture,
                $"Even scaled down to {MinShortSidePixels} px this image exceeds {limitMiB:0.#} MiB. Try a larger size preset.")
            : "Couldn't get the image under the size limit. Try a larger size preset.");
    }

    private static (int? Width, int? Height) ScaleToDimensions(double scale, int srcWidth, int srcHeight)
    {
        if (Math.Abs(scale - 1.0) < 1e-9)
        {
            return (null, null); // native resolution — no scale filter
        }

        return (
            Math.Max(1, (int)Math.Round(srcWidth * scale, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(srcHeight * scale, MidpointRounding.AwayFromZero)));
    }
}
