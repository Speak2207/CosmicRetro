using CosmicRetro.Core.Models;
using CosmicRetro.Core.Repositories;
using CosmicRetro.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CosmicRetro.Tests;

public sealed class SqliteCosmicRepositoryTests
{
    [Fact]
    public async Task PersistsLibrarySettingsProfileFavoriteAndRealPlayHistory()
    {
        using var directory = new TemporaryDirectory();
        var databasePath = Path.Combine(directory.Path, "catalog.db");
        var romPath = Path.Combine(directory.Path, "Super Jogo.nes");
        var retroArchPath = Path.Combine(directory.Path, "retroarch.exe");
        var corePath = Path.Combine(directory.Path, "nes_core.dll");
        var coverPath = Path.Combine(directory.Path, "capa.png");
        var metadataCoverPath = Path.Combine(directory.Path, "metadados.jpg");
        await File.WriteAllTextAsync(romPath, "test fixture");
        await File.WriteAllTextAsync(coverPath, "test fixture");

        ICosmicRepository repository = new SqliteCosmicRepository(databasePath);
        await repository.InitializeAsync();
        await repository.ImportGamesAsync([new ScannedGame(romPath, "Super Jogo", "nes", "Super Jogo (USA) (Rev 1).nes", "USA", "Rev 1")]);
        await repository.SetFavoriteAsync(romPath, true);
        await repository.SetCoverPathAsync(romPath, coverPath);
        await repository.SaveRomSourceAsync(directory.Path, "nes");
        await repository.SaveSettingsAsync(new AppSettings(retroArchPath, directory.Path, "nes", directory.Path));
        await repository.SaveMetadataAsync(romPath, new GameMetadata(
            "Super Jogo",
            "Descrição",
            new Uri(metadataCoverPath),
            "USA",
            "Rev 1",
            "teste"));
        await repository.SaveProfileAsync(new EmulatorProfile("nes", corePath));
        var sessionId = await repository.StartSessionAsync(romPath);
        await repository.EndSessionAsync(sessionId, 0);

        ICosmicRepository reopenedRepository = new SqliteCosmicRepository(databasePath);
        await reopenedRepository.InitializeAsync();
        var favorites = await reopenedRepository.GetGamesAsync(favoritesOnly: true);
        var history = await reopenedRepository.GetGamesAsync(historyOnly: true);
        var settings = await reopenedRepository.GetSettingsAsync();
        var profile = await reopenedRepository.GetProfileAsync("nes");
        var sources = await reopenedRepository.GetRomSourcesAsync();

        var favorite = Assert.Single(favorites);
        Assert.True(favorite.IsFavorite);
        Assert.Equal("Super Jogo", favorite.Name);
        Assert.Equal(metadataCoverPath, favorite.CoverPath);
        Assert.Equal("USA", favorite.Region);
        Assert.Equal("Rev 1", favorite.Version);
        Assert.Equal("Descrição", favorite.Description);
        Assert.NotNull(Assert.Single(history).LastPlayedUtc);
        Assert.Equal(directory.Path, settings.LastRomFolder);
        Assert.Equal(directory.Path, settings.RetroArchSystemPath);
        Assert.Equal(retroArchPath, settings.RetroArchPath);
        Assert.Equal(corePath, profile?.CorePath);
        Assert.Equal(directory.Path, Assert.Single(sources).FolderPath);
    }

    [Fact]
    public async Task SearchTreatsWildcardCharactersAsLiteralText()
    {
        using var directory = new TemporaryDirectory();
        ICosmicRepository repository = new SqliteCosmicRepository(Path.Combine(directory.Path, "catalog.db"));
        await repository.InitializeAsync();
        await repository.ImportGamesAsync(
        [
            new ScannedGame(Path.Combine(directory.Path, "Jogo 100%.nes"), "Jogo 100%", "nes"),
            new ScannedGame(Path.Combine(directory.Path, "Jogo 100x.nes"), "Jogo 100x", "nes")
        ]);

        var results = await repository.GetGamesAsync(searchText: "100%");

        Assert.Equal("Jogo 100%", Assert.Single(results).Name);
    }

    [Fact]
    public async Task RemovingRomSourceStopsAutomaticMonitoringWithoutDeletingGames()
    {
        using var directory = new TemporaryDirectory();
        ICosmicRepository repository = new SqliteCosmicRepository(Path.Combine(directory.Path, "catalog.db"));
        await repository.InitializeAsync();
        var romPath = Path.Combine(directory.Path, "Jogo.sfc");
        await repository.ImportGamesAsync([new ScannedGame(romPath, "Jogo", "snes")]);
        await repository.SaveRomSourceAsync(directory.Path, "snes");

        await repository.RemoveRomSourceAsync(directory.Path, "snes");

        Assert.Empty(await repository.GetRomSourcesAsync());
        Assert.Single(await repository.GetGamesAsync());
    }

    [Fact]
    public async Task InitializeAddsMetadataColumnsToAnExistingV01Database()
    {
        using var directory = new TemporaryDirectory();
        var databasePath = Path.Combine(directory.Path, "legacy.db");
        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE games (
                    path TEXT NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    platform_id TEXT NOT NULL,
                    cover_path TEXT NULL,
                    is_favorite INTEGER NOT NULL DEFAULT 0,
                    imported_utc TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        ICosmicRepository repository = new SqliteCosmicRepository(databasePath);
        await repository.InitializeAsync();
        await repository.ImportGamesAsync(
            [new ScannedGame(Path.Combine(directory.Path, "Jogo.nes"), "Jogo", "nes", "Jogo (USA).nes", "USA")]);

        var game = Assert.Single(await repository.GetGamesAsync());
        Assert.Equal("USA", game.Region);
        Assert.Equal("Jogo (USA).nes", game.OriginalName);
    }
}
