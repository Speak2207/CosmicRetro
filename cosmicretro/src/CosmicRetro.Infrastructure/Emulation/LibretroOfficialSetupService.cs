using System.IO.Compression;
using CosmicRetro.Core.Models;
using CosmicRetro.Core.Platforms;
using CosmicRetro.Core.Services;
using SharpCompress.Readers;

namespace CosmicRetro.Infrastructure.Emulation;

public sealed class LibretroOfficialSetupService : IRetroArchSetupService
{
    private const string StableRetroArchVersion = "1.22.2";
    private const long MaximumRetroArchArchiveBytes = 512L * 1024 * 1024;
    private const long MaximumCoreArchiveBytes = 128L * 1024 * 1024;
    private const long MaximumCoreBytes = 512L * 1024 * 1024;
    private const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
    private const int MaximumArchiveEntries = 30_000;
    private static readonly Uri OfficialBuildBot = new("https://buildbot.libretro.com/");
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly IReadOnlyList<string> SupportedCorePlatforms =
        ["nes", "snes", "megadrive", "arcade", "neogeo"];

    public async Task<string> InstallRetroArchAsync(
        string installationDirectory,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(installationDirectory);

        var target = Path.GetFullPath(Path.Combine(installationDirectory, StableRetroArchVersion));
        var executable = Path.Combine(target, "retroarch.exe");
        if (File.Exists(executable))
        {
            return executable;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var stagingDirectory = Path.Combine(
            Path.GetDirectoryName(target)!,
            $".{StableRetroArchVersion}-{Guid.NewGuid():N}.staging");
        Directory.CreateDirectory(stagingDirectory);
        var archivePath = Path.Combine(stagingDirectory, "RetroArch.7z");
        var extractedDirectory = Path.Combine(stagingDirectory, "files");

        try
        {
            var source = new Uri(
                OfficialBuildBot,
                $"stable/{StableRetroArchVersion}/windows/x86_64/RetroArch.7z");
            await DownloadOfficialFileAsync(
                source,
                archivePath,
                MaximumRetroArchArchiveBytes,
                cancellationToken);

            Directory.CreateDirectory(extractedDirectory);
            await Task.Run(
                () => Extract7ZipSafely(archivePath, extractedDirectory, cancellationToken),
                cancellationToken);
            var extractedExecutable = Path.Combine(extractedDirectory, "retroarch.exe");
            if (!File.Exists(extractedExecutable))
            {
                throw new InvalidDataException("O pacote oficial não contém retroarch.exe na raiz.");
            }

            if (Directory.Exists(target))
            {
                throw new IOException($"O destino já existe e não contém uma instalação válida: {target}");
            }

            Directory.Move(extractedDirectory, target);
            return Path.Combine(target, "retroarch.exe");
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    public async Task<IReadOnlyList<CoreInstallResult>> InstallRecommendedCoresAsync(
        string retroArchDirectory,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(retroArchDirectory);
        var fullRetroArchDirectory = Path.GetFullPath(retroArchDirectory);
        var coreDirectory = Path.Combine(fullRetroArchDirectory, "cores");
        Directory.CreateDirectory(coreDirectory);

        var results = new List<CoreInstallResult>();
        foreach (var platformId in SupportedCorePlatforms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var platform = PlatformCatalog.GetRequired(platformId);
            var destination = Path.Combine(coreDirectory, platform.CoreFileName);
            if (File.Exists(destination))
            {
                results.Add(new CoreInstallResult(platformId, platform.CoreFileName, true, "Core já instalado."));
                continue;
            }

            var archivePath = Path.Combine(Path.GetTempPath(), $"cosmicretro-{Guid.NewGuid():N}.zip");
            var source = new Uri(
                OfficialBuildBot,
                $"nightly/windows/x86_64/latest/{platform.CoreFileName}.zip");
            try
            {
                await DownloadOfficialFileAsync(source, archivePath, MaximumCoreArchiveBytes, cancellationToken);
                await ExtractCoreZipAsync(archivePath, destination, platform.CoreFileName, cancellationToken);
                results.Add(new CoreInstallResult(platformId, platform.CoreFileName, true, "Instalado da buildbot oficial."));
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException)
            {
                results.Add(new CoreInstallResult(
                    platformId,
                    platform.CoreFileName,
                    false,
                    $"Falha ao instalar o core: {exception.Message}"));
            }
            finally
            {
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }
            }
        }

        return results;
    }

    private static async Task DownloadOfficialFileAsync(
        Uri source,
        string destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (source.Scheme != Uri.UriSchemeHttps ||
            !source.Host.Equals(OfficialBuildBot.Host, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Download recusado: origem não oficial.");
        }

        using var response = await HttpClient.GetAsync(
            source,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.RequestMessage?.RequestUri is not { Scheme: "https" } finalUri ||
            !finalUri.Host.Equals(OfficialBuildBot.Host, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("O servidor oficial redirecionou o download para uma origem não autorizada.");
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 0 and var contentLength &&
            contentLength > maximumBytes)
        {
            throw new InvalidDataException($"Arquivo maior que o limite permitido ({maximumBytes} bytes).");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 64,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[1024 * 64];
        long totalBytes = 0;
        while (true)
        {
            var bytesRead = await input.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > maximumBytes)
            {
                throw new InvalidDataException($"Download excedeu o limite de {maximumBytes} bytes.");
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        await output.FlushAsync(cancellationToken);
        if (totalBytes == 0)
        {
            throw new InvalidDataException("O servidor oficial retornou um arquivo vazio.");
        }
    }

    private static void Extract7ZipSafely(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream);
        var destinationRoot = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;
        long totalExpandedBytes = 0;
        var entryCount = 0;

        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++entryCount > MaximumArchiveEntries)
            {
                throw new InvalidDataException("O arquivo 7z excede o limite de entradas.");
            }

            var entryName = reader.Entry.Key
                ?? throw new InvalidDataException("O arquivo 7z contém uma entrada sem nome.");
            var normalizedName = entryName.Replace('\\', '/');
            if (Path.IsPathRooted(normalizedName) ||
                normalizedName.Split('/').Any(part => part is "." or ".."))
            {
                throw new InvalidDataException("O arquivo 7z contém um caminho de extração inválido.");
            }

            var outputPath = Path.GetFullPath(Path.Combine(
                destinationDirectory,
                normalizedName.Replace('/', Path.DirectorySeparatorChar)));
            if (!outputPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("O arquivo 7z tenta gravar fora da pasta de instalação.");
            }

            if (reader.Entry.IsDirectory)
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            var declaredSize = reader.Entry.Size;
            if (declaredSize < 0 || declaredSize > MaximumExpandedBytes - totalExpandedBytes)
            {
                throw new InvalidDataException("O conteúdo expandido excede o limite permitido.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var countingStream = new BoundedWriteStream(output, MaximumExpandedBytes - totalExpandedBytes);
            reader.WriteEntryTo(countingStream);
            totalExpandedBytes += countingStream.BytesWritten;
        }
    }

    private static async Task ExtractCoreZipAsync(
        string archivePath,
        string destinationPath,
        string expectedFileName,
        CancellationToken cancellationToken)
    {
        await using var archiveStream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        var matchingEntries = archive.Entries
            .Where(entry => entry.Name.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matchingEntries.Length != 1)
        {
            throw new InvalidDataException("O ZIP oficial não contém um único core com o nome esperado.");
        }

        var entryToExtract = matchingEntries[0];
        if (entryToExtract.Length <= 0 || entryToExtract.Length > MaximumCoreBytes)
        {
            throw new InvalidDataException("O tamanho do core está fora dos limites permitidos.");
        }

        var temporaryPath = destinationPath + $".{Guid.NewGuid():N}.part";
        try
        {
            await using var input = entryToExtract.Open();
            await using var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 64,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[1024 * 64];
            long totalBytes = 0;
            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes > MaximumCoreBytes)
                {
                    throw new InvalidDataException("O core extraído excedeu o limite de tamanho.");
                }

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            if (output.Length != entryToExtract.Length)
            {
                throw new InvalidDataException("O tamanho do core extraído não corresponde ao arquivo ZIP.");
            }

            await output.FlushAsync(cancellationToken);
            File.Move(temporaryPath, destinationPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static HttpClient CreateHttpClient() =>
        new()
        {
            Timeout = TimeSpan.FromMinutes(10),
            DefaultRequestVersion = new Version(2, 0)
        };

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("A instalação automática do RetroArch está disponível somente no Windows.");
        }
    }

    private sealed class BoundedWriteStream(Stream inner, long maximumBytes) : Stream
    {
        public long BytesWritten { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureWithinLimit(count);
            inner.Write(buffer, offset, count);
            BytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWithinLimit(buffer.Length);
            inner.Write(buffer);
            BytesWritten += buffer.Length;
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            EnsureWithinLimit(buffer.Length);
            await inner.WriteAsync(buffer, cancellationToken);
            BytesWritten += buffer.Length;
        }

        private void EnsureWithinLimit(int count)
        {
            if (count > maximumBytes - BytesWritten)
            {
                throw new InvalidDataException("O arquivo expandido excede o limite permitido.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Flush();
            }

            base.Dispose(disposing);
        }
    }
}
