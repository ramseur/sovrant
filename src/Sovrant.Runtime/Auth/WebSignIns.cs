using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Auth;

/// <summary>
/// Phase 145 — how long a Web sign-in lasts. Signed out after <see cref="IdleTimeout"/> without
/// activity (renewed by activity) and after <see cref="AbsoluteLifetime"/> regardless; "Keep me
/// signed in" lasts <see cref="RememberLifetime"/> instead (no idle limit). A zero
/// <see cref="RememberLifetime"/> turns "Keep me signed in" off.
/// </summary>
public sealed record WebSignInPolicy(TimeSpan IdleTimeout, TimeSpan AbsoluteLifetime, TimeSpan RememberLifetime)
{
    public const string IdleMinutesVariable = "SOVRANT_WEB_IDLE_MINUTES";
    public const string MaxSessionHoursVariable = "SOVRANT_WEB_MAX_SESSION_HOURS";
    public const string RememberDaysVariable = "SOVRANT_WEB_REMEMBER_DAYS";

    /// <summary>1 hour idle, 12 hours at most, 30 days with "Keep me signed in".</summary>
    public static WebSignInPolicy Default { get; } = new(TimeSpan.FromHours(1), TimeSpan.FromHours(12), TimeSpan.FromDays(30));

    public bool RememberAllowed => RememberLifetime > TimeSpan.Zero;

    /// <summary>Reads the three limits from the environment; invalid or missing values keep the defaults.</summary>
    public static WebSignInPolicy FromEnvironment(Func<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(env);
        var idle = Read(env, IdleMinutesVariable, min: 1);
        var max = Read(env, MaxSessionHoursVariable, min: 1);
        var remember = Read(env, RememberDaysVariable, min: 0);
        return new WebSignInPolicy(
            idle is { } i ? TimeSpan.FromMinutes(i) : Default.IdleTimeout,
            max is { } m ? TimeSpan.FromHours(m) : Default.AbsoluteLifetime,
            remember is { } r ? TimeSpan.FromDays(r) : Default.RememberLifetime);
    }

