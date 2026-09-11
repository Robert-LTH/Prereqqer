using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Prereqqer.App.Services;
using Prereqqer.App.ViewModels;
using Prereqqer.App.Views;

namespace Prereqqer.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(
                    StartupOptions.IsDesignModeRequested,
                    StartupOptions.ConfigurationUrl),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
