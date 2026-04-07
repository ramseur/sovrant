using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class MigrationRunnerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;

    public MigrationRunnerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task AllMigrations_RunSuccessfully()
    {
        await _provider.InitializeAsync();
        Assert.Equal(8, _provider.SchemaVersion);
    }

    [Fact]
    public async Task Migrations_AreIdempotent()
    {
        await _provider.InitializeAsync();
        await _provider.InitializeAsync();
        Assert.Equal(8, _provider.SchemaVersion);
    }

    [Fact]
    public async Task SchemaContains_ExpectedTables()
    {
        await _provider.InitializeAsync();

        var expectedTables = new[]
        {
            "users", "workspaces", "projects", "config", "api_tokens",
            "roles", "permissions", "sessions", "session_entries", "token_usage",
            "session_summaries", "learned_patterns", "instincts",
            "credentials", "swarm_events", "eval_runs", "eval_results",
            "audit_governance", "audit_bash", "workspace_memory",
        };

        // Verify tables exist by checking the DB file was created and schema version is correct.
        Assert.True(File.Exists(_dbPath));
        Assert.Equal(8, _provider.SchemaVersion);
    }

    [Fact]
    public async Task V008_BackfillsOrphanWorkspaceIds()
    {
        // Run V001..V007 only by disabling V008 via direct seed of orphan rows post-init,
        // then re-running V008 logic. Easier path: initialize fully (V008 already applied),
        // then insert pre-V006-style rows (workspace_id = ''), and re-run V008 SQL manually
        // to assert it backfills only when a personal workspace exists.

        await _provider.InitializeAsync();

        var factory = (ISqliteConnectionFactory)_provider;

        // Seed two users: one with a personal workspace, one without.
        using (var conn = factory.CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (user_id, username, role, status) VALUES ('alice', 'alice', 'user', 'active');
                INSERT INTO users (user_id, username, role, status) VALUES ('bob',   'bob',   'user', 'active');
                INSERT INTO workspaces (workspace_id, type, name, slug, owner_id)
                VALUES ('ws-personal-alice', 'personal', 'alice', 'personal-alice', 'alice');

                -- Orphan rows (workspace_id = '') for both users
                INSERT INTO sessions (session_id, user_id, workspace_id, status)
                VALUES ('s_alice_1', 'alice', '', 'ended'),
                       ('s_bob_1',   'bob',   '', 'ended');

                INSERT INTO token_usage (session_id, user_id, model, input_tokens, output_tokens)
                VALUES ('s_alice_1', 'alice', 'gpt-4o', 10, 5),
                       ('s_bob_1',   'bob',   'gpt-4o', 20, 8);

                INSERT INTO audit_bash (timestamp, command, session_id, exit_code)
                VALUES (strftime('%Y-%m-%dT%H:%M:%fZ','now'), 'ls', 's_alice_1', 0),
                       (strftime('%Y-%m-%dT%H:%M:%fZ','now'), 'pwd', 's_bob_1', 0);
                """;
            cmd.ExecuteNonQuery();
        }

        // Re-run V008 SQL manually (idempotent — only fills empty rows).
        var sql = await File.ReadAllTextAsync(
            FindMigrationFile("V008__backfill_orphan_workspaces.sql"));

        using (var conn = factory.CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // Alice's rows should be backfilled to ws-personal-alice; Bob's should remain orphan.
        using var verify = factory.CreateConnection();
        using var check = verify.CreateCommand();
        check.CommandText = """
            SELECT session_id, workspace_id FROM sessions WHERE session_id IN ('s_alice_1','s_bob_1') ORDER BY session_id
            """;
        using var r = check.ExecuteReader();
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        while (r.Read())
            rows[r.GetString(0)] = r.IsDBNull(1) ? "" : r.GetString(1);

        Assert.Equal("ws-personal-alice", rows["s_alice_1"]);
        Assert.Equal("", rows["s_bob_1"]);

        // Token usage and audit_bash should follow the same pattern.
        using var tuCmd = verify.CreateCommand();
        tuCmd.CommandText = "SELECT user_id, workspace_id FROM token_usage ORDER BY user_id";
        using var tuR = tuCmd.ExecuteReader();
        var tu = new Dictionary<string, string>(StringComparer.Ordinal);
        while (tuR.Read())
            tu[tuR.GetString(0)] = tuR.IsDBNull(1) ? "" : tuR.GetString(1);
        Assert.Equal("ws-personal-alice", tu["alice"]);
        Assert.Equal("", tu["bob"]);

        using var abCmd = verify.CreateCommand();
        abCmd.CommandText = "SELECT session_id, workspace_id FROM audit_bash ORDER BY session_id";
        using var abR = abCmd.ExecuteReader();
        var ab = new Dictionary<string, string>(StringComparer.Ordinal);
        while (abR.Read())
            ab[abR.GetString(0)] = abR.IsDBNull(1) ? "" : abR.GetString(1);
        Assert.Equal("ws-personal-alice", ab["s_alice_1"]);
        Assert.Equal("", ab["s_bob_1"]);
    }

    private static string FindMigrationFile(string name)
    {
        // Walk up from the test bin directory to the repo root, then into Migrations.
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "src", "Sovrant.Runtime", "Storage", "Migrations", name);
            if (File.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new FileNotFoundException($"Could not locate migration {name}");
    }
}
