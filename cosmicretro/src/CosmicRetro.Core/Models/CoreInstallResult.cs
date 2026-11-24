namespace CosmicRetro.Core.Models;

public sealed record CoreInstallResult(
    string PlatformId,
    string CoreFileName,
    bool Installed,
    string? Message);
