using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Auth;

/// <summary>
/// SQLite-backed identity service: registration, login, password management,
/// one-time reset tokens, and server-wide registration control.
/// </summary>
internal sealed partial class SqliteIdentityService : IIdentityService
{
    private const string RegistrationOpenKey = "registration_open";
    private const string RequireApprovalKey = "require_approval";
    private const int ResetTokenExpiryHours = 24;
    private const int MinPasswordLength = 8;
    private const string StatusPending = "pending";
    private const string StatusActive = "active";

    private readonly IUserService _users;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IWorkspaceService _workspaces;
    private readonly ISqliteConnectionFactory _db;
    private readonly ILogger<SqliteIdentityService> _logger;

    public SqliteIdentityService(
        IUserService users,
        ITokenService tokens,
        IPasswordHasher hasher,
        IWorkspaceService workspaces,
        ISqliteConnectionFactory db,
        ILogger<SqliteIdentityService> logger)
    {
        _users = users;
        _tokens = tokens;
        _hasher = hasher;
        _workspaces = workspaces;
        _db = db;
        _logger = logger;
    }

    [LoggerMessage(EventId = 8500, Level = LogLevel.Information,
        Message = "User registered: user_id={UserId} role={Role}")]
    private static partial void LogRegistered(ILogger logger, string userId, string role);

    [LoggerMessage(EventId = 8501, Level = LogLevel.Information,
        Message = "User logged in: user_id={UserId}")]
    private static partial void LogLoggedIn(ILogger logger, string userId);

    [LoggerMessage(EventId = 8502, Level = LogLevel.Warning,
        Message = "Login failed for email={Email}: {Reason}")]
    private static partial void LogLoginFailed(ILogger logger, string email, string reason);

    [LoggerMessage(EventId = 8503, Level = LogLevel.Information,
        Message = "Password reset token generated for user_id={UserId}")]
    private static partial void LogResetTokenGenerated(ILogger logger, string userId);

    [LoggerMessage(EventId = 8504, Level = LogLevel.Information,
        Message = "User approved: user_id={UserId}")]
    private static partial void LogUserApproved(ILogger logger, string userId);

    // ── IIdentityService ──────────────────────────────────────────────────

    public async Task<bool> IsFirstRunAsync(CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users";
        var count = (long)(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
        return count == 0;
    }

    public async Task<bool> IsRegistrationOpenAsync(CancellationToken ct = default)
    {
        if (await IsFirstRunAsync(ct).ConfigureAwait(false))
            return true;

        var value = await GetSettingAsync(RegistrationOpenKey, ct).ConfigureAwait(false);
        return value == "1";
    }

    public async Task<bool> IsApprovalRequiredAsync(CancellationToken ct = default)
    {
        var value = await GetSettingAsync(RequireApprovalKey, ct).ConfigureAwait(false);
        // Default: required (null → not yet set → true). Explicitly "0" means disabled.
        return value != "0";
    }

    public Task SetApprovalRequiredAsync(bool required, CancellationToken ct = default)
        => SetSettingAsync(RequireApprovalKey, required ? "1" : "0", ct);

    public async Task<bool> ApproveUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.GetAsync(userId, ct).ConfigureAwait(false);
        if (user is null || user.Status != StatusPending) return false;

        var updated = await _users.UpdateAsync(userId, status: StatusActive, ct: ct).ConfigureAwait(false);
        if (updated is not null)
            LogUserApproved(_logger, userId);
        return updated is not null;
    }

