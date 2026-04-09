using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;

namespace Sovrant.Runtime.Tests.Auth;

/// <summary>
/// Phase 38 PR 1 — covers <see cref="SqliteTokenService"/>: issuance, storage
/// hashing, listing, revocation, and resolution. The shared posture across
/// these tests is that the plaintext is only ever surfaced via
/// <see cref="ITokenService.IssueAsync"/> and never readable from the DB.
/// </summary>
public sealed class SqliteTokenServiceTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IUserService _users;
    private readonly ITokenService _tokens;

    public SqliteTokenServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_token_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();

        _users = new SqliteUserStore(
            (ISqliteConnectionFactory)_provider,
            NullLogger<SqliteUserStore>.Instance);

        _tokens = new SqliteTokenService(
            (ISqliteConnectionFactory)_provider,
            NullLogger<SqliteTokenService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── Issue ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Issue_ReturnsPlaintextOnceAndPersistsHashOnly()
    {
        var u = await _users.CreateAsync("alice");

        var result = await _tokens.IssueAsync(u.UserId, name: "laptop", scopes: "read,write");

        Assert.StartsWith("svt_", result.Plaintext, StringComparison.Ordinal);
        Assert.StartsWith("tok_", result.Token.TokenId, StringComparison.Ordinal);
        Assert.Equal(20, result.Token.TokenId.Length);
        Assert.Equal(u.UserId, result.Token.UserId);
        Assert.Equal("laptop", result.Token.Name);
        Assert.Equal("read,write", result.Token.Scopes);
        Assert.Null(result.Token.ExpiresAt);
        Assert.Null(result.Token.RevokedAt);
        Assert.Equal(result.Plaintext[..12], result.Token.TokenPrefix);

        // The persisted row must contain the hash, never the plaintext.
        using var conn = ((ISqliteConnectionFactory)_provider).CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT token_hash FROM api_tokens WHERE token_id = $id";
        cmd.Parameters.AddWithValue("$id", result.Token.TokenId);
        var storedHash = (string)cmd.ExecuteScalar()!;

        Assert.Equal(64, storedHash.Length); // SHA-256 hex
        Assert.NotEqual(result.Plaintext, storedHash);
        Assert.Equal(SqliteTokenService.HashToken(result.Plaintext), storedHash);
    }

    [Fact]
    public async Task Issue_TwiceProducesDistinctTokensAndIds()
    {
        var u = await _users.CreateAsync("bob");

        var a = await _tokens.IssueAsync(u.UserId);
        var b = await _tokens.IssueAsync(u.UserId);

        Assert.NotEqual(a.Plaintext, b.Plaintext);
        Assert.NotEqual(a.Token.TokenId, b.Token.TokenId);
    }

    [Fact]
    public async Task Issue_ForUnknownUser_ThrowsForeignKeyViolation()
    {
        var ex = await Assert.ThrowsAsync<SqliteException>(() =>
            _tokens.IssueAsync("usr_doesnotexist"));
        Assert.Equal(19, ex.SqliteErrorCode); // SQLITE_CONSTRAINT
    }

    [Fact]
    public async Task Issue_RejectsEmptyUserId()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _tokens.IssueAsync(""));
    }

    [Fact]
    public async Task Issue_RejectsExpiresInPast()
    {
        var u = await _users.CreateAsync("past");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _tokens.IssueAsync(u.UserId, expiresAt: DateTimeOffset.UtcNow.AddSeconds(-5)));
    }

    [Fact]
    public async Task Issue_RejectsOversizedNameAndScopes()
    {
        var u = await _users.CreateAsync("oversize");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _tokens.IssueAsync(u.UserId, name: new string('a', 65)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _tokens.IssueAsync(u.UserId, scopes: new string('s', 513)));
    }

    // ── List ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsMaskedTokensExcludingRevoked()
    {
        var u = await _users.CreateAsync("lister");
        var first = await _tokens.IssueAsync(u.UserId, name: "first");
        var second = await _tokens.IssueAsync(u.UserId, name: "second");
        await _tokens.RevokeAsync(first.Token.TokenId);

        var listed = await _tokens.ListAsync(u.UserId);

        Assert.Single(listed);
        Assert.Equal(second.Token.TokenId, listed[0].TokenId);
        Assert.Equal(second.Token.TokenPrefix, listed[0].TokenPrefix);
        Assert.Equal(12, listed[0].TokenPrefix.Length);
    }

    [Fact]
    public async Task List_EmptyForUnknownUser()
    {
        var listed = await _tokens.ListAsync("usr_nope");
        Assert.Empty(listed);
    }

    [Fact]
    public async Task List_OnlyReturnsTokensForRequestedUser()
    {
        var alice = await _users.CreateAsync("scoped-a");
        var bob = await _users.CreateAsync("scoped-b");
        await _tokens.IssueAsync(alice.UserId);
        await _tokens.IssueAsync(bob.UserId);

        var aliceTokens = await _tokens.ListAsync(alice.UserId);
        Assert.Single(aliceTokens);
        Assert.Equal(alice.UserId, aliceTokens[0].UserId);
    }

    // ── Revoke ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_MarksTokenRevoked_PreservesRow()
    {
        var u = await _users.CreateAsync("rev");
        var issued = await _tokens.IssueAsync(u.UserId);

        Assert.True(await _tokens.RevokeAsync(issued.Token.TokenId));

        // Row still exists for audit — verify directly.
        using var conn = ((ISqliteConnectionFactory)_provider).CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT revoked_at FROM api_tokens WHERE token_id = $id";
        cmd.Parameters.AddWithValue("$id", issued.Token.TokenId);
        var revokedAt = cmd.ExecuteScalar();
        Assert.NotNull(revokedAt);
        Assert.NotEqual(DBNull.Value, revokedAt);
    }

    [Fact]
    public async Task Revoke_Idempotent()
    {
        var u = await _users.CreateAsync("rev-twice");
        var issued = await _tokens.IssueAsync(u.UserId);

        Assert.True(await _tokens.RevokeAsync(issued.Token.TokenId));
        Assert.False(await _tokens.RevokeAsync(issued.Token.TokenId));
    }

    [Fact]
    public async Task Revoke_UnknownToken_ReturnsFalse()
    {
        Assert.False(await _tokens.RevokeAsync("tok_doesnotexist"));
    }

    // ── Resolve ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_ValidPlaintext_ReturnsTokenAndRole()
    {
        var u = await _users.CreateAsync("resolver");
        var issued = await _tokens.IssueAsync(u.UserId, name: "cli");

        var resolved = await _tokens.ResolveAsync(issued.Plaintext);

        Assert.NotNull(resolved);
        Assert.Equal(issued.Token.TokenId, resolved.Token.TokenId);
        Assert.Equal(u.UserId, resolved.Token.UserId);
        Assert.Equal("cli", resolved.Token.Name);
        Assert.Equal("user", resolved.Role); // default role from CreateAsync
    }

    [Fact]
    public async Task Resolve_AdminUser_ReturnsAdminRole()
    {
        var u = await _users.CreateAsync("resolver-admin", role: "admin");
        var issued = await _tokens.IssueAsync(u.UserId);

        var resolved = await _tokens.ResolveAsync(issued.Plaintext);

        Assert.NotNull(resolved);
        Assert.Equal("admin", resolved.Role);
    }

    [Fact]
    public async Task Resolve_RevokedToken_ReturnsNull()
    {
        var u = await _users.CreateAsync("resolver-rev");
        var issued = await _tokens.IssueAsync(u.UserId);
        await _tokens.RevokeAsync(issued.Token.TokenId);

        var resolved = await _tokens.ResolveAsync(issued.Plaintext);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_ExpiredToken_ReturnsNull()
    {
        var u = await _users.CreateAsync("resolver-exp");
        var issued = await _tokens.IssueAsync(
            u.UserId,
            expiresAt: DateTimeOffset.UtcNow.AddHours(1));

        // Force the row's expires_at into the past to simulate elapsed time.
        using (var conn = ((ISqliteConnectionFactory)_provider).CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE api_tokens SET expires_at = $past WHERE token_id = $id";
            cmd.Parameters.AddWithValue("$past", "2000-01-01T00:00:00.000Z");
            cmd.Parameters.AddWithValue("$id", issued.Token.TokenId);
            cmd.ExecuteNonQuery();
        }

        var resolved = await _tokens.ResolveAsync(issued.Plaintext);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_TokenForInactiveUser_ReturnsNull()
    {
        var u = await _users.CreateAsync("resolver-inactive");
        var issued = await _tokens.IssueAsync(u.UserId);
        await _users.DeactivateAsync(u.UserId);

        var resolved = await _tokens.ResolveAsync(issued.Plaintext);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_UnknownPlaintext_ReturnsNull()
    {
        var resolved = await _tokens.ResolveAsync("svt_completely-unknown-token-value");
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_RejectsMissingPrefix()
    {
        var resolved = await _tokens.ResolveAsync("not-a-sovrant-token");
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_RejectsEmptyInput()
    {
        Assert.Null(await _tokens.ResolveAsync(""));
        Assert.Null(await _tokens.ResolveAsync(null!));
    }
}
