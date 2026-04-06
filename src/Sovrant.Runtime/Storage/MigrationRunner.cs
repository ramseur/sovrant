using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

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

        var applied = GetAppliedVersions(connection);
        var migrations = DiscoverMigrations();

        foreach (var m in migrations)
        {
            if (applied.Contains(m.Version))
                continue;

            LogApplying(logger, m.Version, m.Description);

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
            LogApplied(logger, m.Version);
        }

        return GetCurrentVersion(connection);
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

    private static HashSet<int> GetAppliedVersions(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT version FROM {VersionTable}";
        using var reader = cmd.ExecuteReader();
        var versions = new HashSet<int>();
        while (reader.Read())
            versions.Add(reader.GetInt32(0));
        return versions;
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