    public async Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return new RegisterResult(false, null, null, null, "Email is required.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            return new RegisterResult(false, null, null, null, $"Password must be at least {MinPasswordLength} characters.");

        var isFirst = await IsFirstRunAsync(ct).ConfigureAwait(false);
        if (!isFirst && !await IsRegistrationOpenAsync(ct).ConfigureAwait(false))
            return new RegisterResult(false, null, null, null, "Registration is closed.");

        var role = isFirst ? "admin" : "user";

        User user;
        try
        {
            user = await _users.CreateAsync(email, role, ct: ct).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT (unique)
        {
            return new RegisterResult(false, null, null, null, "An account with that email already exists.");
        }

        // Store password hash and create personal workspace for every new user.
        await SetPasswordHashAsync(user.UserId, _hasher.Hash(password), ct).ConfigureAwait(false);
        await _workspaces.CreatePersonalWorkspaceAsync(user.UserId, ct).ConfigureAwait(false);

        // First user is immediately active admin; registration closes.
        if (isFirst)
        {
            await SetSettingAsync(RegistrationOpenKey, "0", ct).ConfigureAwait(false);

            var issued = await _tokens.IssueAsync(
                user.UserId,
                name: "login",
                expiresAt: DateTimeOffset.UtcNow.AddDays(30),
                ct: ct).ConfigureAwait(false);

            LogRegistered(_logger, user.UserId, role);
            return new RegisterResult(true, issued.Plaintext, user.UserId, role, null);
        }

        // Subsequent registrations: check approval requirement.
        if (await IsApprovalRequiredAsync(ct).ConfigureAwait(false))
        {
            // Hold account in pending state — no token issued.
            await _users.UpdateAsync(user.UserId, status: StatusPending, ct: ct).ConfigureAwait(false);
            LogRegistered(_logger, user.UserId, role);
            return new RegisterResult(true, null, user.UserId, role, null, IsPendingApproval: true);
        }

        // Approval not required — activate immediately and issue token.
        var directIssued = await _tokens.IssueAsync(
            user.UserId,
            name: "login",
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            ct: ct).ConfigureAwait(false);

        LogRegistered(_logger, user.UserId, role);
        return new RegisterResult(true, directIssued.Plaintext, user.UserId, role, null);
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            LogLoginFailed(_logger, email ?? "", "missing credentials");
            return new LoginResult(false, null, null, null, "Email and password are required.");
        }

        var user = await _users.GetByEmailAsync(email, ct).ConfigureAwait(false);
        if (user is null)
        {
            LogLoginFailed(_logger, email, "user not found");
            return new LoginResult(false, null, null, null, "Invalid email or password.");
        }
        if (user.Status == StatusPending)
        {
            LogLoginFailed(_logger, email, "pending approval");
            return new LoginResult(false, null, null, null, "Your account is pending admin approval.");
        }
        if (user.Status != StatusActive)
        {
            LogLoginFailed(_logger, email, $"account {user.Status}");
            return new LoginResult(false, null, null, null, "Your account has been disabled. Contact an administrator.");
        }

        var storedHash = await GetPasswordHashAsync(user.UserId, ct).ConfigureAwait(false);
        if (storedHash is null || !_hasher.Verify(password, storedHash))
        {
            LogLoginFailed(_logger, email, "wrong password");
            return new LoginResult(false, null, null, null, "Invalid email or password.");
        }

        var issued = await _tokens.IssueAsync(
            user.UserId,
            name: "login",
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            ct: ct).ConfigureAwait(false);

        LogLoggedIn(_logger, user.UserId);
        return new LoginResult(true, issued.Plaintext, user.UserId, user.Role, null);
    }

    public Task LogoutAsync(string tokenId, CancellationToken ct = default)
        => _tokens.RevokeAsync(tokenId, ct);

    public async Task ChangePasswordAsync(
        string userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            throw new ArgumentException($"New password must be at least {MinPasswordLength} characters.");

        var stored = await GetPasswordHashAsync(userId, ct).ConfigureAwait(false);
        if (stored is null || !_hasher.Verify(currentPassword, stored))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        await SetPasswordHashAsync(userId, _hasher.Hash(newPassword), ct).ConfigureAwait(false);
    }

