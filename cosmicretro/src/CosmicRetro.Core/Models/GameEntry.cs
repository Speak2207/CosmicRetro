namespace CosmicRetro.Core.Models;

public sealed record GameEntry(
    string Path,
    string Name,
    string PlatformId,
    string? CoverPath,
    bool IsFavorite,
    DateTimeOffset ImportedUtc,
    DateTimeOffset? LastPlayedUtc,
    string? OriginalName = null,
    string? Region = null,
    string? Version = null,
    string? Description = null,
    string? CompatibilityWarning = null);
