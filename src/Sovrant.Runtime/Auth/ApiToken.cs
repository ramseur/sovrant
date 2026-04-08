using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Auth;

/// <summary>
/// A per-user API token row from the <c>api_tokens</c> table. The plaintext
/// secret is <b>never</b> stored or returned through this record — only the
/// hash lives on disk, and the plaintext is only ever surfaced once via
/// <see cref="TokenIssueResult"/> at creation time.
///
/// <para>Phase 38 — replaces the single shared <c>SOVRANT_TOKEN</c> with
/// per-user bearer tokens.</para>
/// </summary>
public sealed record ApiToken
{
    /// <summary>Server-generated token id (<c>tok_{16hex}</c>).</summary>
    [JsonPropertyName("token_id")]
    public required string TokenId { get; init; }

    /// <summary>Owning user (FK → <c>users.user_id</c>).</summary>
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    /// <summary>
    /// Masked prefix safe for display (e.g. <c>svt_AbCdEfGh</c>). The first 12
    /// characters of the plaintext token. Used by the <c>GET</c> list endpoint
    /// so users can identify their tokens without ever seeing the full secret again.
    /// </summary>
    [JsonPropertyName("token_prefix")]
    public required string TokenPrefix { get; init; }

    /// <summary>Optional human-friendly label (e.g. <c>"laptop-cli"</c>).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Optional comma-separated scopes (e.g. <c>"sessions:read,tools:execute"</c>).
    /// Phase 38 stores but does not enforce scopes — enforcement lands in Phase 40.
    /// </summary>
    [JsonPropertyName("scopes")]
    public string? Scopes { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary><c>null</c> means the token never expires.</summary>
    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Set when the token has been revoked. <c>null</c> means active.</summary>
    [JsonPropertyName("revoked_at")]
    public DateTimeOffset? RevokedAt { get; init; }
}

/// <summary>
/// Returned <b>once</b> at issuance time. Carries the plaintext secret that
/// the caller must store (or surrender to the user) immediately — it cannot
/// be recovered later because only the SHA-256 hash is persisted.
/// </summary>
public sealed record TokenIssueResult
{
    /// <summary>The metadata row that was written to <c>api_tokens</c>.</summary>
    public required ApiToken Token { get; init; }

    /// <summary>
    /// The full plaintext token (e.g. <c>svt_AbCdEf...</c>). The caller is
    /// responsible for handing this to the end user immediately and then
    /// dropping all references — the server cannot recover it.
    /// </summary>
    public required string Plaintext { get; init; }
}
