using CosmicRetro.Core.Models;

namespace CosmicRetro.Core.Services;

public interface IRetroArchSetupService
{
    Task<string> InstallRetroArchAsync(
        string installationDirectory,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CoreInstallResult>> InstallRecommendedCoresAsync(
        string retroArchDirectory,
        CancellationToken cancellationToken = default);
}
