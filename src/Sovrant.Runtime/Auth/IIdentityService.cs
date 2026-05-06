namespace Sovrant.Runtime.Auth;

/// <summary>Results returned by <see cref="IIdentityService"/> operations.</summary>
public sealed record RegisterResult(bool Success, string? Token, string? UserId, string? Error);
public sealed record LoginResult(bool Success, string? Token, string? UserId, string? Role, string? Error);
public sealed record ResetPasswordResult(bool Success, string? Error);

/// <summary>
/// Unified identity service: registration, login, password management,
/// and server-wide registration control.
/// </summary>
public interface IIdentityService
{
    /// <summary>Returns true when no users exist — first-run registration is allowed regardless of open/closed state.</summary>
    Task<bool> IsFirstRunAsync(CancellationToken ct = default);

    /// <summary>Returns true when registration is open (first run OR admin has explicitly opened it).</summary>
    Task<bool> IsRegistrationOpenAsync(CancellationToken ct = default);

    /// <summary>
    /// Registers a new user. On first call, the account is made admin and registration closes.
    /// On subsequent calls, fails unless registration is open.
    /// Returns a 30-day sliding svt_ token on success.
    /// </summary>
    Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Authenticates with email + password. Returns a 30-day sliding svt_ token on success.</summary>
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Revokes the token that authenticated the current request.</summary>
    Task LogoutAsync(string tokenId, CancellationToken ct = default);

    /// <summary>Changes the calling user's password after verifying the current one.</summary>
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>Admin only: generates a 24-hour one-time reset token for <paramref name="targetUserId"/>. Returns plaintext token.</summary>
    Task<string> GenerateResetTokenAsync(string targetUserId, CancellationToken ct = default);

    /// <summary>Consumes a one-time reset token and sets a new password.</summary>
    Task<ResetPasswordResult> UseResetTokenAsync(string plaintextToken, string newPassword, CancellationToken ct = default);

    /// <summary>Opens or closes self-registration. Admin only.</summary>
    Task SetRegistrationOpenAsync(bool open, CancellationToken ct = default);
}
