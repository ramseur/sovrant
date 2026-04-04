using System.Text.Json.Serialization;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;

namespace Sovrant.Server.Routes;

/// <summary>Registers session management endpoints.</summary>
internal static class SessionRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/sessions", ListSessions);
        app.MapGet("/v1/sessions/{id}", GetSession);
        app.MapDelete("/v1/sessions/{id}", DeleteSession);
    }

    private static async Task<IResult> ListSessions(ISessionStore store, CancellationToken ct)
    {
        var ids = await store.ListAsync(ct).ConfigureAwait(false);
        var items = ids.Select(id => new SessionSummaryDto { Id = id }).ToList();
        return Results.Ok(new { sessions = items });
    }

    private static async Task<IResult> GetSession(string id, ISessionStore store, CancellationToken ct)
    {
        var entries = await store.LoadAsync(id, ct).ConfigureAwait(false);
        if (entries.Count == 0)
            return Results.NotFound(new { error = $"Session '{id}' not found." });

        var messages = entries
            .Where(e => e.Role is "user" or "assistant")
            .Select(e => new SessionMessageDto
            {
                Role = e.Role,
                Content = e.Content,
                Timestamp = e.Timestamp,
                InputTokens = e.InputTokens,
                OutputTokens = e.OutputTokens,
            })
            .ToList();

        return Results.Ok(new { session_id = id, messages });
    }

    private static IResult DeleteSession(string id, IRuntimeSessionPool pool)
    {
        var sessionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "sessions");
        var path = Path.Combine(sessionsDir, $"{id}.jsonl");

        if (!File.Exists(path))
            return Results.NotFound(new { error = $"Session '{id}' not found." });

        File.Delete(path);

        // Evict the in-memory runtime so stale history is not retained.
        pool.Evict(id);

        return Results.Ok(new { deleted = id });
    }
}

internal sealed class SessionSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

internal sealed class SessionMessageDto
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("input_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int OutputTokens { get; init; }
}
