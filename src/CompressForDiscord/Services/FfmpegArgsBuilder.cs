using System.Collections.Generic;
using System.Globalization;
using CompressForDiscord.Models;

namespace CompressForDiscord.Services;

/// <summary>
/// Pure construction of ffmpeg/ffprobe argument vectors. Always consumed via
/// ProcessStartInfo.ArgumentList — never joined into a command string — so paths
/// with spaces/unicode need no quoting here.
/// </summary>
internal static class FfmpegArgsBuilder
{
    internal static string[] BuildProbeArgs(string input) =>
    [
        "-v", "error",
        "-print_format", "json",
        "-show_format", "-show_streams",
        "-i", input,
    ];

    internal static string[] BuildPacketCountArgs(string input) =>
    [
        "-v", "error",
        "-select_streams", "v:0",
        "-count_packets",
        "-show_entries", "stream=nb_read_packets",
        "-print_format", "json",
        "-i", input,
    ];

    internal static string[] BuildVideoArgs(string input, VideoPlan plan, VideoEncoder encoder, string output)
    {
        var args = CommonEncodeArgs(input);
        args.AddRange(["-map", "0:v:0"]);
        if (plan.AudioKbps is not null)
        {
            args.AddRange(["-map", "0:a:0"]);
        }

        args.AddRange(["-map_metadata", "-1", "-sn", "-dn"]);
        args.AddRange(["-vf", BuildVideoFilterChain(plan)]);
        args.AddRange(RateControlArgs(plan, encoder));

        if (plan.AudioKbps is int audioKbps)
        {
            args.AddRange([
                "-c:a", "aac",
                "-b:a", Invariant($"{audioKbps}k"),
                "-ac", Invariant($"{plan.AudioChannels}"),
            ]);
        }
        else
        {
            args.Add("-an");
        }

        // faststart relocates the moov atom so Discord/browsers can stream from byte 0.
        args.AddRange(["-movflags", "+faststart", "-f", "mp4", output]);
        return [.. args];
    }

    internal static string[] BuildPngArgs(string input, int? width, int? height, bool hasAlpha, string output)
    {
        var args = CommonEncodeArgs(input);
        args.AddRange(["-map", "0:v:0", "-frames:v", "1"]);

        // rgba only when the source has alpha — otherwise rgb24 prevents
        // 16-bit-per-channel PNG bloat from 10/16-bit sources.
        string format = hasAlpha ? "rgba" : "rgb24";
        string chain = width is int w && height is int h
            ? Invariant($"scale={w}:{h}:flags=lanczos,format={format}")
            : Invariant($"format={format}");

        args.AddRange(["-vf", chain]);
        args.AddRange(["-c:v", "png", "-compression_level", "9", "-pred", "mixed", output]);
        return [.. args];
    }

    internal static string[] BuildThumbnailArgs(string input, double seekSeconds, string output) =>
    [
        "-y", "-loglevel", "error",
        "-ss", seekSeconds.ToString("0.###", CultureInfo.InvariantCulture),
        "-i", input,
        "-frames:v", "1",
        "-vf", "scale=640:-2",
        output,
    ];

    /// <summary>fps=…,scale=W:H:flags=lanczos,format=yuv420p — identical for both passes.</summary>
    internal static string BuildVideoFilterChain(VideoPlan plan)
    {
        var parts = new List<string>(3);
        if (plan.Fps is double fps)
        {
            parts.Add(Invariant($"fps={fps.ToString("0.###", CultureInfo.InvariantCulture)}"));
        }

        parts.Add(Invariant($"scale={plan.Width}:{plan.Height}:flags=lanczos"));
        parts.Add("format=yuv420p"); // fixes yuv444p / 10-bit / rgb GIF sources
        return string.Join(',', parts);
    }

    private static List<string> CommonEncodeArgs(string input) =>
        ["-y", "-loglevel", "error", "-progress", "pipe:1", "-i", input];

    // Single-pass VBR with a gentle VBV cap (maxrate 1.5x, bufsize 2x): fast, and close enough
    // on size that the orchestrator's verify-and-retry loop mops up the occasional overshoot.
    // Hardware encoders keep their default (fast) preset; only x264 needs an explicit speed pick.
    private static List<string> RateControlArgs(VideoPlan plan, VideoEncoder encoder)
    {
        var args = new List<string> { "-c:v", encoder.CodecArg };
        if (!encoder.IsHardware)
        {
            args.AddRange(["-preset", "veryfast"]);
        }

        args.AddRange([
            "-b:v", Invariant($"{plan.VideoKbps}k"),
            "-maxrate", Invariant($"{plan.VideoKbps * 150 / 100}k"),
            "-bufsize", Invariant($"{plan.VideoKbps * 2}k"),
        ]);
        return args;
    }

    private static string Invariant(FormattableString value) =>
        FormattableString.Invariant(value);
}
