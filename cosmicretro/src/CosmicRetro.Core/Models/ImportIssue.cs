namespace CosmicRetro.Core.Models;

public sealed record ImportIssue(string Path, string Message, bool IsBlocking = false);
