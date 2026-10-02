using Microsoft.Data.Sqlite;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 133 — derives sidebar labels from the <c>workflows</c> and
/// <c>agent_runs</c> tables. Both always live in SQLite, so this reads them
/// directly even when the session store itself is on Postgres. Two queries per
/// page of conversations, regardless of how many conversations are on it.
/// </summary>
internal sealed class SqliteSessionLinkResolver(ISqliteConnectionFactory connectionFactory) : ISessionLinkResolver
{
    public async Task<IReadOnlyList<SessionListItem>> WithLabelsAsync(
        IReadOnlyList<SessionListItem> items, CancellationToken ct = default)
    {
        if (items.Count == 0)
            return items;

        var ids = items.Select(i => i.SessionId).Distinct(StringComparer.Ordinal).ToList();
        var workflow = new Dictionary<string, string>(StringComparer.Ordinal);
        var adHoc = new Dictionary<string, string>(StringComparer.Ordinal);
        var swarm = new Dictionary<string, int>(StringComparer.Ordinal);
        var team = new Dictionary<string, int>(StringComparer.Ordinal);

        using var connection = connectionFactory.CreateConnection();
        foreach (var chunk in ids.Chunk(ChunkSize))
            await ReadLinksAsync(connection, chunk, workflow, adHoc, swarm, team, ct).ConfigureAwait(false);

        return items.Select(item =>
        {
            var links = new SessionLinks(
                WorkflowStatus: workflow.GetValueOrDefault(item.SessionId),
                AdHocAgent: adHoc.GetValueOrDefault(item.SessionId),
                SwarmRuns: swarm.GetValueOrDefault(item.SessionId),
                TeamRuns: team.GetValueOrDefault(item.SessionId));
            return item with { Labels = SessionLabels.Build(item, links) };
        }).ToList();
    }

    /// <summary>Keeps every query well under SQLite's bound-parameter limit.</summary>
    private const int ChunkSize = 500;

    private static async Task ReadLinksAsync(
        SqliteConnection connection,
        IReadOnlyList<string> ids,
        Dictionary<string, string> workflow,
        Dictionary<string, string> adHoc,
        Dictionary<string, int> swarm,
        Dictionary<string, int> team,
        CancellationToken ct)
    {
        // A workflow's conversation is its explicit session_id, or — for workflows
        // created before session_id was always set — the workflow's own id. The
        // most recently updated workflow wins if several point at one conversation.
        using (var cmd = connection.CreateCommand())
        {
            var p = AddIdParameters(cmd, ids);
#pragma warning disable CA2100 // placeholders are generated parameter names ($id0, $id1, …); ids are bound as values
            cmd.CommandText = $"""
                SELECT COALESCE(session_id, id) AS sid, status
                FROM workflows
                WHERE session_id IN ({p}) OR (session_id IS NULL AND id IN ({p}))
                ORDER BY updated_at ASC
                """;
#pragma warning restore CA2100
            using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
                workflow[r.GetString(0)] = r.GetString(1);
        }

        // Runs launched from a conversation (session_id), plus ad-hoc agent runs,
        // whose turns are persisted under the run id itself.
        using (var cmd = connection.CreateCommand())
        {
            var p = AddIdParameters(cmd, ids);
#pragma warning disable CA2100 // placeholders are generated parameter names ($id0, $id1, …); ids are bound as values
            cmd.CommandText = $"""
                SELECT session_id, run_id, kind, member_id
                FROM agent_runs
                WHERE session_id IN ({p}) OR run_id IN ({p})
                """;
#pragma warning restore CA2100
            using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                var sessionId = await r.IsDBNullAsync(0, ct).ConfigureAwait(false) ? null : r.GetString(0);
                var runId = r.GetString(1);
                var kind = r.GetString(2);
                var member = await r.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : r.GetString(3);

                if (kind == "adhoc-template" && member is not null)
                    adHoc[runId] = member;

                if (sessionId is null)
                    continue;
                if (kind == "swarm" || kind == "swarm-task")
                    swarm[sessionId] = swarm.GetValueOrDefault(sessionId) + 1;
                else if (kind == "delegation")
                    team[sessionId] = team.GetValueOrDefault(sessionId) + 1;
            }
        }
    }

    /// <summary>Adds one parameter per id and returns the comma-separated placeholder list.</summary>
    private static string AddIdParameters(SqliteCommand cmd, IReadOnlyList<string> ids)
    {
        var names = new string[ids.Count];
        for (var i = 0; i < ids.Count; i++)
        {
            names[i] = $"$id{i}";
            cmd.Parameters.AddWithValue(names[i], ids[i]);
        }
        return string.Join(", ", names);
    }
}
