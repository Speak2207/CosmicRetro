using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CosmicRetro.App.ViewModels;
using CosmicRetro.Core.Models;

namespace CosmicRetro.App.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow() => InitializeComponent();

    private async void OnOpened(object? sender, EventArgs eventArgs) =>
        await ViewModel.InitializeAsync();

    private async void OnSearchKeyUp(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter)
        {
            await ViewModel.RefreshCommand.ExecuteAsync(null);
        }
    }

    private async void OnImportFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Selecione a pasta de jogos"
        });
        if (folders.Count > 0)
        {
            await ViewModel.ImportFolderAsync(folders[0].Path.LocalPath);
        }
    }

    private async void OnImportLibraryRootClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Selecione a pasta principal com as pastas dos sistemas"
        });
        if (folders.Count > 0)
        {
            await ViewModel.ImportLibraryRootAsync(folders[0].Path.LocalPath);
        }
    }

    private async void OnInstallRetroArchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Selecione a pasta onde o RetroArch será instalado"
        });
        if (folders.Count > 0)
        {
            await ViewModel.InstallRetroArchAsync(folders[0].Path.LocalPath);
        }
    }

    private async void OnChooseSystemFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Selecione a pasta system do RetroArch (BIOS autorizadas)"
        });
        if (folders.Count > 0)
        {
            ViewModel.SetRetroArchSystemPath(folders[0].Path.LocalPath);
        }
    }

    private async void OnFindMetadataClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        await ViewModel.FindMetadataAsync();

    private async void OnChooseCoverClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Escolha uma capa",
            FileTypeFilter =
            [
                new FilePickerFileType("Imagens")
                {
                    Patterns = ["*.bmp", "*.jpeg", "*.jpg", "*.png"]
                }
            ]
        });
        if (files.Count > 0)
        {
            await ViewModel.SetCoverAsync(files[0].Path.LocalPath);
        }
    }

    private async void OnChooseRetroArchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Selecione o executável do RetroArch",
            FileTypeFilter =
            [
                new FilePickerFileType("Executável do RetroArch")
                {
                    Patterns = ["retroarch.exe"]
                }
            ]
        });
        if (files.Count > 0)
        {
            ViewModel.SetRetroArchExecutable(files[0].Path.LocalPath);
        }
    }

    private async void OnChooseCoreClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Selecione um core instalado",
            FileTypeFilter =
            [
                new FilePickerFileType("Core do RetroArch")
                {
                    Patterns = ["*.dll"]
                }
            ]
        });
        if (files.Count > 0)
        {
            ViewModel.SetCorePath(files[0].Path.LocalPath);
        }
    }

    private async void OnRemoveRomSourceClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: RomSource source })
        {
            await ViewModel.RemoveRomSourceAsync(source);
        }
    }
}
