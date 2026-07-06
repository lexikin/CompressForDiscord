using System;
using System.Collections.Generic;
using System.Globalization;
using CompressForDiscord.Models;

namespace CompressForDiscord.Services.Planning;

/// <summary>
/// Pure math: turns (media info, size limit, attempt number) into encoder parameters.
/// No I/O — fully unit-testable.
/// </summary>
internal static class VideoPlanner
{
    /// <summary>Safety margins per attempt; the verify loop tightens on overshoot.</summary>
    internal static readonly double[] Margins = [0.97, 0.90, 0.82];

    internal static int MaxAttempts => Margins.Length;

    /// <summary>Below this video bitrate x264 output is unusable mush — refuse instead.</summary>
    internal const int MinVideoKbps = 50;

    /// <summary>
    /// Quality floor used to pick the resolution rung. Tuned for x264 veryfast, which needs
    /// roughly 40 % more bits per pixel than VP9 did for comparable quality.
    /// </summary>
    internal const double MinBitsPerPixel = 0.022;

    /// <summary>Resolution ladder expressed as the display *short* side (portrait-aware).</summary>
    internal static readonly int[] ShortSideRungs = [2160, 1440, 1080, 720, 480, 360, 240];

    private static readonly (int Kbps, int Channels)[] AudioRungs = [(96, 2), (64, 2), (48, 1)];

    internal static VideoPlanResult Plan(MediaInfo media, long limitBytes, int attempt)
    {
        if (media.Video is null)
        {
            return VideoPlanResult.CannotFit("The file has no video stream.");
        }

        if (media.DurationSeconds is not (> 0 and var duration))
        {
            return VideoPlanResult.CannotFit("Could not determine the video's duration.");
        }

        double margin = Margins[Math.Clamp(attempt, 0, Margins.Length - 1)];
        double totalKbps = limitBytes * 8 * margin / 1000.0 / duration;
        double overheadKbps = Math.Max(2, totalKbps * 0.005);

        // Audio: step down whenever it would eat more than 25 % of the budget.
        (int Kbps, int Channels)? audio = null;
        if (media.Audio is not null)
        {
            audio = AudioRungs[^1];
            foreach (var rung in AudioRungs)
            {
                if (rung.Kbps <= totalKbps * 0.25)
                {
                    audio = rung;
                    break;
                }
            }
        }

        int videoKbps = (int)Math.Floor(totalKbps - (audio?.Kbps ?? 0) - overheadKbps);

        // Last resort before giving up: sacrifice audio entirely.
        if (videoKbps < MinVideoKbps && audio is not null)
        {
            audio = null;
            videoKbps = (int)Math.Floor(totalKbps - overheadKbps);
        }

        if (videoKbps < MinVideoKbps)
        {
            return VideoPlanResult.CannotFit(BuildCannotFitMessage(limitBytes, duration, media.Audio is not null));
        }

        // Resolution/fps ladder: largest rung that keeps bits-per-pixel above the floor.
        int srcShort = Math.Min(media.Video.Width, media.Video.Height);
        var rungs = new List<int>();
        foreach (int rung in ShortSideRungs)
        {
            if (rung <= srcShort)
            {
                rungs.Add(rung); // never upscale
            }
        }

        if (rungs.Count == 0)
        {
            rungs.Add(srcShort); // source smaller than the whole ladder
        }

        (int Width, int Height, double? Fps) chosen = default;
        bool found = false;
        foreach (int rung in rungs)
        {
            var candidate = ResolveRung(media.Video, srcShort, rung);
            double effectiveFps = candidate.Fps ?? media.Video.Fps ?? 30;
            double bitsPerPixel = videoKbps * 1000.0 / (candidate.Width * candidate.Height * effectiveFps);
            if (bitsPerPixel >= MinBitsPerPixel)
            {
                chosen = candidate;
                found = true;
                break;
            }
        }

        if (!found)
        {
            chosen = ResolveRung(media.Video, srcShort, rungs[^1]); // smallest rung, best effort
        }

        var plan = new VideoPlan(
            VideoKbps: videoKbps,
            AudioKbps: audio?.Kbps,
            AudioChannels: audio?.Channels ?? 0,
            Width: chosen.Width,
            Height: chosen.Height,
            Fps: chosen.Fps);

        return VideoPlanResult.Fits(plan);
    }

    private static (int Width, int Height, double? Fps) ResolveRung(VideoStreamInfo video, int srcShort, int rung)
    {
        double fpsCap = rung <= 360 ? 24 : 30;
        // Explicit fps when capping below source or when the source rate is unknown
        // (the fps filter then also gives pass 1/2 an identical, known frame count).
        double? fps = video.Fps is (> 0 and var srcFps)
            ? (srcFps > fpsCap ? fpsCap : null)
            : fpsCap;

        double scale = Math.Min(1.0, (double)rung / srcShort);
        return (EvenDimension(video.Width * scale), EvenDimension(video.Height * scale), fps);
    }

    private static int EvenDimension(double value)
    {
        int n = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        n -= n % 2; // yuv420p requires even dims
        return Math.Max(2, n);
    }

    private static string BuildCannotFitMessage(long limitBytes, double duration, bool hasAudio)
    {
        double minTotalKbps = MinVideoKbps + (hasAudio ? AudioRungs[^1].Kbps : 0) + 2;
        double maxSeconds = limitBytes * 8 * Margins[0] / 1000.0 / minTotalKbps;
        double limitMiB = limitBytes / (double)AppSettings.BytesPerUnit;

        return string.Create(CultureInfo.InvariantCulture,
            $"A {limitMiB:0.#} MiB limit fits about {maxSeconds / 60:0.#} minutes of video at minimum quality, " +
            $"but this video is {duration / 60:0.#} minutes long. Try a larger size preset in settings.");
    }
}
