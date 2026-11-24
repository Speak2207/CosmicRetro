using System.Globalization;
using CosmicRetro.Core.Models;
using CosmicRetro.Core.Platforms;
using CosmicRetro.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace CosmicRetro.Infrastructure.Persistence;

public sealed class SqliteCosmicRepository : ICosmicRepository
{
    private readonly string _connectionString;

    public SqliteCosmicRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS games (
                path TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                platform_id TEXT NOT NULL,
                cover_path TEXT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                imported_utc TEXT NOT NULL,
                original_name TEXT NULL,
                region TEXT NULL,
                version TEXT NULL,
                description TEXT NULL,
                compatibility_warning TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS emulator_profiles (
                platform_id TEXT NOT NULL PRIMARY KEY,
                core_path TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS rom_sources (
                folder_path TEXT NOT NULL COLLATE NOCASE,
                platform_id TEXT NOT NULL,
                PRIMARY KEY (folder_path, platform_id)
            );
            CREATE TABLE IF NOT EXISTS play_sessions (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                game_path TEXT NOT NULL,
                started_utc TEXT NOT NULL,
                ended_utc TEXT NULL,
                exit_code INTEGER NULL,
                FOREIGN KEY (game_path) REFERENCES games(path) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_play_sessions_started
                ON play_sessions(started_utc DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "games", "original_name", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "games", "region", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "games", "version", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "games", "description", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "games", "compatibility_warning", "TEXT NULL", cancellationToken);
    }

    public async Task<IReadOnlyList<GameEntry>> GetGamesAsync(
        string? platformId = null,
        string? searchText = null,
        bool favoritesOnly = false,
        bool historyOnly = false,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT g.path, g.name, g.platform_id, g.cover_path, g.is_favorite, g.imported_utc,
                   h.last_played_utc, g.original_name, g.region, g.version, g.description,
                   g.compatibility_warning
            FROM games AS g
            LEFT JOIN (
                SELECT game_path, MAX(started_utc) AS last_played_utc
                FROM play_sessions
                GROUP BY game_path
            ) AS h ON h.game_path = g.path
            WHERE (@platform_id IS NULL OR g.platform_id = @platform_id)
              AND (@search_text = '' OR g.name LIKE @search_pattern ESCAPE '\')
              AND (@favorites_only = 0 OR g.is_favorite = 1)
              AND (@history_only = 0 OR h.last_played_utc IS NOT NULL)
            ORDER BY
                CASE WHEN @history_only = 1 THEN h.last_played_utc END DESC,
                g.name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("@platform_id", (object?)platformId ?? DBNull.Value);
        command.Parameters.AddWithValue("@search_text", searchText ?? string.Empty);
        command.Parameters.AddWithValue("@search_pattern", $"%{EscapeLikePattern(searchText ?? string.Empty)}%");
        command.Parameters.AddWithValue("@favorites_only", favoritesOnly ? 1 : 0);
        command.Parameters.AddWithValue("@history_only", historyOnly ? 1 : 0);

        var games = new List<GameEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(new GameEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4) != 0,
                ParseTimestamp(reader.GetString(5)),
                reader.IsDBNull(6) ? null : ParseTimestamp(reader.GetString(6)),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11)));
        }

