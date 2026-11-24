using CosmicRetro.Core.Models;

namespace CosmicRetro.Core.Platforms;

public static class PlatformCatalog
{
    private static readonly IReadOnlyList<PlatformDefinition> AllPlatforms =
    [
        Create("nes", "Nintendo Entertainment System", "mesen_libretro.dll",
            [".nes", ".unf", ".unif", ".zip"], ["nes", "nintendo", "nintendo entertainment system"]),
        Create("snes", "Super Nintendo", "snes9x_libretro.dll",
            [".sfc", ".smc", ".zip"], ["snes", "super nintendo", "super nintendo entertainment system"]),
        Create("megadrive", "Mega Drive", "genesis_plus_gx_libretro.dll",
            [".bin", ".gen", ".md", ".smd", ".zip"], ["sega", "megadrive", "mega drive", "genesis"]),
        Create("gameboy", "Game Boy", "gambatte_libretro.dll",
            [".gb", ".gbc", ".zip"], ["gameboy", "game boy", "gb"]),
        Create("gba", "Game Boy Advance", "mgba_libretro.dll",
            [".gba", ".zip"], ["gba", "game boy advance"]),
        Create("psx", "PlayStation", "swanstation_libretro.dll",
            [".bin", ".chd", ".cue", ".iso", ".m3u", ".pbp"], ["psx", "playstation", "ps1"]),
        Create("arcade", "Fliperamas", "fbneo_libretro.dll",
            [".zip"], ["arcade", "mame", "fliperama", "fliperamas"]),
        Create("neogeo", "Neo Geo", "fbneo_libretro.dll",
            [".zip"], ["neogeo", "neo geo", "neo-geo"])
    ];

    public static IReadOnlyList<PlatformDefinition> Platforms => AllPlatforms;

    public static PlatformDefinition GetRequired(string platformId) =>
        AllPlatforms.FirstOrDefault(platform => platform.Id.Equals(platformId, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Sistema não reconhecido: {platformId}", nameof(platformId));

    public static bool TryGetByFolderName(string folderName, out PlatformDefinition? platform)
    {
        var normalizedName = NormalizeFolderName(folderName);
        platform = AllPlatforms.FirstOrDefault(candidate =>
            candidate.FolderAliases.Any(alias => NormalizeFolderName(alias) == normalizedName));
        return platform is not null;
    }

    private static PlatformDefinition Create(
        string id,
        string name,
        string coreFileName,
        string[] extensions,
        string[] folderAliases) =>
        new(
            id,
            name,
            new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(folderAliases.Select(NormalizeFolderName), StringComparer.OrdinalIgnoreCase),
            coreFileName);

    private static string NormalizeFolderName(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
