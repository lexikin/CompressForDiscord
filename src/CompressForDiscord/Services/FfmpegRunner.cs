using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CompressForDiscord.Services;

internal sealed record FfmpegResult(int ExitCode, string StdOut, string StderrTail)
{
    public bool Success => ExitCode == 0;
}

internal interface IFfmpegRunner
{
    /// <summary>
    /// Runs ffmpeg with `-progress pipe:1` progress reporting. stdout is consumed by the
    /// progress parser (not accumulated); stderr's last lines are returned for error dialogs.
    /// </summary>
    Task<FfmpegResult> RunFfmpegAsync(
        IReadOnlyList<string> args,
        double? durationSeconds,
        IProgress<ProgressUpdate>? progress,
        CancellationToken ct);

    /// <summary>Runs ffprobe and returns its full stdout (JSON).</summary>
    Task<FfmpegResult> RunFfprobeAsync(IReadOnlyList<string> args, CancellationToken ct);
}

internal sealed class FfmpegRunner(IFfmpegLocator locator) : IFfmpegRunner
{
    private const int StderrTailLines = 40;

    public Task<FfmpegResult> RunFfmpegAsync(
        IReadOnlyList<string> args, double? durationSeconds,
        IProgress<ProgressUpdate>? progress, CancellationToken ct)
    {
        var parser = new FfmpegProgressParser(durationSeconds);
        return RunAsync(
            locator.Resolve().FfmpegPath,
            ["-nostdin", "-hide_banner", .. args],
            captureStdOut: false,
            onStdOutLine: line =>
            {
                if (parser.ParseLine(line) is { } update)
                {
                    progress?.Report(update);
                }
            },
            ct);
    }

    public Task<FfmpegResult> RunFfprobeAsync(IReadOnlyList<string> args, CancellationToken ct) =>
        RunAsync(
            locator.Resolve().FfprobePath,
            ["-hide_banner", .. args],
            captureStdOut: true,
            onStdOutLine: null,
            ct);

    private static async Task<FfmpegResult> RunAsync(
        string executable, IReadOnlyList<string> args, bool captureStdOut,
        Action<string>? onStdOutLine, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg); // never join into a string — unicode/space safety
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = captureStdOut ? new StringBuilder() : null;
        var stderrTail = new Queue<string>(StderrTailLines);

        var stdoutTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(CancellationToken.None) is { } line)
            {
                stdout?.AppendLine(line);
                onStdOutLine?.Invoke(line);
            }
        }, CancellationToken.None);

        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(CancellationToken.None) is { } line)
            {
                if (stderrTail.Count == StderrTailLines)
                {
                    stderrTail.Dequeue();
                }

                stderrTail.Enqueue(line);
            }
        }, CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // already exited — nothing to kill
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            throw;
        }

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        return new FfmpegResult(
            process.ExitCode,
            stdout?.ToString() ?? "",
            string.Join(Environment.NewLine, stderrTail));
    }
}
