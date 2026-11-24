using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CosmicRetro.App.ViewModels;
using CosmicRetro.App.Views;
using CosmicRetro.Core.Launching;
using CosmicRetro.Infrastructure.Launching;
using CosmicRetro.Infrastructure.Logging;
using CosmicRetro.Infrastructure.Persistence;
using CosmicRetro.Infrastructure.Emulation;
using CosmicRetro.Infrastructure.Metadata;

namespace CosmicRetro.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CosmicRetro");
            Directory.CreateDirectory(dataDirectory);

            var repository = new SqliteCosmicRepository(Path.Combine(dataDirectory, "cosmicretro.db"));
            var logger = new FileAppLogger(Path.Combine(dataDirectory, "logs", "cosmicretro.log"));
            IGameLauncher launcher = new RetroArchProcessLauncher();
            var viewModel = new MainWindowViewModel(
                repository,
                launcher,
                logger,
                new LibretroOfficialSetupService(),
                new TheGamesDbMetadataProvider());
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
