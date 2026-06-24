using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;

namespace Sovrant.Runtime.Tests.Users;

public sealed class SqliteUserStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IUserService _users;

    public SqliteUserStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_user_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();

        _users = new SqliteUserStore(
            (ISqliteConnectionFactory)_provider,
            NullLogger<SqliteUserStore>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── CRUD ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithEmail_UsesEmailAsUserId()
    {
        var u = await _users.CreateAsync(email: "alice@example.com", role: "user", team: "eng");

        // V043: email IS the user_id for auth-registered users.
        Assert.Equal("alice@example.com", u.UserId);
        Assert.Equal("alice@example.com", u.Email);
        Assert.Equal("user", u.Role);
        Assert.Equal("eng", u.Team);
        Assert.Equal("active", u.Status);

        var loaded = await _users.GetAsync(u.UserId);
        Assert.NotNull(loaded);
        Assert.Equal("alice@example.com", loaded.UserId);
    }

    [Fact]
    public async Task Create_WithoutEmail_GeneratesUsrHexId()
    {
        var u = await _users.CreateAsync(role: "user");

        Assert.StartsWith("usr_", u.UserId, StringComparison.Ordinal);
        Assert.Equal(20, u.UserId.Length); // "usr_" + 16 hex chars
        Assert.Null(u.Email);
    }

    [Fact]
    public async Task Create_WithExplicitUserId_UsesProvidedId()
    {
        var u = await _users.CreateAsync(userId: "os-username");

        Assert.Equal("os-username", u.UserId);
    }

    [Fact]
    public async Task Create_RejectsInvalidEmail()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _users.CreateAsync(email: "not-an-email"));
        await Assert.ThrowsAsync<ArgumentException>(() => _users.CreateAsync(email: "@example.com"));
        await Assert.ThrowsAsync<ArgumentException>(() => _users.CreateAsync(email: "bob@"));
    }

    [Fact]
    public async Task Create_RejectsInvalidRole()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _users.CreateAsync(role: "superuser"));
        await Assert.ThrowsAsync<ArgumentException>(() => _users.CreateAsync(role: ""));
    }

    [Fact]
    public async Task Create_DuplicateEmail_Throws409Material()
    {
        await _users.CreateAsync(email: "shared@example.com");
        var ex = await Assert.ThrowsAsync<SqliteException>(() =>
            _users.CreateAsync(email: "shared@example.com"));
        Assert.Equal(19, ex.SqliteErrorCode); // SQLITE_CONSTRAINT
    }

    [Fact]
    public async Task GetProfile_IncludesDerivedStats()
    {
        var u = await _users.CreateAsync(email: "statsy@example.com");
        var profile = await _users.GetProfileAsync(u.UserId);

        Assert.NotNull(profile);
        Assert.Equal(u.UserId, profile.UserId);
        Assert.Equal(0, profile.SessionCount);
        Assert.Equal(0, profile.TotalInputTokens);
        Assert.Null(profile.LastSeenAt);
    }

    [Fact]
    public async Task GetProfile_NonExistent_ReturnsNull()
    {
        var p = await _users.GetProfileAsync("usr_doesnotexist");
        Assert.Null(p);
    }

    // ── List & filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var a = await _users.CreateAsync(email: "filter-a@example.com");
        var b = await _users.CreateAsync(email: "filter-b@example.com");
        await _users.DeactivateAsync(a.UserId);

        var actives = await _users.ListAsync(new UserListFilter { Status = "active" });
        var inactives = await _users.ListAsync(new UserListFilter { Status = "inactive" });

        Assert.Contains(actives, u => u.UserId == b.UserId);
        Assert.DoesNotContain(actives, u => u.UserId == a.UserId);
        Assert.Contains(inactives, u => u.UserId == a.UserId);
    }

    [Fact]
    public async Task List_FiltersByTeam()
    {
        await _users.CreateAsync(email: "team-eng-1@x.com", team: "eng-team-list");
        await _users.CreateAsync(email: "team-ops-1@x.com", team: "ops-team-list");

        var eng = await _users.ListAsync(new UserListFilter { Team = "eng-team-list" });
        Assert.Contains(eng, u => u.UserId == "team-eng-1@x.com");
        Assert.DoesNotContain(eng, u => u.UserId == "team-ops-1@x.com");
    }

    [Fact]
    public async Task List_RejectsInvalidFilter()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _users.ListAsync(new UserListFilter { Status = "deleted" }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _users.ListAsync(new UserListFilter { Limit = 0 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _users.ListAsync(new UserListFilter { Limit = 5000 }));
    }

    [Fact]
    public async Task Count_RespectsFilter()
    {
        await _users.CreateAsync(email: "count-a@x.com", role: "user");
        await _users.CreateAsync(email: "count-b@x.com", role: "admin");

        var userCount = await _users.CountAsync(new UserListFilter { Role = "user" });
        var adminCount = await _users.CountAsync(new UserListFilter { Role = "admin" });

        Assert.True(userCount >= 1);
        Assert.True(adminCount >= 1);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesOnlyProvidedFields()
    {
        var u = await _users.CreateAsync(email: "upd-a@example.com", role: "user", team: "old");
        var updated = await _users.UpdateAsync(u.UserId, email: "new@example.com", team: "new");

        Assert.NotNull(updated);
        Assert.Equal("user", updated.Role);       // unchanged
        Assert.Equal("new@example.com", updated.Email);
        Assert.Equal("new", updated.Team);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNull()
    {
        var result = await _users.UpdateAsync("usr_nope", email: "x@x.com");
        Assert.Null(result);
    }

    [Fact]
    public async Task Update_RejectsInvalidStatus()
    {
        var u = await _users.CreateAsync(email: "upd-bad@x.com");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _users.UpdateAsync(u.UserId, status: "banned"));
    }

    // ── Soft-delete (deactivate) ───────────────────────────────────────────

    [Fact]
    public async Task Deactivate_FlipsStatus_PreservesRow()
    {
        var u = await _users.CreateAsync(email: "deactivate-me@x.com");
        var ok = await _users.DeactivateAsync(u.UserId);
        Assert.True(ok);

        var loaded = await _users.GetAsync(u.UserId);
        Assert.NotNull(loaded);
        Assert.Equal("inactive", loaded.Status);
    }

    [Fact]
    public async Task Deactivate_Idempotent()
    {
        var u = await _users.CreateAsync(email: "dz-twice@x.com");
        Assert.True(await _users.DeactivateAsync(u.UserId));
        Assert.False(await _users.DeactivateAsync(u.UserId));
    }

    [Fact]
    public async Task Deactivate_NonExistent_ReturnsFalse()
    {
        Assert.False(await _users.DeactivateAsync("usr_nope"));
    }

    [Fact]
    public async Task Reactivate_RestoresStatus()
    {
        var u = await _users.CreateAsync(email: "react-me@x.com");
        await _users.DeactivateAsync(u.UserId);

        var ok = await _users.ReactivateAsync(u.UserId);
        Assert.True(ok);

        var loaded = await _users.GetAsync(u.UserId);
        Assert.NotNull(loaded);
        Assert.Equal("active", loaded.Status);
    }

    [Fact]
    public async Task Deactivate_PreservesFkReferences()
    {
        // Create a user, then create a workspace owned by them, deactivate,
        // and prove the workspace's owner row still resolves.
        var u = await _users.CreateAsync(email: "owner-x@x.com");

        using (var conn = ((ISqliteConnectionFactory)_provider).CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO workspaces (workspace_id, type, name, slug, owner_id, created_at, updated_at)
                VALUES ($wid, 'team', 'X', $slug, $oid,
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                        strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                """;
            cmd.Parameters.AddWithValue("$wid", "ws-pres-x");
            cmd.Parameters.AddWithValue("$slug", "pres-x");
            cmd.Parameters.AddWithValue("$oid", u.UserId);
            cmd.ExecuteNonQuery();
        }

        await _users.DeactivateAsync(u.UserId);

        // The user row still exists (soft delete), so the FK is still valid.
        var loaded = await _users.GetAsync(u.UserId);
        Assert.NotNull(loaded);
        Assert.Equal("inactive", loaded.Status);

        using var verify = ((ISqliteConnectionFactory)_provider).CreateConnection();
        using var check = verify.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM workspaces WHERE owner_id = $oid";
        check.Parameters.AddWithValue("$oid", u.UserId);
        var count = Convert.ToInt32(check.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(1, count);
    }

    // ── Per-user data views ────────────────────────────────────────────────

    [Fact]
    public async Task ListSessions_EmptyForFreshUser()
    {
        var u = await _users.CreateAsync(email: "no-sessions@x.com");
        var sessions = await _users.ListSessionsAsync(u.UserId);
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task GetUsage_ReturnsZerosForFreshUser()
    {
        var u = await _users.CreateAsync(email: "no-usage@x.com");
        var usage = await _users.GetUsageAsync(u.UserId);

        Assert.Equal(0, usage.TotalInputTokens);
        Assert.Equal(0, usage.TotalOutputTokens);
        Assert.Equal(0, usage.SessionCount);
        Assert.Empty(usage.ByModel);
    }

    [Fact]
    public async Task GetUsage_AggregatesTokenUsageRows()
    {
        var u = await _users.CreateAsync(email: "with-usage@x.com");

        // Insert two token_usage rows directly.
        using (var conn = ((ISqliteConnectionFactory)_provider).CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO token_usage (session_id, user_id, model, input_tokens, output_tokens, cost_usd)
                VALUES ('s1', $uid, 'gpt-4o', 100, 50, 0.01),
                       ('s2', $uid, 'gpt-4o', 200, 75, 0.02),
                       ('s3', $uid, 'claude-opus', 300, 100, 0.05)
                """;
            cmd.Parameters.AddWithValue("$uid", u.UserId);
            cmd.ExecuteNonQuery();
        }

        var usage = await _users.GetUsageAsync(u.UserId);
        Assert.Equal(600, usage.TotalInputTokens);
        Assert.Equal(225, usage.TotalOutputTokens);
        Assert.Equal(2, usage.ByModel.Count);
    }

    [Fact]
    public async Task GetUsage_FiltersByModel()
    {
        var u = await _users.CreateAsync(email: "filter-usage@x.com");

        using (var conn = ((ISqliteConnectionFactory)_provider).CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO token_usage (session_id, user_id, model, input_tokens, output_tokens, cost_usd)
                VALUES ('s1', $uid, 'gpt-4o', 100, 50, 0.01),
                       ('s2', $uid, 'claude-opus', 999, 999, 9.99)
                """;
            cmd.Parameters.AddWithValue("$uid", u.UserId);
            cmd.ExecuteNonQuery();
        }

        var only4o = await _users.GetUsageAsync(u.UserId, model: "gpt-4o");
        Assert.Equal(100, only4o.TotalInputTokens);
        Assert.Single(only4o.ByModel);
    }

    [Fact]
    public async Task ListAuditEvents_EmptyWhenNoSessions()
    {
        var u = await _users.CreateAsync(email: "no-audit@x.com");
        var events = await _users.ListAuditEventsAsync(u.UserId);
        Assert.Empty(events);
    }
}
