using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CompressForDiscord.Infrastructure;

/// <summary>
/// Windows 11 sparse-package (modern context menu) registration. The MSI drops
/// CompressForDiscord.msix next to the exe and calls `--register-shell`; the app also
/// self-heals silently at startup (PowerToys pattern). Everything is a no-op below
/// Win11 22000 and on Linux.
/// </summary>
internal static class ShellIntegration
{
    private const string PackageName = "CompressForDiscord.Shell";

    private static string MsixPath => Path.Combine(AppContext.BaseDirectory, "CompressForDiscord.msix");

    /// <summary>Handles --register-shell/--unregister-shell before any UI; null = not a shell flag.</summary>
    public static int? TryHandleCliFlags(string[] args)
    {
        bool register = Array.Exists(args, a => a == "--register-shell");
        bool unregister = Array.Exists(args, a => a == "--unregister-shell");
        if (!register && !unregister)
        {
            return null;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return ExitCodes.Success; // classic registry verb covers Win10 — nothing to do
        }

        try
        {
            return register ? Register() : Unregister();
        }
        catch (Exception e)
        {
            AppLog.Write("shell registration", e);
            return ExitCodes.UnexpectedError;
        }
    }

    public static void TrySelfHealRegistrationInBackground()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || !File.Exists(MsixPath))
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                if (!IsRegistered())
                {
                    Register();
                }
            }
            catch (Exception e)
            {
                AppLog.Write("shell self-heal", e);
            }
        });
    }

    private static int Register()
    {
        if (!File.Exists(MsixPath))
        {
            AppLog.Write($"--register-shell: no sparse package at {MsixPath}");
            return ExitCodes.UnexpectedError;
        }

        string externalLocation = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        int exit = RunPowerShell(
            $"Add-AppxPackage -Path '{Quote(MsixPath)}' -ExternalLocation '{Quote(externalLocation)}' " +
            "-ForceUpdateFromAnyVersion");
        AppLog.Write($"shell register exit code {exit}");
        return exit == 0 ? ExitCodes.Success : ExitCodes.UnexpectedError;
    }

    private static int Unregister()
    {
        int exit = RunPowerShell(
            $"Get-AppxPackage -Name '{PackageName}' | Remove-AppxPackage");
        AppLog.Write($"shell unregister exit code {exit}");
        return ExitCodes.Success; // best effort — never block an uninstall
    }

    private static bool IsRegistered()
    {
        var psi = BuildPowerShell($"(Get-AppxPackage -Name '{PackageName}').PackageFullName");
        psi.RedirectStandardOutput = true;
        using var process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(15_000);
        return !string.IsNullOrWhiteSpace(output);
    }

    private static int RunPowerShell(string command)
    {
        using var process = Process.Start(BuildPowerShell(command))!;
        process.WaitForExit(60_000);
        return process.HasExited ? process.ExitCode : -1;
    }

    private static ProcessStartInfo BuildPowerShell(string command)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(command);
        return psi;
    }

    private static string Quote(string value) => value.Replace("'", "''");
}
