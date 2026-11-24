using System.Net;
using System.Text.Json;
using CosmicRetro.Core.Models;
using CosmicRetro.Core.Platforms;
using CosmicRetro.Core.Services;

namespace CosmicRetro.Infrastructure.Metadata;

public sealed class TheGamesDbMetadataProvider : IGameMetadataProvider
{
    private const long MaximumCoverBytes = 12L * 1024 * 1024;
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private static readonly Uri ApiBaseUri = new("https://api.thegamesdb.net/");
    private static readonly Uri CdnBaseUri = new("https://cdn.thegamesdb.net/");

    public async Task<GameMetadata?> FindAndDownloadAsync(
        GameEntry game,
        string apiKey,
        string coverDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(coverDirectory);
        var platform = PlatformCatalog.GetRequired(game.PlatformId);
        var requestUri = new Uri(
            ApiBaseUri,
            $"v1.1/Games/ByGameName?apikey={Uri.EscapeDataString(apiKey)}" +
            $"&name={Uri.EscapeDataString(game.Name)}&include=boxart%2Cplatform&fields=overview%2Crelease_date");

        using var searchResponse = await HttpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (searchResponse.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("TheGamesDB recusou a chave de API ou atingiu o limite de consultas.");
        }

        searchResponse.EnsureSuccessStatusCode();
        await using var responseStream = await searchResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("games", out var games) ||
            games.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Resposta inesperada da API TheGamesDB.");
        }

        var platforms = GetPlatformMap(root);
        var match = games.EnumerateArray().FirstOrDefault(candidate =>
            IsPlatformMatch(candidate, platforms, platform));
        if (match.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var gameId = match.GetProperty("id").ToString();
        var coverUri = GetFrontCoverUri(root, gameId);
        var localCoverPath = coverUri is null
            ? null
            : await DownloadCoverAsync(coverUri, gameId, coverDirectory, cancellationToken);
        var metadataName = match.TryGetProperty("game_title", out var title)
            ? title.GetString()
            : null;
        var description = match.TryGetProperty("overview", out var overview)
            ? overview.GetString()
            : null;

        return new GameMetadata(
            string.IsNullOrWhiteSpace(metadataName) ? game.Name : metadataName.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            localCoverPath is null ? null : new Uri(localCoverPath, UriKind.Absolute),
            game.Region,
            game.Version,
            "TheGamesDB");
    }

    private static IReadOnlyDictionary<string, JsonElement> GetPlatformMap(JsonElement root)
    {
        if (!root.TryGetProperty("include", out var include) ||
            !include.TryGetProperty("platform", out var platformData) ||
            !platformData.TryGetProperty("data", out var platforms) ||
            platforms.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, JsonElement>();
        }

        return platforms.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value,
            StringComparer.Ordinal);
    }

    private static bool IsPlatformMatch(
        JsonElement game,
        IReadOnlyDictionary<string, JsonElement> platforms,
        PlatformDefinition expectedPlatform)
    {
        if (!game.TryGetProperty("platform", out var platformId))
        {
            return false;
        }

        var key = platformId.ToString();
        if (!platforms.TryGetValue(key, out var platformData))
        {
            return false;
        }

        if (platformData.ValueKind == JsonValueKind.Object &&
            platformData.TryGetProperty("data", out var nestedData))
        {
            platformData = nestedData;
        }

        if (!platformData.TryGetProperty("name", out var nameElement))
        {
            return false;
        }

        var candidateName = Normalize(nameElement.GetString() ?? string.Empty);
        return PlatformNames(expectedPlatform.Id).Any(name =>
            candidateName.Equals(Normalize(name), StringComparison.Ordinal) ||
            candidateName.Contains(Normalize(name), StringComparison.Ordinal));
    }

    private static Uri? GetFrontCoverUri(JsonElement root, string gameId)
    {
        if (!root.TryGetProperty("include", out var include) ||
            !include.TryGetProperty("boxart", out var boxart) ||
            !boxart.TryGetProperty("base_url", out var baseUrls) ||
            !baseUrls.TryGetProperty("large", out var baseUrlElement) ||
            !boxart.TryGetProperty("data", out var boxartData) ||
            !boxartData.TryGetProperty(gameId, out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var baseUrl = baseUrlElement.GetString();
        var frontImage = entries.EnumerateArray().FirstOrDefault(entry =>
            entry.TryGetProperty("type", out var type) &&
            type.GetString()?.Equals("boxart", StringComparison.OrdinalIgnoreCase) == true &&
            entry.TryGetProperty("side", out var side) &&
            side.GetString()?.Equals("front", StringComparison.OrdinalIgnoreCase) == true);
        if (frontImage.ValueKind != JsonValueKind.Object ||
            !frontImage.TryGetProperty("filename", out var filenameElement) ||
            string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var filename = filenameElement.GetString();
        if (string.IsNullOrWhiteSpace(filename) ||
            filename.Replace('\\', '/').Split('/').Any(part => part is "." or ".."))
        {
            throw new InvalidDataException("A resposta da API contém caminho de capa inválido.");
        }

        var uri = new Uri(new Uri(baseUrl, UriKind.Absolute), filename);
        return uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals(CdnBaseUri.Host, StringComparison.OrdinalIgnoreCase)
            ? uri
            : throw new InvalidDataException("A URL da capa não pertence ao CDN oficial do TheGamesDB.");
    }

    private static async Task<string> DownloadCoverAsync(
        Uri coverUri,
        string gameId,
        string coverDirectory,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(
            coverUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var extension = Path.GetExtension(coverUri.AbsolutePath).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
        {
            throw new InvalidDataException("Formato de capa não suportado.");
        }

        if (response.Content.Headers.ContentLength is long length && length > MaximumCoverBytes)
        {
            throw new InvalidDataException("A imagem de capa excede 12 MB.");
        }

        Directory.CreateDirectory(coverDirectory);
        var finalPath = Path.GetFullPath(Path.Combine(coverDirectory, $"thegamesdb-{gameId}{extension}"));
        var temporaryPath = finalPath + $".{Guid.NewGuid():N}.part";
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[81920];
            long totalBytes = 0;
            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes > MaximumCoverBytes)
                {
                    throw new InvalidDataException("A imagem de capa excedeu 12 MB.");
                }

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            await output.FlushAsync(cancellationToken);
            File.Move(temporaryPath, finalPath, overwrite: true);
            return finalPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static IEnumerable<string> PlatformNames(string platformId) =>
        platformId switch
        {
            "nes" => ["Nintendo Entertainment System", "NES"],
            "snes" => ["Super Nintendo Entertainment System", "Super Nintendo", "SNES"],
            "megadrive" => ["Sega Genesis", "Sega Mega Drive", "Mega Drive"],
            "arcade" => ["Arcade", "MAME"],
            "neogeo" => ["Neo Geo", "Neo-Geo"],
            _ => [PlatformCatalog.GetRequired(platformId).Name]
        };

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
