using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed storage provider. Manages the database lifecycle, runs migrations,
/// and provides an internal connection factory for domain-specific stores.
/// </summary>
public sealed partial class SqliteStorageProvider : IStorageProvider, ISqliteConnectionFactory
{
    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly ILogger<SqliteStorageProvider> _logger;
    private int _schemaVersion;

    [LoggerMessage(Level = LogLevel.Information, Message = "SQLite storage initialized at schema version {Version}")]
    private static partial void LogInitialized(ILogger logger, int version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize SQLite storage at {DbPath}. The application will continue but data will not be persisted. Error: {ErrorMessage}")]
    private static partial void LogInitFailed(ILogger logger, string dbPath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created data directory: {Directory}")]
    private static partial void LogDirCreated(ILogger logger, string directory);

    public SqliteStorageProvider(ILogger<SqliteStorageProvider> logger, string? dbPath = null)
    {
        _logger = logger;

        _dbPath = dbPath
            ?? Environment.GetEnvironmentVariable("SOVRANT_DB_PATH")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".sovrant", "data", "sovrant.db");

        // Always ensure the data directory exists (even on fresh install).
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            try
            {
                Directory.CreateDirectory(dir);
                LogDirCreated(_logger, dir);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogInitFailed(_logger, _dbPath, $"Cannot create directory: {ex.Message}");
            }
        }

        _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared";
    }

    /// <inheritdoc />
    public int SchemaVersion => _schemaVersion;

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            using var connection = CreateConnection();
            SetPragmas(connection);

            var runner = new MigrationRunner(_logger);
            _schemaVersion = runner.RunPendingMigrations(connection);

            // Seed the default user after migrations create the users table.
            SeedDefaultUser(connection);

            LogInitialized(_logger, _schemaVersion);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogInitFailed(_logger, _dbPath, ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var connection = CreateConnection();
        var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var _ = tx.ConfigureAwait(false);
        try
        {
            await action(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Creates and opens a new connection with PRAGMAs applied.</summary>
    SqliteConnection ISqliteConnectionFactory.CreateConnection() => CreateConnection();

    internal SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        SetPragmas(connection);
        return connection;
    }

    private static void SetPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            PRAGMA cache_size = -20000;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void SeedDefaultUser(SqliteConnection connection)
    {
        // Check if users table exists.
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='users'";
        if (check.ExecuteScalar() is null)
            return;

        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO users (user_id, username, role, status, created_at, updated_at)
            VALUES ($id, $name, 'admin', 'active',
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.Parameters.AddWithValue("$name", userId);
        cmd.ExecuteNonQuery();

        // Auto-create personal workspace (Phase 35).
        SeedPersonalWorkspace(connection, userId);
    }

    private static void SeedPersonalWorkspace(SqliteConnection connection, string userId)
    {
        // Only seed if the workspaces table exists.
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='workspaces'";
        if (check.ExecuteScalar() is null)
            return;

        var workspaceId = $"ws-personal-{userId}";
        var slug = $"personal-{userId}";

        using var wsCmd = connection.CreateCommand();
        wsCmd.CommandText = """
            INSERT OR IGNORE INTO workspaces (workspace_id, type, name, slug, owner_id, created_at, updated_at)
            VALUES ($wid, 'personal', $name, $slug, $owner,
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        wsCmd.Parameters.AddWithValue("$wid", workspaceId);
        wsCmd.Parameters.AddWithValue("$name", $"{userId}'s Workspace");
        wsCmd.Parameters.AddWithValue("$slug", slug);
        wsCmd.Parameters.AddWithValue("$owner", userId);
        wsCmd.ExecuteNonQuery();

        using var memberCmd = connection.CreateCommand();
        memberCmd.CommandText = """
            INSERT OR IGNORE INTO workspace_members (workspace_id, user_id, role, joined_at)
            VALUES ($wid, $uid, 'owner', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        memberCmd.Parameters.AddWithValue("$wid", workspaceId);
        memberCmd.Parameters.AddWithValue("$uid", userId);
        memberCmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