    private static double? Read(Func<string, string?> env, string name, double min) =>
        double.TryParse(env(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= min ? v : null;
}

/// <summary>One browser a person is signed in on.</summary>
public sealed record WebSignIn(
    string SignInId,
    string UserId,
    bool Remember,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActiveAt,
    DateTimeOffset ExpiresAt);

/// <summary>Display text for sign-ins, shared by Web and Desktop admin pages.</summary>
public static class WebSignInText
{
    /// <summary>"Chrome on Windows"-style label for a User-Agent, for Admin → Users and the account menu.</summary>
    public static string DescribeBrowser(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown browser";
        var ua = userAgent;
        var browser = ua.Contains("Edg/", StringComparison.Ordinal) ? "Edge"
            : ua.Contains("OPR/", StringComparison.Ordinal) ? "Opera"
            : ua.Contains("Firefox/", StringComparison.Ordinal) ? "Firefox"
            : ua.Contains("Chrome/", StringComparison.Ordinal) || ua.Contains("CriOS/", StringComparison.Ordinal) ? "Chrome"
            : ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari"
            : "Browser";
        var os = ua.Contains("iPhone", StringComparison.Ordinal) ? "iPhone"
            : ua.Contains("iPad", StringComparison.Ordinal) ? "iPad"
            : ua.Contains("Android", StringComparison.Ordinal) ? "Android"
            : ua.Contains("Windows", StringComparison.Ordinal) ? "Windows"
            : ua.Contains("Mac OS X", StringComparison.Ordinal) ? "Mac"
            : ua.Contains("Linux", StringComparison.Ordinal) ? "Linux"
            : null;
        return os is null ? browser : $"{browser} on {os}";
    }
}

/// <summary>Why a presented sign-in token isn't valid (or <see cref="Valid"/>).</summary>
public enum WebSignInStatus { Valid, Unknown, TimedOut, Revoked }

/// <summary>The result of checking a sign-in token.</summary>
public sealed record WebSignInCheck(WebSignInStatus Status, WebSignIn? SignIn = null, string? Role = null, string? RevokedReason = null)
{
    public bool IsValid => Status == WebSignInStatus.Valid;
}

/// <summary>Reasons recorded when a sign-in is ended.</summary>
public static class WebSignInRevokeReasons
{
    public const string SignOut = "signout";
    public const string SignOutEverywhere = "signout_all";
    public const string Admin = "admin";
}

/// <summary>Phase 145 — per-browser Web sign-ins (V049 <c>web_sign_ins</c>).</summary>
public interface IWebSignInService
{
    /// <summary>Creates a sign-in and returns it with the plaintext cookie token (returned once, never stored).</summary>
    Task<(WebSignIn SignIn, string Token)> CreateAsync(string userId, bool remember, string? userAgent, string? ipAddress, WebSignInPolicy policy, CancellationToken ct = default);

    /// <summary>
    /// Checks a token. When <paramref name="touch"/> is true and the sign-in is valid, records activity
    /// (at most once a minute), which renews the idle timeout.
    /// </summary>
    Task<WebSignInCheck> CheckAsync(string token, WebSignInPolicy policy, bool touch, CancellationToken ct = default);

    /// <summary>
    /// Checks a sign-in by its id without recording activity: an open Web tab uses this to notice that
    /// its sign-in has ended (timed out, revoked, or signed out in another tab).
    /// </summary>
    Task<WebSignInCheck> CheckByIdAsync(string signInId, WebSignInPolicy policy, CancellationToken ct = default);

    /// <summary>A person's active (not revoked, not expired) sign-ins, most recently active first.</summary>
    Task<IReadOnlyList<WebSignIn>> ListActiveAsync(string userId, WebSignInPolicy policy, CancellationToken ct = default);

    /// <summary>Everyone's active sign-ins in one query (Admin → Users), most recently active first.</summary>
    Task<IReadOnlyList<WebSignIn>> ListAllActiveAsync(WebSignInPolicy policy, CancellationToken ct = default);

    /// <summary>Ends one sign-in. Returns false if it didn't exist or had already ended.</summary>
    Task<bool> RevokeAsync(string signInId, string reason, CancellationToken ct = default);

    /// <summary>Ends all of a person's sign-ins (optionally keeping one) and returns how many ended.</summary>
    Task<int> RevokeAllAsync(string userId, string reason, string? exceptSignInId = null, CancellationToken ct = default);
}

internal sealed class SqliteWebSignInService(ISqliteConnectionFactory connectionFactory, TimeProvider? clock = null) : IWebSignInService
{
    private const string TokenPrefix = "sws_";
    private const string Columns = "s.sign_in_id, s.user_id, s.remember, s.user_agent, s.ip_address, s.created_at, s.last_active_at, s.expires_at";
    private const string CheckSelect = "SELECT " + Columns + ", s.revoked_at, s.revoked_reason, u.role, u.status FROM web_sign_ins s INNER JOIN users u ON u.user_id = s.user_id ";
    private const string CheckByTokenSql = CheckSelect + "WHERE s.token_hash = $key";
    private const string CheckByIdSql = CheckSelect + "WHERE s.sign_in_id = $key";
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<(WebSignIn SignIn, string Token)> CreateAsync(string userId, bool remember, string? userAgent, string? ipAddress, WebSignInPolicy policy, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentNullException.ThrowIfNull(policy);
        remember &= policy.RememberAllowed;
        var now = _clock.GetUtcNow();
        var token = TokenPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var signIn = new WebSignIn(
            SignInId: "wsi-" + Guid.NewGuid().ToString("N"),
            UserId: userId,
            Remember: remember,
            UserAgent: Trim(userAgent, 300),
            IpAddress: Trim(ipAddress, 64),
            CreatedAt: now,
            LastActiveAt: now,
            ExpiresAt: now + (remember ? policy.RememberLifetime : policy.AbsoluteLifetime));

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO web_sign_ins (sign_in_id, user_id, token_hash, remember, user_agent, ip_address, created_at, last_active_at, expires_at)
            VALUES ($id, $uid, $hash, $remember, $ua, $ip, $now, $now, $expires)
            """;
        cmd.Parameters.AddWithValue("$id", signIn.SignInId);
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$hash", Hash(token));
        cmd.Parameters.AddWithValue("$remember", remember ? 1 : 0);
        cmd.Parameters.AddWithValue("$ua", (object?)signIn.UserAgent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ip", (object?)signIn.IpAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$now", Ts(now));
        cmd.Parameters.AddWithValue("$expires", Ts(signIn.ExpiresAt));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return (signIn, token);
    }

    public Task<WebSignInCheck> CheckAsync(string token, WebSignInPolicy policy, bool touch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrEmpty(token) || !token.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return Task.FromResult(new WebSignInCheck(WebSignInStatus.Unknown));
        return CheckCoreAsync(byId: false, Hash(token), policy, touch, ct);
    }

    public Task<WebSignInCheck> CheckByIdAsync(string signInId, WebSignInPolicy policy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return string.IsNullOrEmpty(signInId)
            ? Task.FromResult(new WebSignInCheck(WebSignInStatus.Unknown))
            : CheckCoreAsync(byId: true, signInId, policy, touch: false, ct);
    }

    private async Task<WebSignInCheck> CheckCoreAsync(bool byId, string key, WebSignInPolicy policy, bool touch, CancellationToken ct)
    {
        using var connection = connectionFactory.CreateConnection();
        WebSignIn signIn;
        string role, userStatus;
        string? revokedReason;
        bool revoked;
        using (var cmd = connection.CreateCommand())
        {
            if (byId)
                cmd.CommandText = CheckByIdSql;
            else
                cmd.CommandText = CheckByTokenSql;
            cmd.Parameters.AddWithValue("$key", key);
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                return new WebSignInCheck(WebSignInStatus.Unknown);
            signIn = Read(reader);
            revoked = !await reader.IsDBNullAsync(8, ct).ConfigureAwait(false);
            revokedReason = await reader.IsDBNullAsync(9, ct).ConfigureAwait(false) ? null : reader.GetString(9);
            role = reader.GetString(10);
            userStatus = reader.GetString(11);
        }

        if (revoked)
            return new WebSignInCheck(WebSignInStatus.Revoked, signIn, role, revokedReason);
        // A deactivated or not-yet-approved account can't use an old sign-in.
        if (!string.Equals(userStatus, "active", StringComparison.Ordinal))
            return new WebSignInCheck(WebSignInStatus.Revoked, signIn, role, WebSignInRevokeReasons.Admin);

        var now = _clock.GetUtcNow();
        if (IsExpired(signIn, policy, now))
            return new WebSignInCheck(WebSignInStatus.TimedOut, signIn, role);

        if (touch && now - signIn.LastActiveAt >= TouchInterval)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE web_sign_ins SET last_active_at = $now WHERE sign_in_id = $id AND revoked_at IS NULL";
            update.Parameters.AddWithValue("$now", Ts(now));
            update.Parameters.AddWithValue("$id", signIn.SignInId);
            await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            signIn = signIn with { LastActiveAt = now };
        }
        return new WebSignInCheck(WebSignInStatus.Valid, signIn, role);
    }

    public async Task<IReadOnlyList<WebSignIn>> ListActiveAsync(string userId, WebSignInPolicy policy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT " + Columns + " FROM web_sign_ins s WHERE s.user_id = $uid AND s.revoked_at IS NULL ORDER BY s.last_active_at DESC";
        cmd.Parameters.AddWithValue("$uid", userId);
        var now = _clock.GetUtcNow();
        var list = new List<WebSignIn>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var s = Read(reader);
            if (!IsExpired(s, policy, now))
                list.Add(s);
        }
        return list;
    }

    public async Task<IReadOnlyList<WebSignIn>> ListAllActiveAsync(WebSignInPolicy policy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT " + Columns + " FROM web_sign_ins s WHERE s.revoked_at IS NULL ORDER BY s.last_active_at DESC";
        var now = _clock.GetUtcNow();
        var list = new List<WebSignIn>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var s = Read(reader);
            if (!IsExpired(s, policy, now))
                list.Add(s);
        }
        return list;
    }

    public async Task<bool> RevokeAsync(string signInId, string reason, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE web_sign_ins SET revoked_at = $now, revoked_reason = $reason WHERE sign_in_id = $id AND revoked_at IS NULL";
        cmd.Parameters.AddWithValue("$now", Ts(_clock.GetUtcNow()));
        cmd.Parameters.AddWithValue("$reason", reason);
        cmd.Parameters.AddWithValue("$id", signInId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<int> RevokeAllAsync(string userId, string reason, string? exceptSignInId = null, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE web_sign_ins SET revoked_at = $now, revoked_reason = $reason
            WHERE user_id = $uid AND revoked_at IS NULL AND ($except IS NULL OR sign_in_id <> $except)
            """;
        cmd.Parameters.AddWithValue("$now", Ts(_clock.GetUtcNow()));
        cmd.Parameters.AddWithValue("$reason", reason);
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$except", (object?)exceptSignInId ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Expired at the absolute or remember-me limit, or (without "Keep me signed in") after the idle timeout.</summary>
    internal static bool IsExpired(WebSignIn s, WebSignInPolicy policy, DateTimeOffset now) =>
        now >= s.ExpiresAt || (!s.Remember && now - s.LastActiveAt >= policy.IdleTimeout);

    private static WebSignIn Read(System.Data.Common.DbDataReader r) => new(
        SignInId: r.GetString(0),
        UserId: r.GetString(1),
        Remember: r.GetInt64(2) != 0,
        UserAgent: r.IsDBNull(3) ? null : r.GetString(3),
        IpAddress: r.IsDBNull(4) ? null : r.GetString(4),
        CreatedAt: ParseTs(r.GetString(5)),
        LastActiveAt: ParseTs(r.GetString(6)),
        ExpiresAt: ParseTs(r.GetString(7)));

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string? Trim(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Length <= max ? s : s[..max];
    private static string Ts(DateTimeOffset dto) => dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseTs(string s) => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}
