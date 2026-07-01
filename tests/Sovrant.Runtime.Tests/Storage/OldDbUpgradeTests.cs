using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

/// <summary>
/// Phase 42.5 — proves that a database created at an older schema version
/// survives an in-place upgrade to the current version with all user data
/// intact. Without these tests, the migration runner's "additive only"
/// design is an unverified claim.
/// </summary>
public sealed class OldDbUpgradeTests : IAsyncDisposable
{
    private readonly string _dbPath;

    public OldDbUpgradeTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_upgrade_{Guid.NewGuid():N}.db");
    }

    public ValueTask DisposeAsync()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", ".bak-5", ".bak-0" })
        {
            var p = _dbPath + suffix;
            if (File.Exists(p))
            {
                try { File.Delete(p); }
                catch (IOException) { }
            }
        }
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task UpgradeFromV005_PreservesSessionsAuditAndUsage()
    {
        // ── Build a V005-era database ─────────────────────────────────────
        // V005 is the last schema version before workspaces/projects
        // (V006/V007) and before the orphan backfill (V008). Pre-V006
        // sessions had no workspace_id column — but migrations are
        // additive, so V006 adds the column with default '' rather than
        // rewriting old rows. We simulate a real V005 install by applying
        // V001..V005 by hand, inserting data with the V005 shape, then
        // running InitializeAsync to carry the DB up to current.
        ApplyMigrationsUpTo(5);
        SeedV005EraData();

        // ── Run the real initializer on the existing file ────────────────
        var provider = new SqliteStorageProvider(
            NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        await provider.InitializeAsync();
        Assert.Equal(46, provider.SchemaVersion);

        // ── Verify V005-era data survived ────────────────────────────────
        using var conn = ((ISqliteConnectionFactory)provider).CreateConnection();

        // Users: alice and bob still exist
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM users WHERE user_id IN ('alice','bob')";
            Assert.Equal(2L, (long)cmd.ExecuteScalar()!);
        }

        // Sessions: the 3 V005-era rows still exist, now carrying the new
        // workspace_id column (value defaults to '' per V006).
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT session_id, user_id FROM sessions ORDER BY session_id";
            using var r = cmd.ExecuteReader();
            var sessions = new List<(string sid, string uid)>();
            while (r.Read()) sessions.Add((r.GetString(0), r.GetString(1)));
            Assert.Equal(3, sessions.Count);
            Assert.Contains(sessions, s => s.sid == "s_alice_1" && s.uid == "alice");
            Assert.Contains(sessions, s => s.sid == "s_bob_1" && s.uid == "bob");
            Assert.Contains(sessions, s => s.sid == "s_bob_2" && s.uid == "bob");
        }

        // Session entries survive (content-level row preservation)
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM session_entries WHERE session_id = 's_alice_1'";
            Assert.Equal(2L, (long)cmd.ExecuteScalar()!);
        }

        // Token usage survives
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT SUM(input_tokens), SUM(output_tokens) FROM token_usage";
            using var r = cmd.ExecuteReader();
            Assert.True(r.Read());
            Assert.Equal(300L, r.GetInt64(0));
            Assert.Equal(150L, r.GetInt64(1));
        }

        // Audit bash survives
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM audit_bash";
            Assert.Equal(2L, (long)cmd.ExecuteScalar()!);
        }

        // ── Verify V006/V007 tables were created by the upgrade ──────────
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT name FROM sqlite_master WHERE type='table'
                AND name IN ('workspaces', 'workspace_members', 'projects')
                ORDER BY name
                """;
            using var r = cmd.ExecuteReader();
            var tables = new List<string>();
            while (r.Read()) tables.Add(r.GetString(0));
            Assert.Equal(new[] { "projects", "workspace_members", "workspaces" }, tables);
        }

        await provider.DisposeAsync();
    }

    [Fact]
    public async Task UpgradeFromV005_WithBackupFlag_WritesBackupFile()
    {
        // Phase 42.5 — SOVRANT_DB_BACKUP_ON_UPGRADE=true must produce
        // {dbPath}.bak-{oldVersion} before the migration runner touches
        // the DB. The stamped version is the *pre-migration* one (V005),
        // not the post-migration one, so a rollback copies the right file
        // back.
        ApplyMigrationsUpTo(5);
        SeedV005EraData();

        Environment.SetEnvironmentVariable("SOVRANT_DB_BACKUP_ON_UPGRADE", "true");
        try
        {
            var provider = new SqliteStorageProvider(
                NullLogger<SqliteStorageProvider>.Instance, _dbPath);
            await provider.InitializeAsync();
            Assert.Equal(46, provider.SchemaVersion);
            await provider.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_DB_BACKUP_ON_UPGRADE", null);
        }

        var backupPath = $"{_dbPath}.bak-5";
        Assert.True(File.Exists(backupPath), $"Expected backup at {backupPath}");

        // The backup should contain V005-era data and be at schema v5.
        using var conn = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT MAX(version) FROM schema_version";
            Assert.Equal(5L, Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sessions";
            Assert.Equal(3L, (long)cmd.ExecuteScalar()!);
        }
        conn.Close();

        // Clean up the backup file.
        try { File.Delete(backupPath); } catch (IOException) { }
    }

    [Fact]
    public async Task FreshInstall_WithBackupFlag_DoesNotWriteBackup()
    {
        // On a fresh install there's nothing meaningful to back up —
        // the file is brand new and holds only an empty schema_version
        // row. The flag should be a no-op here (no .bak-0 file).
        Environment.SetEnvironmentVariable("SOVRANT_DB_BACKUP_ON_UPGRADE", "true");
        try
        {
            var provider = new SqliteStorageProvider(
                NullLogger<SqliteStorageProvider>.Instance, _dbPath);
            await provider.InitializeAsync();
            Assert.Equal(46, provider.SchemaVersion);
            await provider.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_DB_BACKUP_ON_UPGRADE", null);
        }

        Assert.False(File.Exists($"{_dbPath}.bak-0"));
    }

    [Fact]
    public async Task UpgradeFromV005_SchemaVersionRowsStamped()
    {
        ApplyMigrationsUpTo(5);
        SeedV005EraData();

        var provider = new SqliteStorageProvider(
            NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        await provider.InitializeAsync();

        using var conn = ((ISqliteConnectionFactory)provider).CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT version, checksum FROM schema_version ORDER BY version";
        using var r = cmd.ExecuteReader();
        var rows = new List<(int v, string? c)>();
        while (r.Read())
            rows.Add((r.GetInt32(0), r.IsDBNull(1) ? null : r.GetString(1)));

        // V001..V005 applied manually (no checksum), V006..V046 applied
        // by the real runner (with checksum).
        Assert.Equal(46, rows.Count);
        Assert.All(rows.Where(x => x.v <= 5), x => Assert.Null(x.c));
        Assert.All(rows.Where(x => x.v >= 6), x => Assert.False(string.IsNullOrEmpty(x.c)));

        await provider.DisposeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Applies V001..V{maxVersion} directly against a fresh DB file,
    /// stamping schema_version as if they had been applied normally but
    /// without a checksum (to match pre-Phase-42.5 behavior on legacy
    /// rows, and to avoid drift detection tripping on our simulated-old
    /// install).
    /// </summary>
    private void ApplyMigrationsUpTo(int maxVersion)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate");
        conn.Open();

        using (var pragmas = conn.CreateCommand())
        {
            pragmas.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA foreign_keys = ON;
                """;
            pragmas.ExecuteNonQuery();
        }

        // Create schema_version table the way MigrationRunner does.
        using (var vt = conn.CreateCommand())
        {
            vt.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_version (
                    version     INTEGER PRIMARY KEY,
                    description TEXT    NOT NULL,
                    applied_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                    checksum    TEXT
                )
                """;
            vt.ExecuteNonQuery();
        }

        var assembly = typeof(MigrationRunner).Assembly;
        var prefix = "Sovrant.Runtime.Storage.Migrations.";

        for (int v = 1; v <= maxVersion; v++)
        {
            var resName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.StartsWith($"{prefix}V{v:D3}__", StringComparison.Ordinal));
            Assert.NotNull(resName);

            using var stream = assembly.GetManifestResourceStream(resName)!;
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            using var tx = conn.BeginTransaction();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            using (var record = conn.CreateCommand())
            {
                record.Transaction = tx;
                // Leave checksum NULL — represents a legacy install that
                // predates Phase 42.5's checksum stamping.
                record.CommandText = "INSERT INTO schema_version (version, description) VALUES ($v, $d)";
                record.Parameters.AddWithValue("$v", v);
                record.Parameters.AddWithValue("$d", $"legacy-v{v}");
                record.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    /// <summary>
    /// Inserts rows that reflect the V005-era schema: users, sessions,
    /// session_entries, token_usage, audit_bash.
    /// </summary>
    private void SeedV005EraData()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (user_id, username, role, status)
            VALUES ('alice', 'alice', 'user', 'active'),
                   ('bob',   'bob',   'user', 'active');

            INSERT INTO sessions (session_id, user_id, status)
            VALUES ('s_alice_1', 'alice', 'ended'),
                   ('s_bob_1',   'bob',   'ended'),
                   ('s_bob_2',   'bob',   'active');

            INSERT INTO session_entries (session_id, entry_uid, timestamp, role, content)
            VALUES ('s_alice_1', 'e1', strftime('%Y-%m-%dT%H:%M:%fZ','now'), 'user',      'Hi'),
                   ('s_alice_1', 'e2', strftime('%Y-%m-%dT%H:%M:%fZ','now'), 'assistant', 'Hello');

            INSERT INTO token_usage (session_id, user_id, model, input_tokens, output_tokens)
            VALUES ('s_alice_1', 'alice', 'gpt-4o', 100, 50),
                   ('s_bob_1',   'bob',   'gpt-4o', 200, 100);

            INSERT INTO audit_bash (timestamp, command, session_id, exit_code)
            VALUES (strftime('%Y-%m-%dT%H:%M:%fZ','now'), 'ls',  's_alice_1', 0),
                   (strftime('%Y-%m-%dT%H:%M:%fZ','now'), 'pwd', 's_bob_1',   0);
            """;
        cmd.ExecuteNonQuery();
    }
}
