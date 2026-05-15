using System.Text.Json.Serialization;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers <c>GET /v1/usage</c> — per-session token usage summary.
///
/// <para>Phase 38 — scopes the session list and per-session entries to the
/// authenticated caller. Admins see all sessions in the store.</para>
/// </summary>
internal static class UsageRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/usage", GetUsage);
    }

    private static async Task<IResult> GetUsage(
        HttpContext ctx,
        ISessionStore store,
        IRuntimeSessionPool pool,
        CancellationToken ct)
    {
        var ownerFilter = ctx.IsAdmin() ? null : ctx.GetUserId();
        var sessionIds = await store.ListAsync(ownerFilter, ct).ConfigureAwait(false);
        var sessions = new List<SessionUsageDto>(sessionIds.Count);

        foreach (var id in sessionIds)
        {
            var config = pool.TryGetConfig(id, ctx.GetUserId());
            if (config is not null)
            {
                // Active session — use live counters.
                sessions.Add(new SessionUsageDto
                {
                    SessionId = id,
                    InputTokens = config.TotalInputTokens,
                    OutputTokens = config.TotalOutputTokens,
                    TotalTokens = config.TotalInputTokens + config.TotalOutputTokens,
                });
            }
            else
            {
                // Inactive session — sum from persisted entries. The list is
                // already owner-filtered so LoadAsync can run unfiltered here
                // and avoid an extra ownership check on every row.
                var entries = await store.LoadAsync(id, ownerUserId: null, ct).ConfigureAwait(false);
                var inp = entries.Sum(e => (long)e.InputTokens);
                var outp = entries.Sum(e => (long)e.OutputTokens);
                sessions.Add(new SessionUsageDto
                {
                    SessionId = id,
                    InputTokens = inp,
                    OutputTokens = outp,
                    TotalTokens = inp + outp,
                });
            }
        }

        var totalInput = sessions.Sum(s => s.InputTokens);
        var totalOutput = sessions.Sum(s => s.OutputTokens);

        return Results.Ok(new UsageResponse
        {
            Sessions = sessions,
            TotalInputTokens = totalInput,
            TotalOutputTokens = totalOutput,
            TotalTokens = totalInput + totalOutput,
        });
    }
}

internal sealed class UsageResponse
{
    [JsonPropertyName("sessions")]
    public IReadOnlyList<SessionUsageDto> Sessions { get; init; } = [];

    [JsonPropertyName("total_input_tokens")]
    public long TotalInputTokens { get; init; }

    [JsonPropertyName("total_output_tokens")]
    public long TotalOutputTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public long TotalTokens { get; init; }
}

internal sealed class SessionUsageDto
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("input_tokens")]
    public long InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public long OutputTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public long TotalTokens { get; init; }
}
