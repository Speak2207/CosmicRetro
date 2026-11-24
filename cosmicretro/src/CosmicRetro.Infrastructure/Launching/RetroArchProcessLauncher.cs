using System.Diagnostics;
using CosmicRetro.Core.Launching;

namespace CosmicRetro.Infrastructure.Launching;

public sealed class RetroArchProcessLauncher : IGameLauncher
{
    public async Task<int> LaunchAsync(RetroArchCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!File.Exists(command.ExecutablePath))
        {
            throw new FileNotFoundException("O executável do RetroArch configurado não foi encontrado.", command.ExecutablePath);
        }

        if (!File.Exists(command.CorePath))
        {
            throw new FileNotFoundException("O core selecionado não foi encontrado.", command.CorePath);
        }

        if (!File.Exists(command.RomPath))
        {
            throw new FileNotFoundException("O arquivo do jogo não foi encontrado.", command.RomPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(command.ExecutablePath),
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false
        };
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("O Windows não conseguiu iniciar o RetroArch.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