        return games;
    }

    public async Task<int> ImportGamesAsync(
        IEnumerable<ScannedGame> games,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(games);
        var items = games.ToArray();
        if (items.Length == 0)
        {
            return 0;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var affected = 0;
        foreach (var game in items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(game.Path);
            ArgumentException.ThrowIfNullOrWhiteSpace(game.Name);
            var platform = PlatformCatalog.GetRequired(game.PlatformId);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO games (
                    path, name, platform_id, imported_utc, original_name, region, version, compatibility_warning)
                VALUES (
                    @path, @name, @platform_id, @imported_utc, @original_name, @region, @version, @compatibility_warning)
                ON CONFLICT(path) DO UPDATE SET
                    name = excluded.name,
                    platform_id = excluded.platform_id,
                    original_name = excluded.original_name,
                    region = excluded.region,
                    version = excluded.version,
                    compatibility_warning = excluded.compatibility_warning;
                """;
            command.Parameters.AddWithValue("@path", Path.GetFullPath(game.Path));
            command.Parameters.AddWithValue("@name", game.Name.Trim());
            command.Parameters.AddWithValue("@platform_id", platform.Id);
            command.Parameters.AddWithValue("@imported_utc", FormatTimestamp(DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("@original_name", (object?)game.OriginalName ?? game.Name);
            command.Parameters.AddWithValue("@region", (object?)game.Region ?? DBNull.Value);
            command.Parameters.AddWithValue("@version", (object?)game.Version ?? DBNull.Value);
            command.Parameters.AddWithValue("@compatibility_warning", (object?)game.CompatibilityWarning ?? DBNull.Value);
            affected += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return affected;
    }

    public async Task SetFavoriteAsync(
        string gamePath,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE games SET is_favorite = @favorite WHERE path = @path;";
        command.Parameters.AddWithValue("@favorite", isFavorite ? 1 : 0);
        command.Parameters.AddWithValue("@path", Path.GetFullPath(gamePath));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("O jogo não foi encontrado na biblioteca.");
        }
    }

    public async Task<IReadOnlyList<RomSource>> GetRomSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT folder_path, platform_id FROM rom_sources ORDER BY folder_path COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var sources = new List<RomSource>();
        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(new RomSource(reader.GetString(0), reader.GetString(1)));
        }

        return sources;
    }

    public async Task SaveRomSourceAsync(
        string folderPath,
        string platformId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var platform = PlatformCatalog.GetRequired(platformId);
        var fullFolderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullFolderPath))
        {
            throw new DirectoryNotFoundException($"A pasta selecionada não existe: {fullFolderPath}");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO rom_sources (folder_path, platform_id)
            VALUES (@folder_path, @platform_id)
            ON CONFLICT(folder_path, platform_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("@folder_path", fullFolderPath);
        command.Parameters.AddWithValue("@platform_id", platform.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveRomSourceAsync(
        string folderPath,
        string platformId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var fullFolderPath = Path.GetFullPath(folderPath);
        var platform = PlatformCatalog.GetRequired(platformId);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rom_sources WHERE folder_path = @folder_path AND platform_id = @platform_id;";
        command.Parameters.AddWithValue("@folder_path", fullFolderPath);
        command.Parameters.AddWithValue("@platform_id", platform.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetCoverPathAsync(
        string gamePath,
        string coverPath,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE games SET cover_path = @cover_path WHERE path = @path;";
        command.Parameters.AddWithValue("@cover_path", Path.GetFullPath(coverPath));
        command.Parameters.AddWithValue("@path", Path.GetFullPath(gamePath));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("O jogo não foi encontrado na biblioteca.");
        }
    }

    public async Task SaveMetadataAsync(
        string gamePath,
        GameMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gamePath);
        ArgumentNullException.ThrowIfNull(metadata);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE games
            SET name = @name,
                description = @description,
                cover_path = COALESCE(@cover_path, cover_path),
                region = COALESCE(@region, region),
                version = COALESCE(@version, version)
            WHERE path = @path;
            """;
        command.Parameters.AddWithValue("@name", metadata.Name);
        command.Parameters.AddWithValue("@description", (object?)metadata.Description ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@cover_path",
            metadata.CoverUri is { IsFile: true } coverUri ? coverUri.LocalPath : DBNull.Value);
        command.Parameters.AddWithValue("@region", (object?)metadata.Region ?? DBNull.Value);
        command.Parameters.AddWithValue("@version", (object?)metadata.Version ?? DBNull.Value);
        command.Parameters.AddWithValue("@path", Path.GetFullPath(gamePath));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("O jogo não foi encontrado na biblioteca.");
        }
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM app_settings;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        return new AppSettings(
            values.GetValueOrDefault("retroarch_path", string.Empty),
            values.GetValueOrDefault("last_rom_folder"),
            values.GetValueOrDefault("last_platform_id", "nes"),
            values.GetValueOrDefault("retroarch_system_path"));
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await SaveSettingAsync(connection, transaction, "retroarch_path", settings.RetroArchPath, cancellationToken);
        await SaveSettingAsync(connection, transaction, "last_rom_folder", settings.LastRomFolder ?? string.Empty, cancellationToken);
        await SaveSettingAsync(connection, transaction, "last_platform_id", settings.LastPlatformId, cancellationToken);
        await SaveSettingAsync(connection, transaction, "retroarch_system_path", settings.RetroArchSystemPath ?? string.Empty, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string?> GetSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_settings WHERE key = @key;";
        command.Parameters.AddWithValue("@key", key);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string stringValue ? stringValue : null;
    }

    public async Task SaveSettingAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO app_settings (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EmulatorProfile?> GetProfileAsync(
        string platformId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT platform_id, core_path FROM emulator_profiles WHERE platform_id = @platform_id;";
        command.Parameters.AddWithValue("@platform_id", PlatformCatalog.GetRequired(platformId).Id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new EmulatorProfile(reader.GetString(0), reader.GetString(1))
            : null;
    }

    public async Task SaveProfileAsync(
        EmulatorProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var platform = PlatformCatalog.GetRequired(profile.PlatformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.CorePath);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO emulator_profiles (platform_id, core_path)
            VALUES (@platform_id, @core_path)
            ON CONFLICT(platform_id) DO UPDATE SET core_path = excluded.core_path;
            """;
        command.Parameters.AddWithValue("@platform_id", platform.Id);
        command.Parameters.AddWithValue("@core_path", Path.GetFullPath(profile.CorePath));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> StartSessionAsync(
        string gamePath,
        CancellationToken cancellationToken = default)
    {
        var fullGamePath = Path.GetFullPath(gamePath);
        var timestamp = FormatTimestamp(DateTimeOffset.UtcNow);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using var sessionCommand = connection.CreateCommand();
        sessionCommand.Transaction = transaction;
        sessionCommand.CommandText =
            """
            INSERT INTO play_sessions (game_path, started_utc)
            VALUES (@game_path, @started_utc);
            SELECT last_insert_rowid();
            """;
        sessionCommand.Parameters.AddWithValue("@game_path", fullGamePath);
        sessionCommand.Parameters.AddWithValue("@started_utc", timestamp);
        var result = await sessionCommand.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            throw new InvalidOperationException("Não foi possível registrar a sessão de jogo.");
        }

        await transaction.CommitAsync(cancellationToken);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task EndSessionAsync(
        long sessionId,
        int? exitCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE play_sessions
            SET ended_utc = @ended_utc, exit_code = @exit_code
            WHERE id = @id AND ended_utc IS NULL;
            """;
        command.Parameters.AddWithValue("@ended_utc", FormatTimestamp(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("@exit_code", (object?)exitCode ?? DBNull.Value);
        command.Parameters.AddWithValue("@id", sessionId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("A sessão de jogo não foi encontrada ou já foi encerrada.");
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task SaveSettingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO app_settings (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await checkCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await reader.DisposeAsync();
        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
