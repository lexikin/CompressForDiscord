using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CompressForDiscord.Models;
using CompressForDiscord.Services;
using CompressForDiscord.Services.Clipboard;
using CompressForDiscord.ViewModels;
using CompressForDiscord.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CompressForDiscord.Infrastructure;

/// <summary>
/// Owns window transitions and the application lifetime. The app runs with
/// ShutdownMode.OnExplicitShutdown — the default OnLastWindowClose would kill the process in
/// the gap between closing the progress window and opening the preview window.
/// </summary>
internal sealed class AppController(
    IClassicDesktopStyleApplicationLifetime desktop,
    IServiceProvider services)
{
    private bool _cliMode;

    public void Start()
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _ = Task.Run(CompressionOrchestrator.SweepStaleJobDirectories);
        ShellIntegration.TrySelfHealRegistrationInBackground();

        string[] files = [.. (desktop.Args ?? []).Where(a => !a.StartsWith('-'))];
        if (files.Length > 0)
        {
            _cliMode = true;
            _ = RunCliAsync(files);
        }
        else
        {
            ShowMainWindow();
        }
    }

    private void ShowMainWindow()
    {
        var viewModel = new MainWindowViewModel(
            services.GetRequiredService<ISettingsService>(),
            services.GetRequiredService<IUpdateChecker>());
        var window = new MainWindow(this) { DataContext = viewModel };
        desktop.MainWindow = window;
        window.Closed += (_, _) =>
        {
            if (!_cliMode)
            {
                desktop.Shutdown(ExitCodes.Success);
            }
        };
        window.Show();
        _ = viewModel.CheckForUpdatesAsync(CancellationToken.None);
    }

    private async Task RunCliAsync(string[] files)
    {
        int exitCode = await RunFileJobAsync(files, owner: null);
        desktop.Shutdown(exitCode);
    }

    /// <summary>
    /// Runs one compression job with UI. Accepts a list so the Win11 shell extension (which
    /// delivers all selected items in one invocation) has a seam; v1 processes the first file.
    /// </summary>
    public async Task<int> RunFileJobAsync(IReadOnlyList<string> files, Window? owner)
    {
        try
        {
            return await RunFileJobCoreAsync(files, owner);
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Cancelled;
        }
        catch (Exception e)
        {
            AppLog.Write("job failed", e);
            await ShowErrorAsync(ErrorDialogViewModel.FromException(e), owner);
            return (e as AppException)?.ExitCode ?? ExitCodes.UnexpectedError;
        }
    }

    private async Task<int> RunFileJobCoreAsync(IReadOnlyList<string> files, Window? owner)
    {
        if (files.Count > 1)
        {
            AppLog.Write($"{files.Count} files passed; processing the first (batching is a future feature).");
        }

        string path = files[0];
        if (!File.Exists(path))
        {
            await ShowErrorAsync(
                new ErrorDialogViewModel("File not found", $"There is no file at:\n{path}", null), owner);
            return ExitCodes.BadArguments;
        }

        long limitBytes = services.GetRequiredService<ISettingsService>().Load().EffectiveLimitBytes;

        using var cts = new CancellationTokenSource();
        var progressVm = new ProgressWindowViewModel(Path.GetFileName(path), cts);
        var progressWindow = new ProgressWindow { DataContext = progressVm };
        if (owner is null)
        {
            progressWindow.Show();
        }
        else
        {
            progressWindow.Show(owner);
        }

        try
        {
            // Progress<T> is constructed on the UI thread → callbacks marshal automatically.
            var progress = new Progress<CompressionProgress>(progressVm.Report);
            var orchestrator = services.GetRequiredService<ICompressionOrchestrator>();

            CompressionResult result = await orchestrator.RunAsync(path, limitBytes, progress, cts.Token);

            var outcome = await CopyToClipboardAsync(result.OutputPath, progressWindow);

            PreviewWindow? previewWindow = null;
            var previewVm = new PreviewWindowViewModel(
                result, limitBytes,
                services.GetRequiredService<IVlcService>(),
                services.GetRequiredService<IThumbnailService>(),
                // ReSharper disable once AccessToModifiedClosure — assigned right below
                () => CopyToClipboardAsync(result.OutputPath, previewWindow),
                services.GetRequiredService<IUpdateChecker>());
            await previewVm.InitializeAsync(CancellationToken.None);

            previewWindow = new PreviewWindow { DataContext = previewVm };
            var closed = new TaskCompletionSource();
            previewWindow.Closed += (_, _) => closed.TrySetResult();

            previewWindow.Show();          // open the preview BEFORE closing progress
            progressWindow.ForceClose();
            previewVm.ShowBannerFor(outcome);
            _ = previewVm.CheckForUpdatesAsync(CancellationToken.None);

            if (_cliMode)
            {
                await closed.Task;         // process lives while the preview is open
            }

            return ExitCodes.Success;
        }
        finally
        {
            progressWindow.ForceClose();
        }
    }

    private async Task<ClipboardOutcome> CopyToClipboardAsync(string path, TopLevel? fallbackSource)
    {
        var service = services.GetRequiredService<IClipboardFileService>();
        var outcome = await service.CopyFileAsync(path);

        if (outcome == ClipboardOutcome.Failed && fallbackSource?.Clipboard is { } clipboard)
        {
            try
            {
                await clipboard.SetTextAsync(UriListBuilder.ToFileUri(path));
                return ClipboardOutcome.TextFallback;
            }
            catch (Exception e)
            {
                AppLog.Write("clipboard text fallback", e);
            }
        }

        return outcome;
    }

    private async Task ShowErrorAsync(ErrorDialogViewModel vm, Window? owner)
    {
        var dialog = new ErrorDialog { DataContext = vm };
        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
            return;
        }

        var closed = new TaskCompletionSource();
        dialog.Closed += (_, _) => closed.TrySetResult();
        dialog.Show();
        await closed.Task;
    }
}