    public async Task<string> GenerateResetTokenAsync(string targetUserId, CancellationToken ct = default)
    {
        // Expire any existing unused tokens for this user
        using var connection = _db.CreateConnection();
        using var expireCmd = connection.CreateCommand();
        expireCmd.CommandText = """
            UPDATE password_reset_tokens
            SET used_at = $now
            WHERE user_id = $uid AND used_at IS NULL
            """;
        expireCmd.Parameters.AddWithValue("$uid", targetUserId);
        expireCmd.Parameters.AddWithValue("$now", Ts(DateTimeOffset.UtcNow));
        await expireCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Generate new reset token
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var plaintext = "prt_" + Convert.ToHexStringLower(bytes);
        var hash = HashResetToken(plaintext);
        var tokenId = "prt_" + GenerateShortId();
        var expires = DateTimeOffset.UtcNow.AddHours(ResetTokenExpiryHours);

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO password_reset_tokens (token_id, user_id, token_hash, expires_at)
            VALUES ($id, $uid, $hash, $expires)
            """;
        insertCmd.Parameters.AddWithValue("$id", tokenId);
        insertCmd.Parameters.AddWithValue("$uid", targetUserId);
        insertCmd.Parameters.AddWithValue("$hash", hash);
        insertCmd.Parameters.AddWithValue("$expires", Ts(expires));
        await insertCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        LogResetTokenGenerated(_logger, targetUserId);
        return plaintext;
    }

    public async Task<ResetPasswordResult> UseResetTokenAsync(
        string plaintextToken, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken))
            return new ResetPasswordResult(false, "Reset token is required.");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            return new ResetPasswordResult(false, $"New password must be at least {MinPasswordLength} characters.");

        var hash = HashResetToken(plaintextToken);
        var now = DateTimeOffset.UtcNow;

        using var connection = _db.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT token_id, user_id, expires_at, used_at
            FROM password_reset_tokens
            WHERE token_hash = $hash
            """;
        cmd.Parameters.AddWithValue("$hash", hash);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return new ResetPasswordResult(false, "Invalid or expired reset token.");

        var tokenId = reader.GetString(0);
        var userId = reader.GetString(1);
        var expiresAt = ParseTs(reader.GetString(2));
        var usedAt = await reader.IsDBNullAsync(3, ct).ConfigureAwait(false)
            ? (DateTimeOffset?)null
            : ParseTs(reader.GetString(3));
        await reader.CloseAsync().ConfigureAwait(false);

        if (usedAt.HasValue || expiresAt <= now)
            return new ResetPasswordResult(false, "Invalid or expired reset token.");

        // Mark used
        using var markCmd = connection.CreateCommand();
        markCmd.CommandText = "UPDATE password_reset_tokens SET used_at = $now WHERE token_id = $id";
        markCmd.Parameters.AddWithValue("$id", tokenId);
        markCmd.Parameters.AddWithValue("$now", Ts(now));
        await markCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await SetPasswordHashAsync(userId, _hasher.Hash(newPassword), ct).ConfigureAwait(false);
        return new ResetPasswordResult(true, null);
    }

    public Task SetRegistrationOpenAsync(bool open, CancellationToken ct = default)
        => SetSettingAsync(RegistrationOpenKey, open ? "1" : "0", ct);

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<string?> GetPasswordHashAsync(string userId, CancellationToken ct)
    {
        using var connection = _db.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT password_hash FROM users WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$id", userId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is DBNull or null ? null : (string)result;
    }

    private async Task SetPasswordHashAsync(string userId, string hash, CancellationToken ct)
    {
        using var connection = _db.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET password_hash = $hash, updated_at = $now WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$now", Ts(DateTimeOffset.UtcNow));
        cmd.Parameters.AddWithValue("$id", userId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct)
    {
        using var connection = _db.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM server_settings WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is DBNull or null ? null : (string)result;
    }

    private async Task SetSettingAsync(string key, string value, CancellationToken ct)
    {
        using var connection = _db.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO server_settings (key, value, updated_at)
            VALUES ($key, $value, $now)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$now", Ts(DateTimeOffset.UtcNow));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static string HashResetToken(string plaintext)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(plaintext), hash);
        return Convert.ToHexStringLower(hash);
    }

    private static string Ts(DateTimeOffset dt) =>
        dt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTs(string s) =>
        DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

    private static string GenerateShortId()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
