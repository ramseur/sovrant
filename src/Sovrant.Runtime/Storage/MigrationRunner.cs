using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Errors;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Reads embedded SQL migration scripts (V001__name.sql, V002__name.sql, …)
/// and applies them in order. Tracks applied versions in the <c>schema_version</c> table.
/// </summary>
internal sealed partial class MigrationRunner(ILogger logger)
{
    private const string VersionTable = "schema_version";

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying migration V{Version}: {Description}")]
    private static partial void LogApplying(ILogger logger, int version, string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migration V{Version} applied successfully")]
    private static partial void LogApplied(ILogger logger, int version);

    /// <summary>Runs all pending migrations and returns the current schema version.</summary>
    public int RunPendingMigrations(SqliteConnection connection)
    {
        EnsureVersionTable(connection);

        var applied = GetAppliedChecksums(connection);
        var migrations = DiscoverMigrations();

        // Phase 42.5 — drift detection. Before applying any new migration,
        // verify every already-applied migration still matches the embedded
        // SQL. If a previously-applied V00X.sql has been edited in place,
        // the checksum will no longer match and we fail loudly rather than
        // silently skipping the edited version.
        VerifyNoChecksumDrift(applied, migrations);

        foreach (var m in migrations)
        {
            if (applied.ContainsKey(m.Version))
                continue;

            LogApplying(logger, m.Version, m.Description);

            // Migrations that start with "-- sovrant:no-fk" need FK enforcement
            // disabled for the duration of the migration (e.g. PK cascade rewrites).
            // PRAGMA foreign_keys is a no-op inside a transaction in SQLite, so it
            // must be executed on the connection before BeginTransaction().
            bool disableFk = m.Sql.TrimStart().StartsWith("-- sovrant:no-fk", StringComparison.Ordinal);
            if (disableFk)
            {
                using var off = connection.CreateCommand();
                off.CommandText = "PRAGMA foreign_keys = OFF";
                off.ExecuteNonQuery();
            }

            using var tx = connection.BeginTransaction();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
#pragma warning disable CA2100 // SQL comes from embedded resources, not user input
            cmd.CommandText = m.Sql;
#pragma warning restore CA2100
            cmd.ExecuteNonQuery();

            // Record the migration.
            using var record = connection.CreateCommand();
            record.Transaction = tx;
            record.CommandText = $"""
                INSERT INTO {VersionTable} (version, description, applied_at, checksum)
                VALUES ($v, $d, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), $c)
                """;
            record.Parameters.AddWithValue("$v", m.Version);
            record.Parameters.AddWithValue("$d", m.Description);
            record.Parameters.AddWithValue("$c", ComputeChecksum(m.Sql));
            record.ExecuteNonQuery();

            tx.Commit();

            if (disableFk)
            {
                using var on = connection.CreateCommand();
                on.CommandText = "PRAGMA foreign_keys = ON";
                on.ExecuteNonQuery();
            }

            LogApplied(logger, m.Version);
        }

        return GetCurrentVersion(connection);
    }

    /// <summary>
    /// Returns the list of embedded migrations that have not yet been applied
    /// to the given database. Used by <c>sovrant db migrate --dry-run</c> so
    /// operators can see exactly what a real migration run would do.
    /// </summary>
    public static IReadOnlyList<(int Version, string Description)> GetPendingMigrations(SqliteConnection connection)
    {
        EnsureVersionTable(connection);
        var applied = GetAppliedChecksums(connection);
        var result = new List<(int, string)>();
        foreach (var m in DiscoverMigrations())
        {
            if (!applied.ContainsKey(m.Version))
                result.Add((m.Version, m.Description));
        }
        return result;
    }

    private static void EnsureVersionTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {VersionTable} (
                version     INTEGER PRIMARY KEY,
                description TEXT    NOT NULL,
                applied_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                checksum    TEXT
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private static Dictionary<int, string?> GetAppliedChecksums(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT version, checksum FROM {VersionTable}";
        using var reader = cmd.ExecuteReader();
        var result = new Dictionary<int, string?>();
        while (reader.Read())
        {
            var version = reader.GetInt32(0);
            var checksum = reader.IsDBNull(1) ? null : reader.GetString(1);
            result[version] = checksum;
        }
        return result;
    }

    /// <summary>
    /// Throws <see cref="MigrationDriftException"/> if any previously-applied
    /// migration's stored checksum no longer matches the embedded SQL. A null
    /// stored checksum (legacy pre-Phase-42.5 rows) is tolerated since we
    /// can't retroactively prove drift on those.
    /// </summary>
    private static void VerifyNoChecksumDrift(
        Dictionary<int, string?> applied,
        List<Migration> embedded)
    {
        foreach (var m in embedded)
        {
            if (!applied.TryGetValue(m.Version, out var storedChecksum))
                continue; // not applied yet — no drift possible
            if (storedChecksum is null)
                continue; // legacy row with no checksum recorded
            var expected = ComputeChecksum(m.Sql);
            if (!string.Equals(expected, storedChecksum, StringComparison.Ordinal))
                throw new MigrationDriftException(m.Version, m.Description, storedChecksum, expected);
        }
    }

    private static int GetCurrentVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(version), 0) FROM {VersionTable}";
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Discovers embedded SQL resources named <c>V{NNN}__{description}.sql</c>
    /// and returns them sorted by version number.
    /// </summary>
    private static List<Migration> DiscoverMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var prefix = "Sovrant.Runtime.Storage.Migrations.";
        var result = new List<Migration>();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".sql", StringComparison.Ordinal))
                continue;

            var fileName = name[prefix.Length..^4]; // strip prefix and .sql
            var parts = fileName.Split("__", 2, StringSplitOptions.None);
            if (parts.Length != 2 || !int.TryParse(parts[0][1..], out var version)) // V001 → 1
                continue;

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var sql = reader.ReadToEnd();

            result.Add(new Migration(version, parts[1].Replace('_', ' '), sql));
        }

        result.Sort((a, b) => a.Version.CompareTo(b.Version));
        return result;
    }

    private static string ComputeChecksum(string sql)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexStringLower(hash);
    }

    private sealed record Migration(int Version, string Description, string Sql);
}

