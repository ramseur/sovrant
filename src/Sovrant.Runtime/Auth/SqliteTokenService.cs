using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Auth;

/// <summary>
/// SQLite-backed implementation of <see cref="ITokenService"/> over the Phase 32
/// <c>api_tokens</c> table.
///
/// <para><b>Security posture (Phase 38):</b></para>
/// <list type="bullet">
///   <item>Plaintext tokens are SHA-256 hashed before persistence. The plaintext
///         is returned exactly once via <see cref="IssueAsync"/> and never stored.</item>
///   <item>Lookups happen by hashing the incoming plaintext and querying the
///         indexed <c>token_hash</c> column — constant-time wrt token contents.</item>
///   <item>The token-id PK is server-generated as <c>tok_{16hex}</c> from the
///         system CSPRNG; callers cannot influence it.</item>
///   <item>All SQL is parameterized.</item>
///   <item><see cref="ResolveAsync"/> joins <c>users</c> and rejects tokens
///         belonging to inactive users in a single round-trip.</item>
/// </list>
/// </summary>
internal sealed partial class SqliteTokenService : ITokenService
{
    /// <summary>Plaintext token prefix. Identifies a Sovrant token at a glance.</summary>
    public const string TokenPrefix = "svt_";

    /// <summary>Number of random bytes in the secret portion of the token.</summary>
    private const int SecretByteLength = 32;

    /// <summary>
    /// Number of leading characters of the plaintext stored as the masked
    /// display prefix (e.g. <c>svt_AbCdEfGh</c> = 12 chars).
    /// </summary>
    private const int DisplayPrefixLength = 12;

    private const int MaxNameLength = 64;
    private const int MaxScopesLength = 512;

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteTokenService> _logger;

    public SqliteTokenService(
        ISqliteConnectionFactory connectionFactory,
        ILogger<SqliteTokenService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    [LoggerMessage(EventId = 3800, Level = LogLevel.Information,
        Message = "API token issued: token_id={TokenId} user_id={UserId}")]
    private static partial void LogTokenIssued(ILogger logger, string tokenId, string userId);

    [LoggerMessage(EventId = 3801, Level = LogLevel.Information,
        Message = "API token revoked: token_id={TokenId}")]
    private static partial void LogTokenRevoked(ILogger logger, string tokenId);

    // ── ITokenService ──────────────────────────────────────────────────────

    public async Task<TokenIssueResult> IssueAsync(
        string userId,
        string? name = null,
        string? scopes = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("user_id is required.", nameof(userId));
        ValidateName(name);
        ValidateScopes(scopes);
        if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow)
            throw new ArgumentException("expires_at must be in the future.", nameof(expiresAt));

        var plaintext = GeneratePlaintext();
        var hash = HashToken(plaintext);
        var displayPrefix = plaintext[..DisplayPrefixLength];
        var tokenId = GenerateTokenId();
        var now = DateTimeOffset.UtcNow;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO api_tokens
                (token_id, user_id, token_hash, token_prefix, name, scopes, created_at, expires_at, revoked_at)
            VALUES
                ($id, $user, $hash, $prefix, $name, $scopes, $created, $expires, NULL)
            """;
        cmd.Parameters.AddWithValue("$id", tokenId);
        cmd.Parameters.AddWithValue("$user", userId);
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$prefix", displayPrefix);
        cmd.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$scopes", (object?)scopes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", Ts(now));
        cmd.Parameters.AddWithValue("$expires", expiresAt.HasValue ? Ts(expiresAt.Value) : (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        LogTokenIssued(_logger, tokenId, userId);

        var token = new ApiToken
        {
            TokenId = tokenId,
            UserId = userId,
            TokenPrefix = displayPrefix,
            Name = name,
            Scopes = scopes,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            RevokedAt = null,
        };
        return new TokenIssueResult { Token = token, Plaintext = plaintext };
    }

    public async Task<IReadOnlyList<ApiToken>> ListAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return Array.Empty<ApiToken>();

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT token_id, user_id, token_prefix, name, scopes, created_at, expires_at, revoked_at
            FROM api_tokens
            WHERE user_id = $user AND revoked_at IS NULL
            ORDER BY created_at DESC
            """;
        cmd.Parameters.AddWithValue("$user", userId);

        var results = new List<ApiToken>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(ReadToken(reader));
        return results;
    }

