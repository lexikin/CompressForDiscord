using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CompressForDiscord.Infrastructure;
using CompressForDiscord.ViewModels;

namespace CompressForDiscord.Views;

public partial class MainWindow : Window
{
    private readonly AppController? _controller;

    // Designer/preview constructor.
    public MainWindow() => InitializeComponent();

    internal MainWindow(AppController controller) : this()
    {
        _controller = controller;
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618 // Data/DataFormats.Files: new DataTransfer API is still settling in 11.3
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
#pragma warning restore CS0618
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_controller is null || DataContext is not MainWindowViewModel vm || vm.IsBusy)
        {
            return;
        }

#pragma warning disable CS0618 // Data.GetFiles: new DataTransfer API is still settling in 11.3
        List<string> paths = e.Data.GetFiles()?
            .Select(item => item.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>()
            .ToList() ?? [];
#pragma warning restore CS0618

        if (paths.Count == 0)
        {
            return;
        }

        vm.IsBusy = true;
        try
        {
            await _controller.RunFileJobAsync(paths, owner: this);
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
