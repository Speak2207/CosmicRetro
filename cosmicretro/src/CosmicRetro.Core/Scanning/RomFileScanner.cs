using CosmicRetro.Core.Models;
using CosmicRetro.Core.Platforms;
using System.IO.Compression;

namespace CosmicRetro.Core.Scanning;

public sealed class RomFileScanner
{
    private const int MaximumZipEntriesToInspect = 4096;

    public IReadOnlyList<ScannedGame> Scan(string folderPath, string platformId)
    {
        var result = ScanPlatformFolder(folderPath, PlatformCatalog.GetRequired(platformId));
        return result.Games;
    }

    public LibraryScanResult ScanLibraryRoot(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var fullRootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRootPath))
        {
            throw new DirectoryNotFoundException($"A pasta selecionada não existe: {fullRootPath}");
        }

        var games = new List<ScannedGame>();
        var sources = new List<RomSource>();
        var issues = new List<ImportIssue>();
        foreach (var directory in Directory.EnumerateDirectories(fullRootPath))
        {
            if (!PlatformCatalog.TryGetByFolderName(Path.GetFileName(directory), out var platform) || platform is null)
            {
                continue;
            }

            sources.Add(new RomSource(Path.GetFullPath(directory), platform.Id));
            var result = ScanPlatformFolder(directory, platform);
            games.AddRange(result.Games);
            issues.AddRange(result.Issues);
        }

        var rootFiles = Directory.EnumerateFiles(fullRootPath).ToArray();
        foreach (var file in rootFiles)
        {
            if (!TryFindUnambiguousPlatform(Path.GetExtension(file), out _))
            {
                issues.Add(new ImportIssue(file, "Não foi possível identificar automaticamente o sistema deste arquivo."));
            }
        }

        var rootPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in rootFiles)
        {
            if (TryFindUnambiguousPlatform(Path.GetExtension(file), out var platform) && platform is not null)
            {
                rootPlatforms.Add(platform.Id);
            }
        }

        foreach (var platform in PlatformCatalog.Platforms.Where(item => rootPlatforms.Contains(item.Id)))
        {
            sources.Add(new RomSource(fullRootPath, platform.Id));
            var result = ScanPlatformFolder(fullRootPath, platform, includeNestedFiles: false);
            games.AddRange(result.Games);
            issues.AddRange(result.Issues);
        }

        if (sources.Count == 0)
        {
            issues.Add(new ImportIssue(fullRootPath, "Nenhuma pasta de sistema reconhecida ou ROM identificável foi encontrada."));
        }

        var distinctGames = games
            .GroupBy(game => game.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new LibraryScanResult(
            distinctGames,
            sources.DistinctBy(source => $"{source.FolderPath}|{source.PlatformId}", StringComparer.OrdinalIgnoreCase).ToArray(),
            issues);
    }

    private static (IReadOnlyList<ScannedGame> Games, IReadOnlyList<ImportIssue> Issues) ScanPlatformFolder(
        string folderPath,
        PlatformDefinition platform,
        bool includeNestedFiles = true)
    {
        var fullFolderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullFolderPath))
        {
            throw new DirectoryNotFoundException($"A pasta selecionada não existe: {fullFolderPath}");
        }

        var games = new List<ScannedGame>();
        var issues = new List<ImportIssue>();
        var files = Directory.EnumerateFiles(fullFolderPath, "*", SearchOptions(includeNestedFiles));
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (!platform.Extensions.Contains(extension))
            {
                issues.Add(new ImportIssue(
                    file,
                    $"Formato incompatível com {platform.Name}: {(string.IsNullOrEmpty(extension) ? "(sem extensão)" : extension)}"));
                continue;
            }

            if (platform.Id == "neogeo" &&
                Path.GetFileName(file).Equals("neogeo.zip", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ImportIssue(file, "Arquivo de BIOS Neo Geo detectado; não foi importado como jogo."));
                continue;
            }

            var warning = extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                ? ValidateZip(file, platform, issues)
                : null;
            var parsedName = RomNameParser.Parse(Path.GetFileName(file));
            games.Add(new ScannedGame(
                Path.GetFullPath(file),
                parsedName.DisplayName,
                platform.Id,
                Path.GetFileName(file),
                EmptyToNull(parsedName.Region),
                EmptyToNull(parsedName.Version),
                warning));
        }

        return (games.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), issues);
    }

    private static string? ValidateZip(string archivePath, PlatformDefinition platform, ICollection<ImportIssue> issues)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > MaximumZipEntriesToInspect)
            {
                const string warning = "ZIP contém quantidade excessiva de arquivos e não foi verificado.";
                issues.Add(new ImportIssue(archivePath, warning));
                return warning;
            }

            if (archive.Entries.Count == 0)
            {
                const string warning = "ZIP vazio; o arquivo original foi mantido.";
                issues.Add(new ImportIssue(archivePath, warning));
                return warning;
            }

            var matchingEntry = platform.Id is "arcade" or "neogeo" ||
                archive.Entries.Any(entry =>
                    !string.IsNullOrEmpty(Path.GetExtension(entry.Name)) &&
                    !Path.GetExtension(entry.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                    platform.Extensions.Contains(Path.GetExtension(entry.Name)));
            if (!matchingEntry)
            {
                const string warning = "ZIP não contém uma extensão de ROM reconhecida para este sistema; arquivo original mantido.";
                issues.Add(new ImportIssue(archivePath, warning));
                return warning;
            }

            return null;
        }
        catch (InvalidDataException exception)
        {
            var warning = $"ZIP inválido ou danificado; arquivo original mantido ({exception.Message}).";
            issues.Add(new ImportIssue(archivePath, warning));
            return warning;
        }
    }

    private static EnumerationOptions SearchOptions(bool recurse = true) =>
        new()
        {
            RecurseSubdirectories = recurse,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

    private static bool TryFindUnambiguousPlatform(string extension, out PlatformDefinition platform)
    {
        var matches = PlatformCatalog.Platforms
            .Where(candidate => candidate.Extensions.Contains(extension))
            .ToArray();
        if (matches.Length == 1)
        {
            platform = matches[0];
            return true;
        }

        platform = null!;
        return false;
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
