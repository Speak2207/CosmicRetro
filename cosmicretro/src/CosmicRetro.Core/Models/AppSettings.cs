namespace CosmicRetro.Core.Models;

public sealed record AppSettings(
    string RetroArchPath,
    string? LastRomFolder,
    string LastPlatformId,
    string? RetroArchSystemPath = null);
