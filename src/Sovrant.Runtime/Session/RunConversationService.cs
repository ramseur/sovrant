using System.Globalization;

namespace Sovrant.Runtime.Session;

/// <summary>
/// Phase 133 (option A) — gives a team or swarm run that wasn't started from a
/// chat its own conversation, the same way every workflow has one. The
/// conversation id is the run id; it's seeded with the goal, titled from it,
/// and gets the outcome appended when the run ends. Because the run records
/// this conversation in <c>agent_runs.session_id</c>, the sidebar shows it with
/// a "Team · 1 run" / "Swarm · 1 run" label and it can be filed like any chat.
/// Best-effort: a failure here never fails the run itself.
/// </summary>
public sealed class RunConversationService(ISessionStore sessions)
{
    /// <summary>Longest run output copied into the conversation when the run ends.</summary>
    public const int MaxOutputChars = 4000;

    /// <summary>
    /// Creates the run's conversation. <paramref name="kind"/> is a display word
    /// such as "Team run" or "Swarm". Returns <c>false</c> if it couldn't be created.
    /// </summary>
    public async Task<bool> StartAsync(string runId, string ownerUserId, string kind, string goal, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        ArgumentNullException.ThrowIfNull(goal);
        try
        {
            await sessions.AppendAsync(runId,
                new SessionEntry(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "user", goal),
                ownerUserId, ct).ConfigureAwait(false);
            await sessions.SetTitleAsync(runId, Title(kind, goal), ownerUserId, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Appends the run's outcome to its conversation.</summary>
    public async Task CompleteAsync(
        string runId, string ownerUserId, string kind, bool succeeded, string? output, TimeSpan? duration = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        var took = duration is { } d ? string.Create(CultureInfo.InvariantCulture, $" in {d.TotalSeconds:0.#}s") : string.Empty;
        var header = $"{kind} {(succeeded ? "finished" : "failed")}{took}.";
        var body = string.IsNullOrWhiteSpace(output)
            ? header
            : $"{header}\n\n{(output.Length > MaxOutputChars ? string.Concat(output.AsSpan(0, MaxOutputChars), "\n\n…(truncated)") : output)}";
        try
        {
            await sessions.AppendAsync(runId,
                new SessionEntry(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "assistant", body),
                ownerUserId, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: the run's own status in agent_runs is the source of truth.
        }
    }

    private static string Title(string kind, string goal)
    {
        var oneLine = string.Join(' ', goal.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var title = $"{kind}: {oneLine}";
        return title.Length > 60 ? string.Concat(title.AsSpan(0, 57), "...") : title;
    }
}
