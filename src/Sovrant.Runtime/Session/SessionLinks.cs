using System.Globalization;

namespace Sovrant.Runtime.Session;

/// <summary>
/// Phase 133 — what a conversation is linked to right now. Nothing here is
/// stored on the session: a conversation can gain or lose an agent, a
/// workflow, or runs at any time, so the sidebar re-derives this every load.
/// </summary>
public sealed record SessionLinks(
    string? WorkflowStatus = null,
    string? AdHocAgent = null,
    int SwarmRuns = 0,
    int TeamRuns = 0);

/// <summary>
/// Phase 133 — fills in <see cref="SessionListItem.Labels"/> for a page of
/// conversations. Workflows and agent runs always live in SQLite, so the local
/// implementation reads them there even when sessions are on Postgres.
/// </summary>
public interface ISessionLinkResolver
{
    /// <summary>Returns <paramref name="items"/> with <see cref="SessionListItem.Labels"/> set.</summary>
    Task<IReadOnlyList<SessionListItem>> WithLabelsAsync(IReadOnlyList<SessionListItem> items, CancellationToken ct = default);
}

/// <summary>Phase 133 — turns a conversation's links into its sidebar labels. Shared by every surface.</summary>
public static class SessionLabels
{
    /// <summary>Prefix of internal sessions (workflow planner, context compactor) that never appear in a sidebar.</summary>
    public const string SystemSessionPrefix = "__sovrant_";

    /// <summary>Prefix of conversations created by the webhook endpoint: <c>webhook:{source}:{user}</c>.</summary>
    public const string WebhookSessionPrefix = "webhook:";

    /// <summary><c>true</c> for internal sessions that should be hidden from every conversation list.</summary>
    public static bool IsSystemSession(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return sessionId.StartsWith(SystemSessionPrefix, StringComparison.Ordinal);
    }

    /// <summary>Builds the labels for one conversation, in a stable order: agent, workflow, swarm, team, webhook.</summary>
    public static IReadOnlyList<SessionLabel> Build(SessionListItem item, SessionLinks? links)
    {
        ArgumentNullException.ThrowIfNull(item);
        var labels = new List<SessionLabel>();

        var agent = !string.IsNullOrWhiteSpace(item.AgentName) ? item.AgentName : links?.AdHocAgent;
        if (!string.IsNullOrWhiteSpace(agent))
            labels.Add(new SessionLabel($"Agent · {agent}"));

        if (links?.WorkflowStatus is { Length: > 0 } status)
            labels.Add(new SessionLabel($"Workflow · {WorkflowStatusText(status)}", IsActive: IsActiveWorkflow(status)));

        if (links is { SwarmRuns: > 0 })
            labels.Add(new SessionLabel($"Swarm · {Runs(links.SwarmRuns)}"));

        if (links is { TeamRuns: > 0 })
            labels.Add(new SessionLabel($"Team · {Runs(links.TeamRuns)}"));

        if (item.SessionId.StartsWith(WebhookSessionPrefix, StringComparison.Ordinal))
        {
            var rest = item.SessionId[WebhookSessionPrefix.Length..];
            var source = rest.Split(':', 2)[0];
            labels.Add(new SessionLabel(source.Length > 0 ? $"Webhook · {source}" : "Webhook"));
        }

        return labels;
    }

    /// <summary>Joins labels into the single muted line shown under a conversation title.</summary>
    public static string Line(IReadOnlyList<SessionLabel>? labels) =>
        labels is null || labels.Count == 0 ? string.Empty : string.Join(", ", labels.Select(l => l.Text));

    private static string Runs(int n) => n == 1 ? "1 run" : string.Create(CultureInfo.InvariantCulture, $"{n} runs");

    private static bool IsActiveWorkflow(string status) =>
        status is "running" or "planning";

    private static string WorkflowStatusText(string status) => status switch
    {
        "planning" => "Planning",
        "running" => "Running",
        "awaiting_human" => "Awaiting review",
        "completed" => "Completed",
        "failed" => "Failed",
        "cancelled" => "Cancelled",
        _ => status,
    };
}
