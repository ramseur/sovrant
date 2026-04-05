using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Session;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Routes;

/// <summary>Registers session management endpoints.</summary>
internal static class SessionRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/sessions", ListSessions);
        app.MapGet("/v1/sessions/{id}", GetSession);
        app.MapDelete("/v1/sessions/{id}", DeleteSession);
        app.MapPut("/v1/sessions/{id}/config", PutSessionConfig);
        app.MapGet("/v1/sessions/{id}/config", GetSessionConfig);
        app.MapGet("/v1/sessions/{id}/export", ExportSession);
    }

    private static async Task<IResult> ListSessions(ISessionStore store, CancellationToken ct)
    {
        var ids = await store.ListAsync(ct).ConfigureAwait(false);
        var items = ids.Select(id => new SessionSummaryDto { Id = id }).ToList();
        return Results.Ok(new { sessions = items });
    }

    private static async Task<IResult> GetSession(
        string id,
        ISessionStore store,
        IRuntimeSessionPool pool,
        CancellationToken ct)
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

        // Enrich with live session config if the session is active in memory.
        var sessionConfig = pool.TryGetConfig(id);
        var totalInput = sessionConfig?.TotalInputTokens ?? entries.Sum(e => (long)e.InputTokens);
        var totalOutput = sessionConfig?.TotalOutputTokens ?? entries.Sum(e => (long)e.OutputTokens);

        return Results.Ok(new SessionDetailDto
        {
            SessionId = id,
            Messages = messages,
            TotalInputTokens = totalInput,
            TotalOutputTokens = totalOutput,
        });
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

    private static IResult GetSessionConfig(
        string id,
        IRuntimeSessionPool pool,
        MutableServerConfig serverConfig)
    {
        var sessionConfig = pool.TryGetConfig(id);
        if (sessionConfig is null)
            return Results.NotFound(new { error = $"Session '{id}' is not active in the pool." });

        return Results.Ok(new SessionConfigDto
        {
            Model = sessionConfig.Model ?? serverConfig.Model,
            PermissionMode = (sessionConfig.PermissionMode ?? serverConfig.PermissionMode)
                .ToString().ToLowerInvariant(),
            IsOverridden = sessionConfig.Model is not null || sessionConfig.PermissionMode is not null,
        });
    }

    private static IResult PutSessionConfig(
        string id,
        SessionConfigUpdateRequest req,
        IRuntimeSessionPool pool)
    {
        ArgumentNullException.ThrowIfNull(req);

        var sessionConfig = pool.TryGetConfig(id);
        if (sessionConfig is null)
            return Results.NotFound(new { error = $"Session '{id}' is not active in the pool." });

        if (req.Model is not null)
            sessionConfig.Model = req.Model;

        if (req.PermissionMode is not null &&
            Enum.TryParse<PermissionMode>(req.PermissionMode, ignoreCase: true, out var pm))
            sessionConfig.PermissionMode = pm;

        return Results.Ok(new { updated = true });
    }

    private static async Task<IResult> ExportSession(
        string id,
        ISessionStore store,
        CancellationToken ct)
    {
        var entries = await store.LoadAsync(id, ct).ConfigureAwait(false);
        if (entries.Count == 0)
            return Results.NotFound(new { error = $"Session '{id}' not found." });

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"# Session: {id}").AppendLine();
        sb.AppendLine();

        foreach (var entry in entries)
        {
            var ts = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            switch (entry.Role)
            {
                case "user":
                    sb.Append(CultureInfo.InvariantCulture, $"## User ({ts})").AppendLine();
                    sb.AppendLine();
                    sb.AppendLine(entry.Content);
                    sb.AppendLine();
                    break;

                case "assistant":
                    sb.Append(CultureInfo.InvariantCulture, $"## Assistant ({ts})").AppendLine();
                    if (entry.Model is not null)
                        sb.Append(CultureInfo.InvariantCulture, $"*Model: {entry.Model}*").AppendLine();
                    sb.AppendLine();
                    sb.AppendLine(entry.Content);
                    if (entry.InputTokens > 0 || entry.OutputTokens > 0)
                    {
                        sb.AppendLine();
                        sb.Append(CultureInfo.InvariantCulture,
                            $"> Tokens: {entry.InputTokens} input, {entry.OutputTokens} output").AppendLine();
                    }
                    sb.AppendLine();
                    break;

                case "tool_use":
                    sb.Append(CultureInfo.InvariantCulture,
                        $"### Tool: {entry.ToolName ?? "unknown"} ({ts})").AppendLine();
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine(entry.Content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                    break;

                case "tool_result":
                    var errorTag = entry.IsError ? " [ERROR]" : "";
                    sb.Append(CultureInfo.InvariantCulture,
                        $"### Result: {entry.ToolName ?? "unknown"}{errorTag}").AppendLine();
                    sb.AppendLine();
                    sb.AppendLine("```");
                    // Truncate long tool results to keep export readable.
                    var content = entry.Content.Length > 2000
                        ? string.Concat(entry.Content.AsSpan(0, 2000), "... (truncated)")
                        : entry.Content;
                    sb.AppendLine(content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                    break;

                default:
                    // system, compaction, etc. — include as a note
                    sb.Append(CultureInfo.InvariantCulture,
                        $"### {entry.Role} ({ts})").AppendLine();
                    sb.AppendLine();
                    sb.AppendLine(entry.Content);
                    sb.AppendLine();
                    break;
            }
        }

        return Results.Text(sb.ToString(), "text/markdown; charset=utf-8");
    }
}

internal sealed class SessionConfigDto
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("permission_mode")]
    public string PermissionMode { get; init; } = string.Empty;

    [JsonPropertyName("is_overridden")]
    public bool IsOverridden { get; init; }
}

internal sealed class SessionConfigUpdateRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("permission_mode")]
    public string? PermissionMode { get; init; }
}

internal sealed class SessionDetailDto
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public IReadOnlyList<SessionMessageDto> Messages { get; init; } = [];

    [JsonPropertyName("total_input_tokens")]
    public long TotalInputTokens { get; init; }

    [JsonPropertyName("total_output_tokens")]
    public long TotalOutputTokens { get; init; }
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
