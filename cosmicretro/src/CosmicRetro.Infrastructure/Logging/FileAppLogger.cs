namespace CosmicRetro.Infrastructure.Logging;

public sealed class FileAppLogger
{
    private readonly string _logPath;
    private readonly object _sync = new();

    public FileAppLogger(string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);
        _logPath = Path.GetFullPath(logPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
    }

    public void LogError(string context, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentNullException.ThrowIfNull(exception);
        var entry = $"{DateTimeOffset.UtcNow:O} ERROR {context}{Environment.NewLine}{exception}{Environment.NewLine}";
        lock (_sync)
        {
            File.AppendAllText(_logPath, entry);
        }
    }
}
