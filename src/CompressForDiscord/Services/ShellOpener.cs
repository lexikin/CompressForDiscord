using System;
using System.Diagnostics;

namespace CompressForDiscord.Services;

/// <summary>Best-effort "reveal in file manager" / "open in default player" helpers.</summary>
internal static class ShellOpener
{
    public static void ShowInFolder(string path)
    {
        Try(() =>
        {
            if (OperatingSystem.IsWindows())
            {
                // explorer's /select needs the embedded-quotes form; ArgumentList mangles the comma syntax.
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = false,
                });
            }
            else
            {
                var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                psi.ArgumentList.Add(System.IO.Path.GetDirectoryName(path) ?? "/");
                Process.Start(psi);
            }
        });
    }

    public static void OpenInDefaultApp(string path)
    {
        Try(() =>
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            else
            {
                var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                psi.ArgumentList.Add(path);
                Process.Start(psi);
            }
        });
    }

    public static void OpenUrl(string url)
    {
        Try(() =>
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                psi.ArgumentList.Add(url);
                Process.Start(psi);
            }
        });
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Infrastructure.AppLog.Write($"Shell open failed: {e.Message}");
        }
    }
}
