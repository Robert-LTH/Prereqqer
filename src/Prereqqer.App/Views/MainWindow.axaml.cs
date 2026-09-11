using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Prereqqer.App.Resources;
using Prereqqer.App.ViewModels;

namespace Prereqqer.App.Views;

public partial class MainWindow : Window
{
    private readonly DateTimeOffset _startupShortcutExpiresAt = DateTimeOffset.UtcNow.AddSeconds(10);
    private DispatcherTimer? _startupShortcutTimer;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnStartupKeyDown, RoutingStrategies.Tunnel);
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Focus();

        if (DataContext is MainViewModel { IsDesignMode: true })
        {
            StopStartupDesignShortcut();
            return;
        }

        _startupShortcutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _startupShortcutTimer.Tick += (_, _) => StopStartupDesignShortcut();
        _startupShortcutTimer.Start();
    }

    private void OnStartupKeyDown(object? sender, KeyEventArgs e)
    {
        if (DateTimeOffset.UtcNow > _startupShortcutExpiresAt)
        {
            StopStartupDesignShortcut();
            return;
        }

        if (e.Key == Key.A
            && e.KeyModifiers.HasFlag(KeyModifiers.Meta)
            && DataContext is MainViewModel viewModel)
        {
            viewModel.EnableDesignMode(Strings.DesignModeEnabled);
            StopStartupDesignShortcut();
            e.Handled = true;
        }
    }

    private void StopStartupDesignShortcut()
    {
        RemoveHandler(KeyDownEvent, OnStartupKeyDown);

        if (_startupShortcutTimer is not null)
        {
            _startupShortcutTimer.Stop();
            _startupShortcutTimer = null;
        }
    }

    private async void ImportDefinitionsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.ImportDefinitionsTitle,
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });

        var file = files.FirstOrDefault();
        if (file?.Path.LocalPath is { Length: > 0 } path)
        {
            await viewModel.ImportDefinitionsAsync(path);
        }
    }

    private async void ExportDefinitionsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || StorageProvider is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.ExportDefinitionsTitle,
            SuggestedFileName = "prereqqer-conditions.json",
            FileTypeChoices = [JsonFileType]
        });

        if (file?.Path.LocalPath is { Length: > 0 } path)
        {
            await viewModel.ExportDefinitionsAsync(path);
        }
    }

    private async void SaveResultsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || StorageProvider is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.SaveResultsTitle,
            SuggestedFileName = $"prereqqer-results-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json",
            FileTypeChoices = [JsonFileType]
        });

        if (file?.Path.LocalPath is { Length: > 0 } path)
        {
            await viewModel.SaveResultsAsync(path);
        }
    }

    private static readonly FilePickerFileType JsonFileType = new(Strings.JsonFiles)
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };
}
