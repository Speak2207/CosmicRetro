namespace CosmicRetro.Core.Launching;

public interface IGameLauncher
{
    Task<int> LaunchAsync(RetroArchCommand command);
}
