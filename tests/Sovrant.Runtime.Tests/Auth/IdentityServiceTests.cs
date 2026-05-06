using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Auth;

public sealed class IdentityServiceTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IIdentityService _identity;

    public IdentityServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_identity_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();

        // Clear the legacy seeded user so identity tests start from a true first-run state.
        // Temporarily disable FK constraints so dependent rows don't block the purge.
        using (var conn = ((ISqliteConnectionFactory)_provider).CreateConnection())
        {
            using var fkOff = conn.CreateCommand();
            fkOff.CommandText = "PRAGMA foreign_keys = OFF";
            fkOff.ExecuteNonQuery();

            foreach (var table in new[] { "workspaces", "api_tokens", "password_reset_tokens", "users" })
            {
                using var del = conn.CreateCommand();
                del.CommandText = $"DELETE FROM {table}";
                del.ExecuteNonQuery();
            }

            using var fkOn = conn.CreateCommand();
            fkOn.CommandText = "PRAGMA foreign_keys = ON";
            fkOn.ExecuteNonQuery();
        }

        var db = (ISqliteConnectionFactory)_provider;
        var users = new SqliteUserStore(db, NullLogger<SqliteUserStore>.Instance);
        var tokens = new SqliteTokenService(db, NullLogger<SqliteTokenService>.Instance);
        var hasher = new Argon2idPasswordHasher();
        var workspaces = new SqliteWorkspaceStore(db);

        _identity = new SqliteIdentityService(
            users, tokens, hasher, workspaces, db,
            NullLogger<SqliteIdentityService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── First user (admin) ────────────────────────────────────────────────

    [Fact]
    public async Task Register_FirstUser_BecomesAdminAndGetsToken()
    {
        var result = await _identity.RegisterAsync("admin@example.com", "password123");

        Assert.True(result.Success, $"Registration failed: {result.Error}");
        Assert.NotNull(result.Token);
        Assert.StartsWith("svt_", result.Token, StringComparison.Ordinal);
        Assert.False(result.IsPendingApproval);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Register_FirstUser_ClosesRegistration()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");

        var isOpen = await _identity.IsRegistrationOpenAsync();
        Assert.False(isOpen);
    }

    [Fact]
    public async Task IsFirstRun_TrueBeforeAnyUsers()
    {
        Assert.True(await _identity.IsFirstRunAsync());
    }

    // ── Registration gate ─────────────────────────────────────────────────

    [Fact]
    public async Task Register_SecondUser_FailsWhenRegistrationClosed()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");

        var result = await _identity.RegisterAsync("user@example.com", "password456");

        Assert.False(result.Success);
        Assert.Contains("closed", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_SecondUser_PendingWhenApprovalRequired()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");
        await _identity.SetRegistrationOpenAsync(true);

        var result = await _identity.RegisterAsync("user@example.com", "password456");

        Assert.True(result.Success);
        Assert.True(result.IsPendingApproval);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task Register_SecondUser_GetsTokenWhenApprovalNotRequired()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");
        await _identity.SetRegistrationOpenAsync(true);
        await _identity.SetApprovalRequiredAsync(false);

        var result = await _identity.RegisterAsync("user@example.com", "password456");

        Assert.True(result.Success);
        Assert.False(result.IsPendingApproval);
        Assert.NotNull(result.Token);
    }

    // ── Login ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_SucceedsWithCorrectCredentials()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");

        var result = await _identity.LoginAsync("admin@example.com", "password123");

        Assert.True(result.Success);
        Assert.NotNull(result.Token);
        Assert.StartsWith("svt_", result.Token, StringComparison.Ordinal);
        Assert.Equal("admin", result.Role);
    }

    [Fact]
    public async Task Login_FailsWithWrongPassword()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");

        var result = await _identity.LoginAsync("admin@example.com", "wrongpassword");

        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Login_FailsForPendingUser()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");
        await _identity.SetRegistrationOpenAsync(true);
        await _identity.RegisterAsync("user@example.com", "password456");

        var result = await _identity.LoginAsync("user@example.com", "password456");

        Assert.False(result.Success);
        Assert.Contains("pending", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Approval ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveUser_AllowsLogin()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");
        await _identity.SetRegistrationOpenAsync(true);
        var reg = await _identity.RegisterAsync("user@example.com", "password456");

        var approved = await _identity.ApproveUserAsync(reg.UserId!);
        Assert.True(approved);

        var login = await _identity.LoginAsync("user@example.com", "password456");
        Assert.True(login.Success);
    }

    [Fact]
    public async Task ApproveUser_ReturnsFalseIfNotPending()
    {
        var reg = await _identity.RegisterAsync("admin@example.com", "password123");

        var approved = await _identity.ApproveUserAsync(reg.UserId!);
        Assert.False(approved);
    }

    // ── Password reset ────────────────────────────────────────────────────

    [Fact]
    public async Task ResetToken_CanChangePassword()
    {
        var reg = await _identity.RegisterAsync("admin@example.com", "password123");

        var token = await _identity.GenerateResetTokenAsync(reg.UserId!);
        Assert.StartsWith("prt_", token, StringComparison.Ordinal);

        var resetResult = await _identity.UseResetTokenAsync(token, "newpassword456");
        Assert.True(resetResult.Success);

        var login = await _identity.LoginAsync("admin@example.com", "newpassword456");
        Assert.True(login.Success);
    }

    [Fact]
    public async Task ResetToken_CanOnlyBeUsedOnce()
    {
        var reg = await _identity.RegisterAsync("admin@example.com", "password123");
        var token = await _identity.GenerateResetTokenAsync(reg.UserId!);

        await _identity.UseResetTokenAsync(token, "newpassword456");
        var secondUse = await _identity.UseResetTokenAsync(token, "anotherpassword789");

        Assert.False(secondUse.Success);
        Assert.NotNull(secondUse.Error);
    }

    [Fact]
    public async Task UseResetToken_FailsForInvalidToken()
    {
        var result = await _identity.UseResetTokenAsync("prt_notavalidtoken", "newpassword456");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    // ── Validation ────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_RejectsShortPassword()
    {
        var result = await _identity.RegisterAsync("admin@example.com", "short");

        Assert.False(result.Success);
        Assert.Contains("8", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        await _identity.RegisterAsync("admin@example.com", "password123");
        await _identity.SetRegistrationOpenAsync(true);
        await _identity.SetApprovalRequiredAsync(false);

        var second = await _identity.RegisterAsync("admin@example.com", "password456");

        Assert.False(second.Success);
        Assert.Contains("already exists", second.Error, StringComparison.OrdinalIgnoreCase);
    }
}
