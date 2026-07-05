using System;
using System.IO;

namespace CompressForDiscord.Infrastructure;

/// <summary>
/// Tiny append-only log under %TEMP%\CompressForDiscord\logs. The app is a short-lived
/// context-menu utility — a real logging stack would be overkill; this exists so field
/// failures (shell registration, libvlc, clipboard) leave a trace.
/// </summary>
internal static class AppLog
{
    private static readonly object Gate = new();

    private static readonly string LogPath = Path.Combine(
        Path.GetTempPath(), "CompressForDiscord", "logs",
        $"app-{DateTime.Now:yyyyMMdd}.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Logging must never take the app down.
        }
    }

    public static void Write(string context, Exception exception) =>
        Write($"{context}: {exception.GetType().Name}: {exception.Message}");
}
