using System.Globalization;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Storage.Postgres;

internal sealed class PostgresKnowledgeAttributionStore(IPostgresConnectionFactory factory)
    : IKnowledgeAttributionStore
{
    public async Task RecordAsync(
        string sessionId,
        int turnIndex,
        string kind,
        string slug,
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO knowledge_attributions (session_id, turn_index, kind, slug, used_at)
            VALUES ($1, $2, $3, $4, $5)
            """;
        cmd.Parameters.AddWithValue(sessionId);
        cmd.Parameters.AddWithValue(turnIndex);
        cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(slug);
        cmd.Parameters.AddWithValue(DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KnowledgeAttribution>> GetBySessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, turn_index, kind, slug, used_at
            FROM knowledge_attributions
            WHERE session_id = $1
            ORDER BY id
            """;
        cmd.Parameters.AddWithValue(sessionId);

        var results = new List<KnowledgeAttribution>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new KnowledgeAttribution(
                SessionId: reader.GetString(0),
                TurnIndex: reader.GetInt32(1),
                Kind:      reader.GetString(2),
                Slug:      reader.GetString(3),
                UsedAt:    DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind)));
        }
        return results;
    }
}
