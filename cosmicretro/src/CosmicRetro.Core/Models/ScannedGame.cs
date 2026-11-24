namespace CosmicRetro.Core.Models;

public sealed record ScannedGame(
    string Path,
    string Name,
    string PlatformId,
    string? OriginalName = null,
    string? Region = null,
    string? Version = null,
    string? CompatibilityWarning = null);
