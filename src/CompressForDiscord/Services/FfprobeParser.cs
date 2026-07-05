using System;
using System.Globalization;
using System.Text.Json;
using CompressForDiscord.Models;

namespace CompressForDiscord.Services;

/// <summary>Raw, display-orientation-agnostic probe data as ffprobe reported it.</summary>
internal sealed record RawProbe(
    string? FormatName,
    double? DurationSeconds,
    RawVideoStream? Video,
    RawAudioStream? Audio);

internal sealed record RawVideoStream(
    string CodecName,
    int Width,
    int Height,
    string? PixFmt,
    double? Fps,
    long? NbFrames,
    int RotationDegrees);

internal sealed record RawAudioStream(string CodecName, int Channels);

/// <summary>Pure parsing of `ffprobe -print_format json` output. No I/O.</summary>
internal static class FfprobeParser
{
    internal static RawProbe Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? formatName = null;
        double? formatDuration = null;
        if (root.TryGetProperty("format", out var format))
        {
            formatName = GetString(format, "format_name");
            formatDuration = GetDouble(format, "duration");
        }

        RawVideoStream? video = null;
        RawAudioStream? audio = null;
        double? streamDuration = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                string? codecType = GetString(stream, "codec_type");
                if (codecType == "video" && video is null)
                {
                    video = new RawVideoStream(
                        CodecName: GetString(stream, "codec_name") ?? "unknown",
                        Width: GetInt(stream, "width") ?? 0,
                        Height: GetInt(stream, "height") ?? 0,
                        PixFmt: GetString(stream, "pix_fmt"),
                        Fps: ParseFrameRate(GetString(stream, "avg_frame_rate"))
                             ?? ParseFrameRate(GetString(stream, "r_frame_rate")),
                        NbFrames: GetLong(stream, "nb_frames"),
                        RotationDegrees: ExtractRotation(stream));
                    streamDuration ??= GetDouble(stream, "duration");
                }
                else if (codecType == "audio" && audio is null)
                {
                    audio = new RawAudioStream(
                        CodecName: GetString(stream, "codec_name") ?? "unknown",
                        Channels: GetInt(stream, "channels") ?? 2);
                }
            }
        }

        // Duration fallback chain: format → stream → frame count / fps.
        double? duration = formatDuration ?? streamDuration;
        if (duration is null && video is { NbFrames: > 0, Fps: > 0 })
        {
            duration = video.NbFrames.Value / video.Fps.Value;
        }

        return new RawProbe(formatName, duration, video, audio);
    }

    /// <summary>Parses the targeted `-count_packets` probe; null when unavailable.</summary>
    internal static long? ParsePacketCount(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                if (GetLong(stream, "nb_read_packets") is long n)
                {
                    return n;
                }
            }
        }

        return null;
    }

    /// <summary>Container formats whose animation state needs the packet-count probe.</summary>
    internal static bool RequiresAnimationProbe(string? formatName) =>
        formatName is "gif" or "webp_pipe" or "apng";

    internal static MediaKind Classify(RawProbe probe, long? packetCount)
    {
        if (probe.Video is null || probe.Video.Width <= 0 || probe.Video.Height <= 0)
        {
            return MediaKind.Unsupported;
        }

        return probe.FormatName switch
        {
            "gif" or "webp_pipe" or "apng" =>
                packetCount > 1 ? MediaKind.AnimatedImage : MediaKind.StaticImage,
            "png_pipe" or "image2" or "jpeg_pipe" or "bmp_pipe" or "tiff_pipe" =>
                MediaKind.StaticImage,
            _ => MediaKind.Video,
        };
    }

    /// <summary>"30000/1001" → 29.97; "0/0", "N/A", null → null.</summary>
    internal static double? ParseFrameRate(string? fraction)
    {
        if (string.IsNullOrEmpty(fraction))
        {
            return null;
        }

        int slash = fraction.IndexOf('/');
        if (slash < 0)
        {
            return double.TryParse(fraction, CultureInfo.InvariantCulture, out double plain) && plain > 0
                ? plain
                : null;
        }

        if (double.TryParse(fraction.AsSpan(0, slash), CultureInfo.InvariantCulture, out double num) &&
            double.TryParse(fraction.AsSpan(slash + 1), CultureInfo.InvariantCulture, out double den) &&
            num > 0 && den > 0)
        {
            return num / den;
        }

        return null;
    }

    internal static bool PixFmtHasAlpha(string? pixFmt) => pixFmt switch
    {
        null => false,
        "pal8" => true, // GIF palettes may carry transparency; rgba PNG is the safe choice
        _ => pixFmt.StartsWith("rgba", StringComparison.Ordinal)
             || pixFmt.StartsWith("bgra", StringComparison.Ordinal)
             || pixFmt.StartsWith("abgr", StringComparison.Ordinal)
             || pixFmt.StartsWith("argb", StringComparison.Ordinal)
             || pixFmt.StartsWith("yuva", StringComparison.Ordinal)
             || pixFmt.StartsWith("gbrap", StringComparison.Ordinal)
             || pixFmt.StartsWith("ya", StringComparison.Ordinal),
    };

    private static int ExtractRotation(JsonElement stream)
    {
        if (stream.TryGetProperty("side_data_list", out var sideDataList))
        {
            foreach (var sideData in sideDataList.EnumerateArray())
            {
                if (sideData.TryGetProperty("rotation", out var rotation))
                {
                    if (rotation.ValueKind == JsonValueKind.Number && rotation.TryGetInt32(out int r))
                    {
                        return r;
                    }

                    if (rotation.ValueKind == JsonValueKind.String &&
                        int.TryParse(rotation.GetString(), CultureInfo.InvariantCulture, out int rs))
                    {
                        return rs;
                    }
                }
            }
        }

        return 0;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int result)
            ? result
            : null;

    private static long? GetLong(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        // ffprobe reports counters as strings in JSON.
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out long n) => n,
            JsonValueKind.String when long.TryParse(value.GetString(), CultureInfo.InvariantCulture, out long s) => s,
            _ => null,
        };
    }

    private static double? GetDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        // Durations arrive as strings; "N/A" must map to null.
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), CultureInfo.InvariantCulture, out double s) => s,
            _ => null,
        };
    }
}
