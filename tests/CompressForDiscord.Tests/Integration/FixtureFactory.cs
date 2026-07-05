using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CompressForDiscord.Services;

namespace CompressForDiscord.Tests.Integration;

/// <summary>Generates test media with ffmpeg's lavfi sources — no binary fixtures in git.</summary>
internal static class FixtureFactory
{
    internal static string CreateMp4(
        string directory, string name = "input.mp4",
        int seconds = 8, string size = "640x360", int fps = 30, bool audio = true)
    {
        string path = Path.Combine(directory, name);
        List<string> args =
        [
            "-y", "-f", "lavfi", "-i",
            Invariant($"testsrc2=duration={seconds}:size={size}:rate={fps}"),
        ];
        if (audio)
        {
            args.AddRange(["-f", "lavfi", "-i", Invariant($"sine=frequency=440:duration={seconds}")]);
        }

        args.AddRange(["-c:v", "libx264", "-pix_fmt", "yuv420p"]);
        if (audio)
        {
            args.AddRange(["-c:a", "aac", "-shortest"]);
        }

        args.Add(path);
        Run(args);
        return path;
    }

    internal static string CreateAnimatedGif(string directory, string name = "input.gif")
    {
        string path = Path.Combine(directory, name);
        Run(["-y", "-f", "lavfi", "-i", "testsrc2=duration=2:size=320x240:rate=10", path]);
        return path;
    }

    /// <summary>Random noise compresses terribly → a reliably large PNG for downscale tests.</summary>
    internal static string CreateNoisePng(string directory, string name = "input.png", string size = "1920x1080")
    {
        string path = Path.Combine(directory, name);
        Run(
        [
            "-y", "-f", "lavfi",
            "-i", Invariant($"nullsrc=s={size},geq=lum_expr=random(1)*255:cb_expr=128:cr_expr=128"),
            "-frames:v", "1", path,
        ]);
        return path;
    }

    internal static string CreateTinyWebm(string directory, string name = "input.webm")
    {
        string path = Path.Combine(directory, name);
        Run(
        [
            "-y", "-f", "lavfi", "-i", "testsrc2=duration=1:size=160x120:rate=10",
            "-c:v", "libvpx-vp9", "-b:v", "50k", path,
        ]);
        return path;
    }

    private static void Run(IReadOnlyList<string> args)
    {
        var paths = new FfmpegLocator().Resolve();
        var psi = new ProcessStartInfo(paths.FfmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-nostdin");
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"fixture ffmpeg failed ({process.ExitCode}): {stderr}");
        }
    }

    private static string Invariant(FormattableString value) => FormattableString.Invariant(value);
}
