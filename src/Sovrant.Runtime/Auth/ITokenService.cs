namespace Sovrant.Runtime.Auth;

/// <summary>
/// Phase 38 — per-user API token management. Issues, lists, revokes, and
/// resolves bearer tokens stored in the <c>api_tokens</c> table.
///
/// <para><b>Storage model:</b> tokens are SHA-256 hashed before persistence.
/// The plaintext secret is returned exactly once via <see cref="IssueAsync"/>
/// and is never retrievable again. Lookups happen by hashing the incoming
/// token and querying the indexed <c>token_hash</c> column.</para>
///
/// <para><b>Token format:</b> <c>svt_</c> prefix + 32 random bytes encoded as
/// base64url (no padding) → ~47 characters total. The <c>svt_</c> prefix is
/// included in the hash so that lookups work against the full plaintext.</para>
///
/// <para><b>Not in this phase:</b> middleware integration, admin enforcement,
/// management endpoints, and database hardening — those land in subsequent PRs.</para>
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// The plaintext prefix used to identify a Sovrant per-user token at a
    /// glance — currently <c>svt_</c>. Exposed on the interface so middleware
    /// and CLI tooling can route on the prefix without depending on the
    /// concrete implementation.
    /// </summary>
    public const string TokenPrefix = "svt_";


    /// <summary>
    /// Mints a fresh token for the given user. Returns the metadata row plus
    /// the plaintext secret — <b>this is the only time the plaintext is ever
    /// exposed</b>. The caller must surrender it to the user immediately.
    /// </summary>
    /// <param name="userId">Owning user. Must reference an existing row in <c>users</c>.</param>
    /// <param name="name">Optional human-friendly label (≤ 64 chars).</param>
    /// <param name="scopes">Optional comma-separated scopes (stored, not enforced in Phase 38).</param>
    /// <param name="expiresAt">Optional expiration. <c>null</c> means the token never expires.</param>
    /// <exception cref="ArgumentException">Validation failed.</exception>
    /// <exception cref="Microsoft.Data.Sqlite.SqliteException">
    /// FK violation (user does not exist) or unique violation (vanishingly rare hash collision).
    /// </exception>
    Task<TokenIssueResult> IssueAsync(
        string userId,
        string? name = null,
        string? scopes = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all non-revoked tokens for a user, newest first. Returns masked
    /// metadata only — never the plaintext or hash.
    /// </summary>
    Task<IReadOnlyList<ApiToken>> ListAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes a token by setting <c>revoked_at</c>. The row is preserved so
    /// audit trails referencing it remain intact. Returns <c>true</c> when a
    /// row was actually revoked, <c>false</c> when the token was unknown or
    /// already revoked.
    /// </summary>
    Task<bool> RevokeAsync(string tokenId, CancellationToken ct = default);

    /// <summary>
    /// Resolves an incoming plaintext bearer token to its <see cref="ApiToken"/>
    /// row. Returns <c>null</c> for any of: unknown token, revoked token, expired
    /// token, or token belonging to an inactive user.
    ///
    /// <para>This is the hot path called by <c>BearerTokenMiddleware</c> on every
    /// authenticated request. It performs a single indexed SELECT against
    /// <c>token_hash</c> joined with <c>users</c>.</para>
    /// </summary>
    Task<ApiToken?> ResolveAsync(string plaintextToken, CancellationToken ct = default);
}
