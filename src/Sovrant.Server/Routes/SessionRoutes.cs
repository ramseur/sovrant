using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Session;
using Sovrant.Server.Auth;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers session management endpoints.
///
/// <para>All endpoints scope reads and deletes to the authenticated user's owned
/// sessions. Admin callers (<c>users.role = 'admin'</c>) see the full set without
/// a filter.</para>
/// </summary>
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

    /// <summary>
    /// Returns the ownership filter to apply. Admins return null (no filter);
    /// regular users return their user id.
    /// </summary>
    private static string? OwnerFilter(HttpContext ctx) =>
        ctx.IsAdmin() ? null : ctx.GetUserId();

    private static async Task<IResult> ListSessions(
        HttpContext ctx,
        ISessionStore store,
        CancellationToken ct)
    {
        var query = ctx.Request.Query["q"].FirstOrDefault();

        // If a search query is provided, use FTS5 full-text search.
        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchResults = await store.SearchAsync(query, OwnerFilter(ctx), ct: ct).ConfigureAwait(false);
            return Results.Ok(new { sessions = searchResults });
        }

        var ids = await store.ListAsync(OwnerFilter(ctx), ct).ConfigureAwait(false);
        var items = ids.Select(id => new SessionSummaryDto { Id = id }).ToList();
        return Results.Ok(new { sessions = items });
    }

    private static async Task<IResult> GetSession(
        string id,
        HttpContext ctx,
        ISessionStore store,
        IRuntimeSessionPool pool,
        CancellationToken ct)
    {
        if (!InputValidation.IsValidSessionId(id))
            return Results.BadRequest(new { error = "Invalid session ID format." });

        var entries = await store.LoadAsync(id, OwnerFilter(ctx), ct).ConfigureAwait(false);
        if (entries.Count == 0)
            return Results.NotFound(new { error = $"Session '{id}' not found." });

        // Single pass over entries: build message DTOs and accumulate totals
        // in one iteration instead of two LINQ passes plus two Sum() scans.
        var messages = new List<SessionMessageDto>(entries.Count);
        long fallbackInput = 0;
        long fallbackOutput = 0;
        foreach (var e in entries)
        {
            fallbackInput += e.InputTokens;
            fallbackOutput += e.OutputTokens;
            if (e.Role is "user" or "assistant")
            {
                messages.Add(new SessionMessageDto
                {
                    Role = e.Role,
                    Content = e.Content,
                    Timestamp = e.Timestamp,
                    InputTokens = e.InputTokens,
                    OutputTokens = e.OutputTokens,
                });
            }
        }

        // Enrich with live session config if the session is active in memory.
        var sessionConfig = pool.TryGetConfig(id, ctx.GetUserId());
        var totalInput = sessionConfig?.TotalInputTokens ?? fallbackInput;
        var totalOutput = sessionConfig?.TotalOutputTokens ?? fallbackOutput;

        return Results.Ok(new SessionDetailDto
        {
            SessionId = id,
            Messages = messages,
            TotalInputTokens = totalInput,
            TotalOutputTokens = totalOutput,
        });
    }

    private static async Task<IResult> DeleteSession(
        string id,
        HttpContext ctx,
        ISessionStore store,
        IRuntimeSessionPool pool,
        CancellationToken ct)
    {
        if (!InputValidation.IsValidSessionId(id))
            return Results.BadRequest(new { error = "Invalid session ID format." });

        // Route through the store so ownership is checked in one place
        // (Phase 38). Previously this handler deleted the JSONL file
        // directly, bypassing the ownership column entirely.
        var deleted = await store.DeleteAsync(id, OwnerFilter(ctx), ct).ConfigureAwait(false);
        if (!deleted)
            return Results.NotFound(new { error = $"Session '{id}' not found." });

        // Also drop the in-memory runtime so a subsequent request doesn't
        // resurrect stale history from the pool. Admins evict across all
        // pool partitions for this session id.
        pool.Evict(id, OwnerFilter(ctx));

        return Results.Ok(new { deleted = id });
    }

    private static async Task<IResult> GetSessionConfig(
        string id,
        HttpContext ctx,
        ISessionStore store,
        IRuntimeSessionPool pool,
        MutableServerConfig serverConfig,
        CancellationToken ct)
    {
        if (!InputValidation.IsValidSessionId(id))
            return Results.BadRequest(new { error = "Invalid session ID format." });

        // Ownership pre-flight so non-owners get a consistent 404 rather than
        // leaking whether a session id is live in the pool.
        if (!await CallerOwnsAsync(ctx, store, id, ct).ConfigureAwait(false))
            return Results.NotFound(new { error = $"Session '{id}' is not active in the pool." });

        var sessionConfig = pool.TryGetConfig(id, ctx.GetUserId());
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

    private static async Task<IResult> PutSessionConfig(
        string id,
        SessionConfigUpdateRequest req,
        HttpContext ctx,
        ISessionStore store,
        IRuntimeSessionPool pool,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (!InputValidation.IsValidSessionId(id))
            return Results.BadRequest(new { error = "Invalid session ID format." });

        if (!await CallerOwnsAsync(ctx, store, id, ct).ConfigureAwait(false))
            return Results.NotFound(new { error = $"Session '{id}' is not active in the pool." });

        var sessionConfig = pool.TryGetConfig(id, ctx.GetUserId());
        if (sessionConfig is null)
            return Results.NotFound(new { error = $"Session '{id}' is not active in the pool." });

        if (req.Model is not null)
        {
            if (!InputValidation.IsValidModelName(req.Model))
                return Results.BadRequest(new { error = "Invalid model name." });
            sessionConfig.Model = req.Model;
        }

        if (req.PermissionMode is not null &&
            Enum.TryParse<PermissionMode>(req.PermissionMode, ignoreCase: true, out var pm))
            sessionConfig.PermissionMode = pm;

        return Results.Ok(new { updated = true });
    }

    private static async Task<IResult> ExportSession(
        string id,
        HttpContext ctx,
        ISessionStore store,
        CancellationToken ct)
    {
        if (!InputValidation.IsValidSessionId(id))
            return Results.BadRequest(new { error = "Invalid session ID format." });

        var entries = await store.LoadAsync(id, OwnerFilter(ctx), ct).ConfigureAwait(false);
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

    private static async Task<bool> CallerOwnsAsync(
        HttpContext ctx,
        ISessionStore store,
        string sessionId,
        CancellationToken ct)
    {
        if (ctx.IsAdmin())
            return true;

        var me = ctx.GetUserId();
        if (me is null)
            return false;

        var owner = await store.GetOwnerAsync(sessionId, ct).ConfigureAwait(false);
        return owner is not null && string.Equals(owner, me, StringComparison.Ordinal);
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
