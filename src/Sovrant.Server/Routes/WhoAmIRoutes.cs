using System.Text.Json.Serialization;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers <c>GET /v1/whoami</c> — returns the identity attached to the
/// current request by <see cref="BearerTokenMiddleware"/>.
///
/// <para>Phase 38: lets clients (CLI, dashboard) discover which user a token
/// resolves to without having to scan the token list. Also the assertion
/// surface used by middleware integration tests.</para>
/// </summary>
internal static class WhoAmIRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/whoami", (HttpContext ctx) =>
        {
            var authMode = ctx.Items[SovrantHttpContextKeys.AuthMode] as string;
            var userId = ctx.Items[SovrantHttpContextKeys.UserId] as string;
            var tokenId = ctx.Items[SovrantHttpContextKeys.TokenId] as string;

            return Results.Ok(new WhoAmIResponse
            {
                AuthMode = authMode ?? "unknown",
                UserId = userId,
                TokenId = tokenId,
            });
        });
    }
}

internal sealed class WhoAmIResponse
{
    [JsonPropertyName("auth_mode")]
    public string AuthMode { get; init; } = "unknown";

    [JsonPropertyName("user_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserId { get; init; }

    [JsonPropertyName("token_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenId { get; init; }
}
