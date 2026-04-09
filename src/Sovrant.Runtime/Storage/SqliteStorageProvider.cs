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
        _readOnlyConnectionString = $"Data Source={_dbPath};Mode=ReadOnly;Cache=Shared";
    }

    private readonly string _readOnlyConnectionString;

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

        // Phase 38 PR 4 — tighten file permissions after the DB file has
        // been created by the first Open(). Done post-init so it also
        // applies to the WAL and SHM sidecars that SetPragmas created.
        HardenDbFilePermissions();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Restricts the on-disk SQLite files (main + WAL + SHM) to owner-only
    /// read/write on Unix (<c>0600</c>). On Windows the default ACL for
    /// files under the user profile already grants access only to the
    /// owner and SYSTEM, so no programmatic hardening is attempted —
    /// operators running in multi-user server deployments should manage
    /// NTFS ACLs through group policy or the install script.
    /// </summary>
    private void HardenDbFilePermissions()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
            return;

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (!File.Exists(path)) continue;
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Non-fatal: the server keeps running with the default
                // umask-derived permissions. Log once per path so operators
                // can notice.
                LogInitFailed(_logger, path, $"chmod 600 failed: {ex.Message}");
            }
        }
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

    /// <inheritdoc cref="ISqliteConnectionFactory.CreateReadOnlyConnection" />
    SqliteConnection ISqliteConnectionFactory.CreateReadOnlyConnection() => CreateReadOnlyConnection();

    internal SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        SetPragmas(connection);
        return connection;
    }

    /// <summary>
    /// Phase 38 — read-only connection variant. Opens with <c>Mode=ReadOnly</c>
    /// so any accidental write attempt fails fast at the engine rather than
    /// reaching the file. Query pragmas only; write pragmas are skipped.
    /// </summary>
    internal SqliteConnection CreateReadOnlyConnection()
    {
        var connection = new SqliteConnection(_readOnlyConnectionString);
        connection.Open();
        SetReadOnlyPragmas(connection);
        return connection;
    }

    private static void SetReadOnlyPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        // Query-tuning pragmas only. Write-side pragmas (secure_delete,
        // foreign_keys enforcement on INSERT/UPDATE/DELETE) are meaningless
        // on a read-only handle and would just add latency.
        cmd.CommandText = """
            PRAGMA busy_timeout = 5000;
            PRAGMA cache_size = -20000;
            PRAGMA query_only = ON;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void SetPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        // secure_delete = ON (Phase 38 PR 4): when a row is deleted, SQLite
        // overwrites the freed pages with zeros instead of leaving the old
        // bytes in place. Matters because this DB holds token hashes,
        // credential blobs, and audit content — deletions should not leave
        // recoverable tails on disk. Per-connection cost is tiny; the write
        // amplification is only paid on actual DELETEs, not on inserts or
        // updates.
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            PRAGMA cache_size = -20000;
            PRAGMA secure_delete = ON;
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
