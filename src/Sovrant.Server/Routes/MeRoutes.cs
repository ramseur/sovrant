using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Self-service routes scoped to the authenticated user.
///
/// <para>Every endpoint here resolves the target user from the request's
/// identity (set by <see cref="BearerTokenMiddleware"/>) rather than from
/// the URL — there is no <c>{userId}</c> path parameter to forge.</para>
/// </summary>
internal static class MeRoutes
{
    public static void Map(WebApplication app)
    {
        // Profile.
        app.MapGet("/v1/users/me", GetMe);

        // Per-user API tokens.
        app.MapPost("/v1/users/me/tokens", IssueToken);
        app.MapGet("/v1/users/me/tokens", ListTokens);
        app.MapDelete("/v1/users/me/tokens/{tokenId}", RevokeToken);
    }

    // ── Profile ──────────────────────────────────────────────────────────

    private static async Task<IResult> GetMe(
        HttpContext ctx, IUserService users, CancellationToken ct)
    {
        if (!TryGetCallerUserId(ctx, out var userId, out var error))
            return error;

        var profile = await users.GetProfileAsync(userId, ct).ConfigureAwait(false);
        return profile is null
            ? Results.NotFound(new { error = "User not found." })
            : Results.Ok(profile);
    }

    // ── Token management ─────────────────────────────────────────────────

    private static async Task<IResult> IssueToken(
        IssueTokenRequest req,
        HttpContext ctx,
        ITokenService tokens,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (!TryGetCallerUserId(ctx, out var userId, out var error))
            return error;

        if (req.ExpiresAt is { } exp)
        {
            var now = DateTimeOffset.UtcNow;
            if (exp <= now)
                return Results.BadRequest(new { error = "expires_at must be a future date." });
            if (exp > now.AddYears(2))
                return Results.BadRequest(new { error = "expires_at must be within 2 years." });
        }

        try
        {
            var result = await tokens.IssueAsync(
                userId: userId,
                name: req.Name,
                scopes: req.Scopes,
                expiresAt: req.ExpiresAt,
                ct: ct).ConfigureAwait(false);

            // The plaintext is returned exactly once. Caller must store it
            // immediately — the server cannot recover it.
            return Results.Created(
                $"/v1/users/me/tokens/{result.Token.TokenId}",
                new IssueTokenResponse
                {
                    Token = result.Token,
                    Plaintext = result.Plaintext,
                });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // Vanishingly rare hash collision OR FK violation. The latter
            // would mean the authenticated user_id no longer exists — treat
            // it as 401 since the identity itself is now invalid.
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> ListTokens(
        HttpContext ctx, ITokenService tokens, CancellationToken ct)
    {
        if (!TryGetCallerUserId(ctx, out var userId, out var error))
            return error;

        var list = await tokens.ListAsync(userId, ct).ConfigureAwait(false);
        return Results.Ok(new { tokens = list, count = list.Count });
    }

    private static async Task<IResult> RevokeToken(
        string tokenId,
        HttpContext ctx,
        ITokenService tokens,
        CancellationToken ct)
    {
        if (!TryGetCallerUserId(ctx, out var userId, out var error))
            return error;

        // Ownership check — a user must not be able to revoke another
        // user's tokens by guessing IDs. We list and verify rather than
        // exposing a Get-by-id on ITokenService.
        var owned = await tokens.ListAsync(userId, ct).ConfigureAwait(false);
        if (!owned.Any(t => string.Equals(t.TokenId, tokenId, StringComparison.Ordinal)))
            return Results.NotFound(new { error = "Token not found." });

        var revoked = await tokens.RevokeAsync(tokenId, ct).ConfigureAwait(false);
        return revoked
            ? Results.Ok(new { revoked = tokenId })
            : Results.NotFound(new { error = "Token not found or already revoked." });
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Pulls the authenticated user_id off the request.</summary>
    private static bool TryGetCallerUserId(HttpContext ctx, out string userId, out IResult error)
    {
        var id = ctx.GetUserId();
        if (id is null)
        {
            userId = string.Empty;
            error = Results.BadRequest(new
            {
                error = "/v1/users/me requires a per-user (svt_*) bearer token.",
            });
            return false;
        }

        userId = id;
        error = Results.Ok();
        return true;
    }
}

// ── Request/response DTOs ────────────────────────────────────────────────

internal sealed class IssueTokenRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("scopes")]
    public string? Scopes { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; init; }
}

internal sealed class IssueTokenResponse
{
    [JsonPropertyName("token")]
    public required ApiToken Token { get; init; }

    /// <summary>
    /// The plaintext bearer secret — returned <b>once</b> at issuance and
    /// never recoverable. Clients must capture it immediately.
    /// </summary>
    [JsonPropertyName("plaintext")]
    public required string Plaintext { get; init; }
}
