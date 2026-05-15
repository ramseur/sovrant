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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize SQLite storage at {DbPath}. The application will continue but data will not be persisted. Set SOVRANT_DB_REQUIRE=true to fail fast instead. Error: {ErrorMessage}")]
    private static partial void LogInitFailed(ILogger logger, string dbPath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Migration V{Version} ('{Description}') failed to apply: {ErrorMessage}. To recover, restore your pre-upgrade backup (see SOVRANT_DB_BACKUP_ON_UPGRADE) or consult docs/db-upgrades.md.")]
    private static partial void LogMigrationFailed(ILogger logger, int version, string description, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Migration checksum drift detected on V{Version} ('{Description}'). An already-applied migration's embedded SQL no longer matches the checksum recorded when it was applied. Add a new V-numbered migration instead of editing an existing one.")]
    private static partial void LogMigrationDrift(ILogger logger, int version, string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created data directory: {Directory}")]
    private static partial void LogDirCreated(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pre-migration backup written to {BackupPath} (from schema v{FromVersion})")]
    private static partial void LogBackupWritten(ILogger logger, string backupPath, int fromVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "SOVRANT_DB_BACKUP_ON_UPGRADE is set but backup to {BackupPath} failed: {ErrorMessage}. Migration will NOT proceed — restore a copy of the DB or unset the flag to continue.")]
    private static partial void LogBackupFailed(ILogger logger, string backupPath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Created Sovrant database at {DbPath} (schema v{Version}). This is a first-time initialization.")]
    private static partial void LogFirstBoot(ILogger logger, string dbPath, int version);

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
    public string? DatabasePath => _dbPath;

    /// <inheritdoc />
    public StorageHealth CheckHealth()
    {
        try
        {
            using var connection = CreateReadOnlyConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM schema_version";
            _ = cmd.ExecuteScalar();
            return new StorageHealth(Ok: true, SchemaVersion: _schemaVersion, Path: _dbPath, Error: null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new StorageHealth(Ok: false, SchemaVersion: _schemaVersion, Path: _dbPath, Error: ex.Message);
        }
    }

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            using var connection = CreateConnection();
            SetPragmas(connection);

            // Phase 42.5 — optional pre-migration backup. If
            // SOVRANT_DB_BACKUP_ON_UPGRADE is set and there are pending
            // migrations against an existing DB, checkpoint the WAL and
            // copy sovrant.db to sovrant.db.bak-{currentVersion} before
            // letting the runner touch anything. On a fresh install there
            // is nothing to back up (pending == all migrations, but the
            // file holds only the empty schema_version table), so we
            // detect "is this a fresh file" via currentVersion == 0 and
            // skip.
            if (ShouldBackupBeforeUpgrade())
            {
                var pending = MigrationRunner.GetPendingMigrations(connection);
                // We need the current version for both the backup filename
                // and the "fresh install?" check. Use the runner's helper
                // indirectly by re-reading schema_version.
                var currentVersion = ReadSchemaVersion(connection);
                if (pending.Count > 0 && currentVersion > 0)
                {
                    BackupBeforeUpgrade(connection, currentVersion);
                }
            }

            // Phase 42.5 item 10 — detect first boot before migrations run.
            var preVersion = ReadSchemaVersion(connection);

            var runner = new MigrationRunner(_logger);
            _schemaVersion = runner.RunPendingMigrations(connection);

            if (preVersion == 0)
                LogFirstBoot(_logger, _dbPath, _schemaVersion);
            else
                LogInitialized(_logger, _schemaVersion);
        }
        catch (MigrationDriftException ex)
        {
            // Phase 42.5 — checksum drift is always a hard error because
            // continuing would mean running on an unknown schema.
            LogMigrationDrift(_logger, ex.Version, ex.Description);
            HardenDbFilePermissions();
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogInitFailed(_logger, _dbPath, ex.Message);

            // Phase 42.5 — SOVRANT_DB_REQUIRE=true turns init failures into
            // a hard throw so the server exits at boot instead of running
            // silently with no persistence. Recommended for production.
            if (IsDbRequired())
            {
                HardenDbFilePermissions();
                throw new InvalidOperationException(
                    $"SOVRANT_DB_REQUIRE=true but SQLite storage at '{_dbPath}' failed to initialize: {ex.Message}",
                    ex);
            }
        }

        // Phase 38 PR 4 — tighten file permissions after the DB file has
        // been created by the first Open(). Done post-init so it also
        // applies to the WAL and SHM sidecars that SetPragmas created.
        HardenDbFilePermissions();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads <c>SOVRANT_DB_BACKUP_ON_UPGRADE</c>. Truthy values enable the
    /// pre-migration file-copy backup. Default is off — operators opt in.
    /// </summary>
    private static bool ShouldBackupBeforeUpgrade()
    {
        var v = Environment.GetEnvironmentVariable("SOVRANT_DB_BACKUP_ON_UPGRADE");
        if (string.IsNullOrWhiteSpace(v)) return false;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("1", StringComparison.Ordinal)
            || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the current max(version) out of schema_version. Returns 0 if
    /// the table is empty or doesn't exist yet (fresh file).
    /// </summary>
    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='schema_version'";
        if (check.ExecuteScalar() is null) return 0;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version";
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Checkpoints the WAL then copies the DB file to
    /// <c>{_dbPath}.bak-{fromVersion}</c>. Throws on failure — if the
    /// operator asked for backups, they want migration to halt rather
    /// than proceed without one.
    /// </summary>
    private void BackupBeforeUpgrade(SqliteConnection connection, int fromVersion)
    {
        var backupPath = $"{_dbPath}.bak-{fromVersion}";
        try
        {
            // Fold the WAL into the main file so File.Copy sees a
            // consistent snapshot. TRUNCATE writes the WAL back to the
            // main file and empties the WAL sidecar.
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                cmd.ExecuteNonQuery();
            }

            File.Copy(_dbPath, backupPath, overwrite: true);
            LogBackupWritten(_logger, backupPath, fromVersion);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogBackupFailed(_logger, backupPath, ex.Message);
            throw new InvalidOperationException(
                $"SOVRANT_DB_BACKUP_ON_UPGRADE is set but backup to '{backupPath}' failed: {ex.Message}. " +
                "Migration was not attempted. Resolve the backup failure (disk space, permissions) or unset the flag.",
                ex);
        }
    }

    /// <summary>
    /// Reads <c>SOVRANT_DB_REQUIRE</c> and returns true if set to a
    /// truthy value. Defaults to false so dev/test unchanged.
    /// </summary>
    private static bool IsDbRequired()
    {
        var v = Environment.GetEnvironmentVariable("SOVRANT_DB_REQUIRE");
        if (string.IsNullOrWhiteSpace(v)) return false;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("1", StringComparison.Ordinal)
            || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
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
    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
