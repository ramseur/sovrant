using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Storage;

internal sealed class SqliteKnowledgeAttributionStore(ISqliteConnectionFactory connectionFactory)
    : IKnowledgeAttributionStore
{
    public async Task RecordAsync(
        string sessionId,
        int turnIndex,
        string kind,
        string slug,
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO knowledge_attributions (session_id, turn_index, kind, slug, used_at)
            VALUES ($sid, $turn, $kind, $slug, $used_at)
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$turn", turnIndex);
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$slug", slug);
        cmd.Parameters.AddWithValue("$used_at", DateTimeOffset.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KnowledgeAttribution>> GetBySessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, turn_index, kind, slug, used_at
            FROM knowledge_attributions
            WHERE session_id = $sid
            ORDER BY id
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId);

        var results = new List<KnowledgeAttribution>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new KnowledgeAttribution(
                SessionId: reader.GetString(0),
                TurnIndex: reader.GetInt32(1),
                Kind: reader.GetString(2),
                Slug: reader.GetString(3),
                UsedAt: DateTimeOffset.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        }
        return results;
    }
}
