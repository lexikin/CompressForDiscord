using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Infrastructure;
using CompressForDiscord.Models;

namespace CompressForDiscord.Services;

internal interface IMediaProber
{
    Task<MediaInfo> ProbeAsync(string path, CancellationToken ct);
}

internal sealed class MediaProber(IFfmpegRunner runner) : IMediaProber
{
    public async Task<MediaInfo> ProbeAsync(string path, CancellationToken ct)
    {
        long fileSize = new FileInfo(path).Length;

        var result = await runner.RunFfprobeAsync(FfmpegArgsBuilder.BuildProbeArgs(path), ct);
        if (!result.Success)
        {
            throw new UnsupportedInputException(
                "Couldn't read this as a media file.", result.StderrTail);
        }

        var probe = FfprobeParser.Parse(result.StdOut);

        // Animated-or-not needs a packet count, but only for image containers —
        // never demux-scan a multi-gigabyte video for this.
        long? packetCount = null;
        if (FfprobeParser.RequiresAnimationProbe(probe.FormatName))
        {
            var countResult = await runner.RunFfprobeAsync(
                FfmpegArgsBuilder.BuildPacketCountArgs(path), ct);
            if (countResult.Success)
            {
                packetCount = FfprobeParser.ParsePacketCount(countResult.StdOut);
            }
        }

        var kind = FfprobeParser.Classify(probe, packetCount);

        VideoStreamInfo? video = null;
        if (probe.Video is { } raw)
        {
            // ffmpeg autorotates on decode, so the planner must see display orientation.
            bool swapped = raw.RotationDegrees % 180 != 0;
            video = new VideoStreamInfo(
                CodecName: raw.CodecName,
                Width: swapped ? raw.Height : raw.Width,
                Height: swapped ? raw.Width : raw.Height,
                PixelFormat: raw.PixFmt,
                Fps: raw.Fps,
                HasAlpha: FfprobeParser.PixFmtHasAlpha(raw.PixFmt));
        }

        return new MediaInfo(
            FilePath: path,
            FileSizeBytes: fileSize,
            Kind: kind,
            ContainerFormat: probe.FormatName,
            DurationSeconds: probe.DurationSeconds,
            Video: video,
            Audio: probe.Audio is { } audio ? new AudioStreamInfo(audio.CodecName, audio.Channels) : null);
    }
}
