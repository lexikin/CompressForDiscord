using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace CompressForDiscord.Services.Clipboard;

/// <summary>
/// Copies a text/uri-list via wl-copy (Wayland) or xclip (X11). Both tools fork a child that
/// keeps serving the clipboard after this process exits — which matters, because an X11/Wayland
/// clipboard dies with its owner, and Avalonia's X11 backend can't write uri-list at all.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxClipboard : IClipboardFileService
{
    public async Task<ClipboardOutcome> CopyFileAsync(string absolutePath)
    {
        string uriList = UriListBuilder.BuildUriList(absolutePath);

        bool wayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        string[][] commands = wayland
            ?
            [
                ["wl-copy", "--type", "text/uri-list"],
                ["xclip", "-selection", "clipboard", "-t", "text/uri-list", "-i"],
            ]
            :
            [
                ["xclip", "-selection", "clipboard", "-t", "text/uri-list", "-i"],
                ["wl-copy", "--type", "text/uri-list"],
            ];

        foreach (string[] command in commands)
        {
            if (await TryRunAsync(command, uriList))
            {
                return ClipboardOutcome.FileCopied;
            }
        }

        return ClipboardOutcome.Failed; // caller falls back to a plain-text URI
    }

    private static async Task<bool> TryRunAsync(string[] command, string stdin)
    {
        try
        {
            var psi = new ProcessStartInfo(command[0])
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            for (int i = 1; i < command.Length; i++)
            {
                psi.ArgumentList.Add(command[i]);
            }

            using var process = Process.Start(psi)!;
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();

            // Both tools fork and exit the parent promptly; 5 s is generous.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or OperationCanceledException)
        {
            return false; // tool missing or wedged — try the next one
        }
    }
}
