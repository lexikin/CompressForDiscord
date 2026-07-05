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

    internal static string[] BuildVideoPass1Args(string input, VideoPlan plan, string passLogPrefix, string nullSink)
    {
        var args = CommonEncodeArgs(input);
        args.AddRange(["-map", "0:v:0", "-map_metadata", "-1", "-sn", "-dn"]);
        args.AddRange(["-vf", BuildVideoFilterChain(plan)]);
        args.AddRange(RateControlArgs(plan));
        args.AddRange(["-deadline", "good", "-cpu-used", "4", "-row-mt", "1", "-tile-columns", "2", "-g", "240"]);
        args.AddRange(["-pass", "1", "-passlogfile", passLogPrefix]);
        args.AddRange(["-an", "-f", "null", nullSink]);
        return [.. args];
    }

    internal static string[] BuildVideoPass2Args(string input, VideoPlan plan, string passLogPrefix, string output)
    {
        var args = CommonEncodeArgs(input);
        args.AddRange(["-map", "0:v:0"]);
        if (plan.AudioKbps is not null)
        {
            args.AddRange(["-map", "0:a:0"]);
        }

        args.AddRange(["-map_metadata", "-1", "-sn", "-dn"]);
        // The -vf chain must be byte-identical to pass 1 or the stats are useless.
        args.AddRange(["-vf", BuildVideoFilterChain(plan)]);
        args.AddRange(RateControlArgs(plan));
        args.AddRange(["-deadline", "good", "-cpu-used", "2", "-row-mt", "1", "-tile-columns", "2", "-g", "240"]);
        args.AddRange(["-auto-alt-ref", "1", "-lag-in-frames", "25"]);
        args.AddRange(["-pass", "2", "-passlogfile", passLogPrefix]);

        if (plan.AudioKbps is int audioKbps)
        {
            args.AddRange([
                "-c:a", "libopus",
                "-b:a", Invariant($"{audioKbps}k"),
                "-ac", Invariant($"{plan.AudioChannels}"),
            ]);
        }
        else
        {
            args.Add("-an");
        }

        args.AddRange(["-f", "webm", output]);
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

    private static List<string> RateControlArgs(VideoPlan plan) =>
    [
        "-c:v", "libvpx-vp9",
        "-b:v", Invariant($"{plan.VideoKbps}k"),
        "-minrate", Invariant($"{plan.VideoKbps / 2}k"),
        "-maxrate", Invariant($"{plan.VideoKbps * 145 / 100}k"),
    ];

    private static string Invariant(FormattableString value) =>
        FormattableString.Invariant(value);
}
