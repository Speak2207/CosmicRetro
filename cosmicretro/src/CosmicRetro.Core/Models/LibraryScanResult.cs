namespace CosmicRetro.Core.Models;

public sealed record LibraryScanResult(
    IReadOnlyList<ScannedGame> Games,
    IReadOnlyList<RomSource> Sources,
    IReadOnlyList<ImportIssue> Issues);