    public async Task<bool> RevokeAsync(string tokenId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tokenId)) return false;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE api_tokens
            SET revoked_at = $now
            WHERE token_id = $id AND revoked_at IS NULL
            """;
        cmd.Parameters.AddWithValue("$id", tokenId);
        cmd.Parameters.AddWithValue("$now", Ts(DateTimeOffset.UtcNow));
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (rows > 0)
            LogTokenRevoked(_logger, tokenId);
        return rows > 0;
    }

    public async Task<ApiToken?> ResolveAsync(string plaintextToken, CancellationToken ct = default)
    {
        // Defensive: reject anything that doesn't even look like one of our tokens.
        // This is not the security boundary — the hash lookup is — but it short-circuits
        // obvious garbage before we hit the DB.
        if (string.IsNullOrEmpty(plaintextToken)) return null;
        if (!plaintextToken.StartsWith(TokenPrefix, StringComparison.Ordinal)) return null;

        var hash = HashToken(plaintextToken);

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        // Join users so an inactive user cannot authenticate even if they
        // somehow still hold a non-revoked token.
        cmd.CommandText = """
            SELECT t.token_id, t.user_id, t.token_prefix, t.name, t.scopes,
                   t.created_at, t.expires_at, t.revoked_at
            FROM api_tokens t
            INNER JOIN users u ON u.user_id = t.user_id
            WHERE t.token_hash = $hash
              AND t.revoked_at IS NULL
              AND u.status = 'active'
            """;
        cmd.Parameters.AddWithValue("$hash", hash);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        var token = ReadToken(reader);

        // Reject expired tokens. We check in code rather than SQL so that the
        // expiry semantics are obvious from the test surface and so SQLite's
        // text-based timestamp comparison can't accidentally let a stale token
        // through due to a format mismatch.
        if (token.ExpiresAt.HasValue && token.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return null;

        return token;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Generates a fresh plaintext token (<c>svt_</c> + 32 random bytes, base64url).</summary>
    private static string GeneratePlaintext()
    {
        Span<byte> bytes = stackalloc byte[SecretByteLength];
        RandomNumberGenerator.Fill(bytes);
        // Base64url, no padding — URL-safe and compact.
        var encoded = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return TokenPrefix + encoded;
    }

    /// <summary>SHA-256 hash of the full plaintext token, lowercase hex (64 chars).</summary>
    internal static string HashToken(string plaintext)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(plaintext), hash);
        return Convert.ToHexStringLower(hash);
    }

    private static string GenerateTokenId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return $"tok_{Convert.ToHexStringLower(bytes)}";
    }

    private static void ValidateName(string? name)
    {
        if (name is null) return;
        if (name.Length > MaxNameLength)
            throw new ArgumentException($"name must be ≤ {MaxNameLength} characters.", nameof(name));
    }

    private static void ValidateScopes(string? scopes)
    {
        if (scopes is null) return;
        if (scopes.Length > MaxScopesLength)
            throw new ArgumentException($"scopes must be ≤ {MaxScopesLength} characters.", nameof(scopes));
    }

    private static ApiToken ReadToken(SqliteDataReader reader) => new()
    {
        TokenId = reader.GetString(0),
        UserId = reader.GetString(1),
        TokenPrefix = reader.GetString(2),
        Name = reader.IsDBNull(3) ? null : reader.GetString(3),
        Scopes = reader.IsDBNull(4) ? null : reader.GetString(4),
        CreatedAt = ParseTs(reader.GetString(5)),
        ExpiresAt = reader.IsDBNull(6) ? null : ParseTs(reader.GetString(6)),
        RevokedAt = reader.IsDBNull(7) ? null : ParseTs(reader.GetString(7)),
    };

    private static string Ts(DateTimeOffset dto) =>
        dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTs(string s) =>
        DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
}
