using System;
using Avalonia;
using CompressForDiscord.Infrastructure;

namespace CompressForDiscord;

internal static class Program
{
    /// <summary>
    /// Entry point. Returns the process exit code (see <see cref="ExitCodes"/>).
    /// STAThread is required for OLE clipboard/drag-drop on Windows.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        // Shell (un)registration runs headless, before any UI spins up.
        if (ShellIntegration.TryHandleCliFlags(args) is int shellExitCode)
        {
            return shellExitCode;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
