using CosmicRetro.Core.Launching;

namespace CosmicRetro.Tests;

public sealed class RetroArchCommandTests
{
    [Fact]
    public void ArgumentsKeepExecutableCoreAndRomAsDistinctValuesWithSpaces()
    {
        var command = new RetroArchCommand(
            @"C:\Emuladores\RetroArch\retroarch.exe",
            @"C:\Emuladores\RetroArch\cores\NES core.dll",
            @"D:\Minha Biblioteca\Jogo (BR).nes");

        Assert.Equal(
            [
                "-L",
                Path.GetFullPath(command.CorePath),
                Path.GetFullPath(command.RomPath)
            ],
            command.Arguments);
        Assert.Contains("NES core.dll", command.Arguments[1]);
        Assert.Contains("Jogo (BR).nes", command.Arguments[2]);
    }
}
