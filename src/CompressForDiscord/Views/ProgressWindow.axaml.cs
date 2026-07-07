using System;
using System.ComponentModel;
using Avalonia.Controls;
using CompressForDiscord.Services.Windows;
using CompressForDiscord.ViewModels;

namespace CompressForDiscord.Views;

public partial class ProgressWindow : Window
{
    private bool _allowClose;
    private ITaskbarProgress? _taskbar;

    public ProgressWindow() => InitializeComponent();

    /// <summary>Closes for real — bypassing the close-means-cancel interception.</summary>
    internal void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Mirror the in-window bar onto the taskbar button's progress overlay (Windows only).
        if (OperatingSystem.IsWindows()
            && DataContext is ProgressWindowViewModel vm
            && TryGetPlatformHandle()?.Handle is { } hwnd)
        {
            _taskbar = new WindowsTaskbarProgress(hwnd);
            vm.PropertyChanged += OnViewModelPropertyChanged;
            ApplyTaskbar(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ProgressWindowViewModel vm && e.PropertyName is
                nameof(ProgressWindowViewModel.Percent) or nameof(ProgressWindowViewModel.IsIndeterminate))
        {
            ApplyTaskbar(vm);
        }
    }

    private void ApplyTaskbar(ProgressWindowViewModel vm)
    {
        if (_taskbar is null)
        {
            return;
        }

        if (vm.IsIndeterminate)
        {
            _taskbar.SetIndeterminate();
        }
        else
        {
            _taskbar.SetValue(vm.Percent);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ProgressWindowViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _taskbar?.Dispose();
        _taskbar = null;
        base.OnClosed(e);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_allowClose)
        {
            // The window X is just another Cancel button; the controller closes us when done.
            e.Cancel = true;
            (DataContext as ProgressWindowViewModel)?.CancelCommand.Execute(null);
        }

        base.OnClosing(e);
    }
}
