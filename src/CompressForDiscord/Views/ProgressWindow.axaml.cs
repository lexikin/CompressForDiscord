using Avalonia.Controls;
using CompressForDiscord.ViewModels;

namespace CompressForDiscord.Views;

public partial class ProgressWindow : Window
{
    private bool _allowClose;

    public ProgressWindow() => InitializeComponent();

    /// <summary>Closes for real — bypassing the close-means-cancel interception.</summary>
    internal void ForceClose()
    {
        _allowClose = true;
        Close();
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
