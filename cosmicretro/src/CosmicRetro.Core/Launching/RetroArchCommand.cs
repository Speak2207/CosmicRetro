namespace CosmicRetro.Core.Launching;

public sealed record RetroArchCommand(string ExecutablePath, string CorePath, string RomPath)
{
    public IReadOnlyList<string> Arguments =>
    [
        "-L",
        Path.GetFullPath(CorePath),
        Path.GetFullPath(RomPath)
    ];

    public string WorkingDirectory => Path.GetDirectoryName(Path.GetFullPath(ExecutablePath))
        ?? throw new InvalidOperationException("Não foi possível determinar a pasta do RetroArch.");
}
