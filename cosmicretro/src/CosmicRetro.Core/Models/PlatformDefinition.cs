namespace CosmicRetro.Core.Models;

public sealed record PlatformDefinition(
    string Id,
    string Name,
    IReadOnlySet<string> Extensions,
    IReadOnlySet<string> FolderAliases,
    string CoreFileName);
