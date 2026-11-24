using CosmicRetro.Core.Models;

namespace CosmicRetro.Core.Repositories;

public interface ICosmicRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameEntry>> GetGamesAsync(
        string? platformId = null,
        string? searchText = null,
        bool favoritesOnly = false,
        bool historyOnly = false,
        CancellationToken cancellationToken = default);
    Task<int> ImportGamesAsync(IEnumerable<ScannedGame> games, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RomSource>> GetRomSourcesAsync(CancellationToken cancellationToken = default);
    Task SaveRomSourceAsync(string folderPath, string platformId, CancellationToken cancellationToken = default);
    Task RemoveRomSourceAsync(string folderPath, string platformId, CancellationToken cancellationToken = default);
    Task SetFavoriteAsync(string gamePath, bool isFavorite, CancellationToken cancellationToken = default);
    Task SetCoverPathAsync(string gamePath, string coverPath, CancellationToken cancellationToken = default);
    Task SaveMetadataAsync(string gamePath, GameMetadata metadata, CancellationToken cancellationToken = default);
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default);
    Task SaveSettingAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<EmulatorProfile?> GetProfileAsync(string platformId, CancellationToken cancellationToken = default);
    Task SaveProfileAsync(EmulatorProfile profile, CancellationToken cancellationToken = default);
    Task<long> StartSessionAsync(string gamePath, CancellationToken cancellationToken = default);
    Task EndSessionAsync(long sessionId, int? exitCode, CancellationToken cancellationToken = default);
}
