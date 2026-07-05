using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CompressForDiscord.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CompressForDiscord;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection().AddAppServices().BuildServiceProvider();
            new AppController(desktop, services).Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
