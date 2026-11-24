namespace CosmicRetro.Core.Models;

public sealed record GameMetadata(
    string Name,
    string? Description,
    Uri? CoverUri,
    string? Region,
    string? Version,
    string Provider);
