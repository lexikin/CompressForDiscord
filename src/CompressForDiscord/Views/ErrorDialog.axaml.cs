using Avalonia.Controls;
using Avalonia.Interactivity;
using CompressForDiscord.ViewModels;

namespace CompressForDiscord.Views;

public partial class ErrorDialog : Window
{
    public ErrorDialog() => InitializeComponent();

    private async void OnCopyDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ErrorDialogViewModel { Details: { } details } && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(details);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
