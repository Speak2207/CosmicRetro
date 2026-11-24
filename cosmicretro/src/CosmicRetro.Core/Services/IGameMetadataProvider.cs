using CosmicRetro.Core.Models;

namespace CosmicRetro.Core.Services;

public interface IGameMetadataProvider
{
    Task<GameMetadata?> FindAndDownloadAsync(
        GameEntry game,
        string apiKey,
        string coverDirectory,
        CancellationToken cancellationToken = default);
}
