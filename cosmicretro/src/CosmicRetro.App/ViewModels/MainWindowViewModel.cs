using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicRetro.Core.Launching;
using CosmicRetro.Core.Models;
using CosmicRetro.Core.Platforms;
using CosmicRetro.Core.Repositories;
using CosmicRetro.Core.Scanning;
using CosmicRetro.Core.Services;
using CosmicRetro.Infrastructure.Emulation;
using CosmicRetro.Infrastructure.Logging;
using CosmicRetro.Infrastructure.Metadata;

namespace CosmicRetro.App.ViewModels;

public sealed record PlatformFilter(string? Id, string Name);

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ICosmicRepository _repository;
    private readonly IGameLauncher _launcher;
    private readonly FileAppLogger _logger;
    private readonly IRetroArchSetupService _retroArchSetup;
    private readonly IGameMetadataProvider _metadataProvider;
    private readonly RomFileScanner _scanner = new();
    private bool _initialized;
    private string? _lastRomFolder;

    public MainWindowViewModel(
        ICosmicRepository repository,
        IGameLauncher launcher,
        FileAppLogger logger,
        IRetroArchSetupService retroArchSetup,
        IGameMetadataProvider metadataProvider)
    {
        _repository = repository;
        _launcher = launcher;
        _logger = logger;
        _retroArchSetup = retroArchSetup;
        _metadataProvider = metadataProvider;
        Platforms = PlatformCatalog.Platforms;
        PlatformFilters =
        [
            new PlatformFilter(null, "Todos os sistemas"),
            .. Platforms.Select(platform => new PlatformFilter(platform.Id, platform.Name))
        ];
        SelectedPlatform = Platforms[0];
        SelectedProfilePlatform = Platforms[0];
        SelectedPlatformFilter = PlatformFilters[0];
    }

    private async Task RefreshCoreAsync()
    {
        var selectedPath = SelectedGame?.Path;
        var games = await _repository.GetGamesAsync(
            SelectedPlatformFilter?.Id,
            SearchText,
            CurrentPage == "Favorites",
            CurrentPage == "History");
        ReplaceItems(Games, games);
        SelectedGame = selectedPath is null
            ? null
            : Games.FirstOrDefault(game => game.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
        ReplaceItems(RomSources, await _repository.GetRomSourcesAsync());

        var allGames = await _repository.GetGamesAsync();
        TotalGames = allGames.Count;
        FavoriteCount = allGames.Count(game => game.IsFavorite);

        var history = await _repository.GetGamesAsync(historyOnly: true);
        HistoryCount = history.Count;
        ReplaceItems(RecentGames, history.Take(8));
        StatusMessage = $"{TotalGames} jogo(s) na biblioteca.";
    }

    public IReadOnlyList<PlatformDefinition> Platforms { get; }
    public IReadOnlyList<PlatformFilter> PlatformFilters { get; }
    public ObservableCollection<GameEntry> Games { get; } = [];
    public ObservableCollection<GameEntry> RecentGames { get; } = [];
    public ObservableCollection<RomSource> RomSources { get; } = [];

    [ObservableProperty]
    private string currentPage = "Home";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private PlatformDefinition selectedPlatform;

    [ObservableProperty]
    private PlatformDefinition selectedProfilePlatform;

    [ObservableProperty]
    private PlatformFilter selectedPlatformFilter;

    [ObservableProperty]
    private GameEntry? selectedGame;

    [ObservableProperty]
    private RomSource? selectedRomSource;

    [ObservableProperty]
    private string retroArchPath = string.Empty;

    [ObservableProperty]
    private string corePath = string.Empty;

    [ObservableProperty]
    private string retroArchSystemPath = string.Empty;

    [ObservableProperty]
    private string metadataApiKey = string.Empty;

    [ObservableProperty]
    private string importIssuesText = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Iniciando o Cosmic Retro...";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int totalGames;

    [ObservableProperty]
    private int favoriteCount;

    [ObservableProperty]
    private int historyCount;

    public bool IsHome => CurrentPage == "Home";
    public bool IsGamePage => CurrentPage is "Library" or "Favorites" or "History";
    public bool IsSettings => CurrentPage == "Settings";
    public bool HasHistory => HistoryCount > 0;
    public bool HasNoHistory => !HasHistory;
    public bool HasSelectedGame => SelectedGame is not null;
    public bool HasImportIssues => !string.IsNullOrWhiteSpace(ImportIssuesText);
    public string PageTitle => CurrentPage switch
    {
        "Library" => "Biblioteca",
        "Favorites" => "Favoritos",
        "History" => "Histórico",
        "Settings" => "Configurações",
        _ => "Início"
    };

    public string FavoriteButtonText => SelectedGame?.IsFavorite == true
        ? "Remover dos favoritos"
        : "Adicionar aos favoritos";

    partial void OnCurrentPageChanged(string value)
    {
        OnPropertyChanged(nameof(IsHome));
        OnPropertyChanged(nameof(IsGamePage));
        OnPropertyChanged(nameof(IsSettings));
        OnPropertyChanged(nameof(PageTitle));
    }

    partial void OnSelectedGameChanged(GameEntry? value)
    {
        OnPropertyChanged(nameof(FavoriteButtonText));
        OnPropertyChanged(nameof(HasSelectedGame));
    }

    partial void OnImportIssuesTextChanged(string value) =>
        OnPropertyChanged(nameof(HasImportIssues));

    partial void OnHistoryCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(HasNoHistory));
    }

    partial void OnSelectedProfilePlatformChanged(PlatformDefinition value) =>
        _ = LoadProfileAsync(value);

    [RelayCommand]
    private async Task NavigateAsync(string page)
    {
        CurrentPage = page;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_initialized)
        {
            return;
        }

        await RunSafelyAsync("Atualizar biblioteca", RefreshCoreAsync);
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        await RunSafelyAsync("Salvar configuração do emulador", async () =>
        {
            if (!File.Exists(RetroArchPath))
            {
                throw new FileNotFoundException("Selecione um executável existente do RetroArch.", RetroArchPath);
            }

            if (!Path.GetFileName(RetroArchPath).Equals("retroarch.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("O executável selecionado deve se chamar retroarch.exe.");
            }

            if (!File.Exists(CorePath))
            {
                throw new FileNotFoundException("Selecione um core instalado do RetroArch.", CorePath);
            }

            if (!Path.GetExtension(CorePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("O core selecionado deve ser um arquivo .dll.");
            }

            await _repository.SaveSettingsAsync(new AppSettings(
                Path.GetFullPath(RetroArchPath),
                _lastRomFolder,
                SelectedProfilePlatform.Id,
                RetroArchSystemPath));
            await _repository.SaveProfileAsync(new EmulatorProfile(
                SelectedProfilePlatform.Id,
                Path.GetFullPath(CorePath)));
            StatusMessage = $"Configuração salva para {SelectedProfilePlatform.Name}.";
        });
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Selecione um jogo primeiro.";
            return;
        }

        var game = SelectedGame;
        await RunSafelyAsync("Atualizar favorito", async () =>
        {
            await _repository.SetFavoriteAsync(game.Path, !game.IsFavorite);
            await RefreshCoreAsync();
        });
    }

    [RelayCommand]
    private async Task LaunchSelectedAsync()
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Selecione um jogo para executar.";
            return;
        }

        var game = SelectedGame;
        await RunSafelyAsync("Executar jogo", async () =>
        {
            if (string.IsNullOrWhiteSpace(RetroArchPath))
            {
                throw new InvalidOperationException("Configure o RetroArch antes de executar um jogo.");
            }

            var profile = await _repository.GetProfileAsync(game.PlatformId)
                ?? throw new InvalidOperationException(
                    $"Configure um core do RetroArch para {PlatformCatalog.GetRequired(game.PlatformId).Name}.");

            if (!File.Exists(RetroArchPath))
            {
                throw new FileNotFoundException("O RetroArch configurado não foi encontrado.", RetroArchPath);
            }

            if (!File.Exists(profile.CorePath))
            {
                throw new FileNotFoundException("O core configurado não foi encontrado.", profile.CorePath);
            }

            if (!File.Exists(game.Path))
            {
                throw new FileNotFoundException("O arquivo do jogo não foi encontrado.", game.Path);
            }

            var sessionId = await _repository.StartSessionAsync(game.Path);
            int? exitCode = null;
            try
            {
                exitCode = await _launcher.LaunchAsync(
                    new RetroArchCommand(RetroArchPath, profile.CorePath, game.Path));
                StatusMessage = exitCode == 0
                    ? $"Jogo encerrado: {game.Name}."
                    : $"O emulador encerrou com código {exitCode}: {game.Name}.";
            }
            finally
            {
                await _repository.EndSessionAsync(sessionId, exitCode);
            }

            await RefreshAsync();
        });
    }

    public async Task InitializeAsync()
    {
        await RunSafelyAsync("Inicializar aplicativo", async () =>
        {
            await _repository.InitializeAsync();
            var settings = await _repository.GetSettingsAsync();
            RetroArchPath = settings.RetroArchPath;
            _lastRomFolder = settings.LastRomFolder;
            RetroArchSystemPath = settings.RetroArchSystemPath ?? string.Empty;
            if (PlatformCatalog.Platforms.FirstOrDefault(platform => platform.Id == settings.LastPlatformId) is { } platform)
            {
                SelectedPlatform = platform;
                SelectedProfilePlatform = platform;
            }

            _initialized = true;
            var missingSources = new List<string>();
            var failedSources = 0;
            var sources = await _repository.GetRomSourcesAsync();
            ReplaceItems(RomSources, sources);
            foreach (var source in sources)
            {
                if (!Directory.Exists(source.FolderPath))
                {
                    missingSources.Add(source.FolderPath);
                    continue;
                }

                try
                {
                    var imported = await Task.Run(() => _scanner.Scan(source.FolderPath, source.PlatformId));
                    await _repository.ImportGamesAsync(imported);
                }
                catch (Exception exception)
                {
                    _logger.LogError($"Reindexar fonte {source.FolderPath}", exception);
                    failedSources++;
                }
            }

            await LoadProfileAsync(SelectedProfilePlatform);
            await RefreshCoreAsync();
            StatusMessage = failedSources > 0
                ? $"Não foi possível reindexar {failedSources} fonte(s). Consulte o log."
                : missingSources.Count > 0
                    ? $"Pastas não disponíveis: {string.Join("; ", missingSources)}"
                    : TotalGames == 0
                        ? "Biblioteca vazia. Importe uma pasta de jogos para começar."
                        : $"{TotalGames} jogo(s) carregado(s).";
        });
    }

    public async Task ImportFolderAsync(string folderPath)
    {
        if (IsBusy)
        {
            StatusMessage = "Aguarde a importação atual terminar.";
            return;
        }

        IsBusy = true;
        try
        {
            await RunSafelyAsync("Importar pasta", async () =>
            {
                var platform = SelectedPlatform;
                var scannedGames = await Task.Run(() => _scanner.Scan(folderPath, platform.Id));
                await _repository.SaveRomSourceAsync(folderPath, platform.Id);
                if (scannedGames.Count == 0)
                {
                    await RefreshCoreAsync();
                    StatusMessage = $"Pasta monitorada. Nenhum arquivo compatível com {platform.Name} foi encontrado.";
                    return;
                }

                await _repository.ImportGamesAsync(scannedGames);
                _lastRomFolder = Path.GetFullPath(folderPath);
                await _repository.SaveSettingsAsync(new AppSettings(
                    RetroArchPath,
                    _lastRomFolder,
                    platform.Id,
                    RetroArchSystemPath));
                await RefreshCoreAsync();
                StatusMessage = $"{scannedGames.Count} arquivo(s) indexado(s) em {platform.Name}.";
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ImportLibraryRootAsync(string rootPath)
    {
        if (IsBusy)
        {
            StatusMessage = "Aguarde a importação atual terminar.";
            return;
        }

        IsBusy = true;
        try
        {
            await RunSafelyAsync("Importar biblioteca", async () =>
            {
                var result = await Task.Run(() => _scanner.ScanLibraryRoot(rootPath));
                foreach (var source in result.Sources)
                {
                    await _repository.SaveRomSourceAsync(source.FolderPath, source.PlatformId);
                }

                await _repository.ImportGamesAsync(result.Games);
                _lastRomFolder = Path.GetFullPath(rootPath);
                await _repository.SaveSettingsAsync(new AppSettings(
                    RetroArchPath,
                    _lastRomFolder,
                    SelectedPlatform.Id,
                    RetroArchSystemPath));

                var issues = result.Issues.Select(issue => $"{issue.Path}: {issue.Message}").ToList();
                if (result.Games.Any(game => game.PlatformId == "neogeo") &&
                    (string.IsNullOrWhiteSpace(RetroArchSystemPath) ||
                     !File.Exists(Path.Combine(RetroArchSystemPath, "neogeo.zip"))))
                {
                    issues.Add("BIOS Neo Geo ausente na pasta de sistema configurada (neogeo.zip). A BIOS não é fornecida pelo Cosmic Retro.");
                }

                ImportIssuesText = string.Join(Environment.NewLine, issues.Take(100));
                await RefreshCoreAsync();
                StatusMessage = $"Importação concluída: {result.Games.Count} jogo(s), {result.Sources.Count} fonte(s), {issues.Count} aviso(s).";
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InstallRetroArchAsync(string installationDirectory)
    {
        if (IsBusy)
        {
            StatusMessage = "Aguarde a operação atual terminar.";
            return;
        }

        IsBusy = true;
        try
        {
            await RunSafelyAsync("Instalar RetroArch e cores", async () =>
            {
                RetroArchPath = await _retroArchSetup.InstallRetroArchAsync(installationDirectory);
                var retroArchDirectory = Path.GetDirectoryName(RetroArchPath)
                    ?? throw new InvalidOperationException("Não foi possível determinar a pasta do RetroArch.");
                RetroArchSystemPath = Path.Combine(retroArchDirectory, "system");
                Directory.CreateDirectory(RetroArchSystemPath);
                var results = await _retroArchSetup.InstallRecommendedCoresAsync(retroArchDirectory);
                foreach (var result in results.Where(item => item.Installed))
                {
                    await _repository.SaveProfileAsync(new EmulatorProfile(
                        result.PlatformId,
                        Path.Combine(retroArchDirectory, "cores", result.CoreFileName)));
                }

                await _repository.SaveSettingsAsync(new AppSettings(
                    RetroArchPath,
                    _lastRomFolder,
                    SelectedPlatform.Id,
                    RetroArchSystemPath));
                var failed = results.Where(item => !item.Installed).ToArray();
                StatusMessage = failed.Length == 0
                    ? $"RetroArch e {results.Count} configuração(ões) de core concluídos. BIOS não incluídas."
                    : $"RetroArch instalado; {failed.Length} core(s) falharam: {string.Join("; ", failed.Select(item => $"{item.PlatformId}: {item.Message}"))}";
                await LoadProfileAsync(SelectedProfilePlatform);
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task FindMetadataAsync()
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Selecione um jogo antes de buscar informações.";
            return;
        }

        var game = SelectedGame;
        await RunSafelyAsync("Buscar metadados", async () =>
        {
            var coverDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CosmicRetro",
                "covers");
            var metadata = await _metadataProvider.FindAndDownloadAsync(
                game,
                MetadataApiKey,
                coverDirectory);
            if (metadata is null)
            {
                StatusMessage = "Nenhum resultado correspondente foi encontrado.";
                return;
            }

            await _repository.SaveMetadataAsync(game.Path, metadata);
            await RefreshCoreAsync();
            StatusMessage = $"Informações e capa atualizadas: {game.Name}.";
        });
    }

    public void SetRetroArchSystemPath(string path) =>
        RetroArchSystemPath = Path.GetFullPath(path);

    public async Task RemoveRomSourceAsync(RomSource source)
    {
        await RunSafelyAsync("Remover fonte de jogos", async () =>
        {
            await _repository.RemoveRomSourceAsync(source.FolderPath, source.PlatformId);
            await RefreshCoreAsync();
            StatusMessage = "A pasta deixou de ser reindexada automaticamente. Os jogos já importados foram mantidos.";
        });
    }

    public async Task SetCoverAsync(string coverPath)
    {
        if (SelectedGame is null)
        {
            StatusMessage = "Selecione um jogo antes de escolher uma capa.";
            return;
        }

        var game = SelectedGame;
        await RunSafelyAsync("Salvar capa", async () =>
        {
            var fullPath = Path.GetFullPath(coverPath);
            var extension = Path.GetExtension(fullPath);
            if (!File.Exists(fullPath) ||
                !new[] { ".bmp", ".jpeg", ".jpg", ".png" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Escolha uma imagem BMP, JPEG ou PNG existente.");
            }

            await _repository.SetCoverPathAsync(game.Path, fullPath);
            await RefreshCoreAsync();
            StatusMessage = "Capa personalizada salva.";
        });
    }

    public void SetRetroArchExecutable(string path) => RetroArchPath = Path.GetFullPath(path);
    public void SetCorePath(string path) => CorePath = Path.GetFullPath(path);

    private async Task LoadProfileAsync(PlatformDefinition platform)
    {
        if (!_initialized)
        {
            return;
        }

        await RunSafelyAsync("Carregar perfil", async () =>
        {
            var profile = await _repository.GetProfileAsync(platform.Id);
            CorePath = profile?.CorePath ?? string.Empty;
        });
    }

    private async Task RunSafelyAsync(string context, Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            _logger.LogError(context, exception);
            StatusMessage = $"Erro — {exception.Message}";
        }
    }

    private static void ReplaceItems<T>(
        ObservableCollection<T> destination,
        IEnumerable<T> items)
    {
        destination.Clear();
        foreach (var item in items)
        {
            destination.Add(item);
        }
    }
}