/// <summary>
/// Thrown when a previously-applied migration's recorded checksum no longer
/// matches the embedded SQL. Indicates that someone edited a V00X__*.sql
/// file in place after it had already been applied to the database — a
/// change that silently does nothing because the runner skips applied
/// versions. Catching this at boot prevents corrupt "I edited the old
/// migration but nothing happened" states.
/// </summary>
public sealed class MigrationDriftException : SovrantException
{
    public int Version { get; }
    public string Description { get; }
    public string StoredChecksum { get; }
    public string EmbeddedChecksum { get; }

    public MigrationDriftException() : this(0, string.Empty, string.Empty, string.Empty) { }
    public MigrationDriftException(string message) : base(message)
    {
        Description = string.Empty;
        StoredChecksum = string.Empty;
        EmbeddedChecksum = string.Empty;
    }
    public MigrationDriftException(string message, Exception innerException) : base(message, innerException)
    {
        Description = string.Empty;
        StoredChecksum = string.Empty;
        EmbeddedChecksum = string.Empty;
    }

    internal MigrationDriftException(int version, string description, string storedChecksum, string embeddedChecksum)
        : base(
            $"Migration V{version:D3} ('{description}') has drifted: the embedded SQL checksum " +
            $"({embeddedChecksum[..Math.Min(12, embeddedChecksum.Length)]}…) does not match the checksum " +
            $"recorded when it was applied ({storedChecksum[..Math.Min(12, storedChecksum.Length)]}…). " +
            "Edit migrations by adding a new V-numbered file instead of modifying an existing one; " +
            "if this drift is intentional, bump the version number or manually update schema_version.checksum.")
    {
        Version = version;
        Description = description;
        StoredChecksum = storedChecksum;
        EmbeddedChecksum = embeddedChecksum;
    }
}
