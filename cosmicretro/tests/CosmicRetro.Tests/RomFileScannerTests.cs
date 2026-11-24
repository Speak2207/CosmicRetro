using CosmicRetro.Core.Scanning;
using System.IO.Compression;

namespace CosmicRetro.Tests;

public sealed class RomFileScannerTests
{
    [Fact]
    public void Scan_ImportsOnlyKnownExtensionsForSelectedSystem()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "Jogo.NES"), "test fixture");
        var nestedFolder = Directory.CreateDirectory(Path.Combine(directory.Path, "subpasta"));
        File.WriteAllText(Path.Combine(nestedFolder.FullName, "Outro.nes"), "test fixture");
        File.WriteAllText(Path.Combine(directory.Path, "anotacoes.txt"), "not a game");
        File.WriteAllText(Path.Combine(directory.Path, "outro.sfc"), "different system");

        var result = new RomFileScanner().Scan(directory.Path, "nes");

        Assert.Equal(2, result.Count);
        Assert.Equal("Jogo", result[0].Name);
        Assert.Equal("Outro", result[1].Name);
        Assert.Equal("nes", result[0].PlatformId);
    }

    [Fact]
    public void Scan_ReportsMissingFolderInsteadOfReturningAnEmptyLibrary()
    {
        using var directory = new TemporaryDirectory();
        var missingFolder = Path.Combine(directory.Path, "inexistente");

        Assert.Throws<DirectoryNotFoundException>(() =>
            new RomFileScanner().Scan(missingFolder, "nes"));
    }

    [Fact]
    public void ScanLibraryRoot_DetectsSystemFoldersAndPreservesArcadeArchives()
    {
        using var directory = new TemporaryDirectory();
        var nesFolder = Directory.CreateDirectory(Path.Combine(directory.Path, "NES"));
        var snesFolder = Directory.CreateDirectory(Path.Combine(directory.Path, "SNES", "Coleção"));
        var arcadeFolder = Directory.CreateDirectory(Path.Combine(directory.Path, "NEOGEO"));
        var segaFolder = Directory.CreateDirectory(Path.Combine(directory.Path, "SEGA"));
        File.WriteAllText(Path.Combine(nesFolder.FullName, "Jogo (USA) (Rev 1).nes"), "fixture");
        File.WriteAllText(Path.Combine(snesFolder.FullName, "Outro.sfc"), "fixture");
        File.WriteAllText(Path.Combine(segaFolder.FullName, "Mega Jogo.md"), "fixture");
        var arcadeZip = Path.Combine(arcadeFolder.FullName, "arcade.zip");
        using (var archive = ZipFile.Open(arcadeZip, ZipArchiveMode.Create))
        {
            using var entry = new StreamWriter(archive.CreateEntry("rom.bin").Open());
            entry.Write("fixture");
        }

        var result = new RomFileScanner().ScanLibraryRoot(directory.Path);

        Assert.Equal(4, result.Games.Count);
        Assert.Contains(result.Sources, source => source.PlatformId == "nes");
        Assert.Contains(result.Sources, source => source.PlatformId == "snes");
        Assert.Contains(result.Sources, source => source.PlatformId == "neogeo");
        Assert.Contains(result.Sources, source => source.PlatformId == "megadrive");
        var nesGame = Assert.Single(result.Games, game => game.PlatformId == "nes");
        Assert.Equal("Jogo", nesGame.Name);
        Assert.Equal("USA", nesGame.Region);
        Assert.Equal("Rev 1", nesGame.Version);
        Assert.Equal(arcadeZip, result.Games.Single(game => game.PlatformId == "neogeo").Path);
    }

    [Fact]
    public void ScanLibraryRoot_ReportsUnknownFormatsAndBadZipsWithoutDeletingFiles()
    {
        using var directory = new TemporaryDirectory();
        var nesFolder = Directory.CreateDirectory(Path.Combine(directory.Path, "NES"));
        var unknownZip = Path.Combine(nesFolder.FullName, "document.zip");
        using (var archive = ZipFile.Open(unknownZip, ZipArchiveMode.Create))
        {
            using var entry = new StreamWriter(archive.CreateEntry("readme.txt").Open());
            entry.Write("not a ROM");
        }

        var invalidZip = Path.Combine(nesFolder.FullName, "broken.zip");
        File.WriteAllText(invalidZip, "not a zip");
        File.WriteAllText(Path.Combine(nesFolder.FullName, "notes.txt"), "not a game");

        var result = new RomFileScanner().ScanLibraryRoot(directory.Path);

        Assert.Equal(2, result.Games.Count);
        Assert.All(result.Games, game => Assert.NotNull(game.CompatibilityWarning));
        Assert.Equal(3, result.Issues.Count);
        Assert.True(File.Exists(unknownZip));
        Assert.True(File.Exists(invalidZip));
    }

    [Fact]
    public void ScanLibraryRoot_DoesNotGuessPlatformForAmbiguousRootZip()
    {
        using var directory = new TemporaryDirectory();
        var zipPath = Path.Combine(directory.Path, "collection.zip");
        using (ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
        }

        var result = new RomFileScanner().ScanLibraryRoot(directory.Path);

        Assert.Empty(result.Games);
        Assert.Empty(result.Sources);
    }
}
