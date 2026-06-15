using System.Globalization;
using Npgsql;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Storage.Postgres;

internal sealed class PostgresKnowledgeStore(IPostgresConnectionFactory factory) : IKnowledgeStore
{
    private const string SelectColumns =
        "knowledge_id, kind, slug, name, description, tier, body, workspace_id, " +
        "created_at, updated_at, trigger, agents, tools, industry, default_format, category, " +
        "role, recommended_level, fields_json, filename_template";

    public async Task<IReadOnlyList<KnowledgePage>> GetAllAsync(
        string kind,
        string workspaceId = "",
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM knowledge_pages " +
            "WHERE kind = $1 AND workspace_id = $2 ORDER BY slug";
        cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(workspaceId);
        return await ReadPagesAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<KnowledgePage?> GetAsync(
        string kind,
        string slug,
        string workspaceId = "",
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM knowledge_pages " +
            "WHERE kind = $1 AND slug = $2 AND workspace_id = $3 LIMIT 1";
        cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(slug);
        cmd.Parameters.AddWithValue(workspaceId);
        var pages = await ReadPagesAsync(cmd, ct).ConfigureAwait(false);
        return pages.Count > 0 ? pages[0] : null;
    }

    public async Task<KnowledgePage?> GetActiveAsync(
        string kind,
        string slug,
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM knowledge_pages " +
            "WHERE kind = $1 AND slug = $2 " +
            "ORDER BY CASE WHEN tier = 'User' THEN 0 ELSE 1 END " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(slug);
        var pages = await ReadPagesAsync(cmd, ct).ConfigureAwait(false);
        return pages.Count > 0 ? pages[0] : null;
    }

    public async Task<KnowledgePage?> GetEffectiveAsync(
        string kind,
        string slug,
        string projectId = "",
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        // Layered resolution: project overlay > global overlay > base.
        // Params: $1=kind, $2=slug, $3=base, $4=global, $5=project
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM knowledge_pages " +
            "WHERE kind = $1 AND slug = $2 " +
            "AND workspace_id IN ($3, $4, $5) " +
            "ORDER BY " +
            "  CASE workspace_id " +
            "    WHEN $5 THEN 0 " +
            "    WHEN $4 THEN 1 " +
            "    ELSE 2 " +
            "  END " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(slug);
        cmd.Parameters.AddWithValue(KnowledgeScope.Base);
        cmd.Parameters.AddWithValue(KnowledgeScope.Global);
        // Sentinel value avoids IN-clause errors when no project is scoped.
        cmd.Parameters.AddWithValue(string.IsNullOrEmpty(projectId) ? "\0__no_project__" : projectId);
        var pages = await ReadPagesAsync(cmd, ct).ConfigureAwait(false);
        return pages.Count > 0 ? pages[0] : null;
    }

    public async Task<IReadOnlyList<KnowledgePage>> GetAllEffectiveAsync(
        string kind,
        string projectId = "",
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        // Params: $1=kind, $2=base, $3=global, $4=project
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM knowledge_pages " +
            "WHERE kind = $1 AND workspace_id IN ($2, $3, $4) " +
            "ORDER BY slug";
        cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(KnowledgeScope.Base);
        cmd.Parameters.AddWithValue(KnowledgeScope.Global);
        cmd.Parameters.AddWithValue(string.IsNullOrEmpty(projectId) ? "\0__no_project__" : projectId);
        var rows = await ReadPagesAsync(cmd, ct).ConfigureAwait(false);

        // Collapse to one row per slug: project > global > base.
        static int Rank(KnowledgePage p, string proj) =>
            p.WorkspaceId == proj && !string.IsNullOrEmpty(proj) ? 0
            : p.WorkspaceId == KnowledgeScope.Global ? 1
            : 2;

        var bySlug = new Dictionary<string, KnowledgePage>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!bySlug.TryGetValue(row.Slug, out var existing) ||
                Rank(row, projectId) < Rank(existing, projectId))
            {
                bySlug[row.Slug] = row;
            }
        }
        return bySlug.Values.OrderBy(p => p.Slug, StringComparer.Ordinal).ToList();
    }

    public async Task UpsertAsync(KnowledgePage page, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO knowledge_pages
                (knowledge_id, kind, slug, name, description, tier, body, workspace_id,
                 created_at, updated_at, trigger, agents, tools, industry, default_format, category,
                 role, recommended_level, fields_json, filename_template)
            VALUES
                ($1, $2, $3, $4, $5, $6, $7, $8,
                 $9, $10, $11, $12, $13, $14, $15, $16,
                 $17, $18, $19, $20)
            ON CONFLICT (kind, slug, workspace_id) DO UPDATE SET
                name              = EXCLUDED.name,
                description       = EXCLUDED.description,
                tier              = EXCLUDED.tier,
                body              = EXCLUDED.body,
                updated_at        = EXCLUDED.updated_at,
                trigger           = EXCLUDED.trigger,
                agents            = EXCLUDED.agents,
                tools             = EXCLUDED.tools,
                industry          = EXCLUDED.industry,
                default_format    = EXCLUDED.default_format,
                category          = EXCLUDED.category,
                role              = EXCLUDED.role,
                recommended_level = EXCLUDED.recommended_level,
                fields_json       = EXCLUDED.fields_json,
                filename_template = EXCLUDED.filename_template
            """;

        cmd.Parameters.AddWithValue(page.KnowledgeId);
        cmd.Parameters.AddWithValue(page.Kind);
        cmd.Parameters.AddWithValue(page.Slug);
        cmd.Parameters.AddWithValue(page.Name);
        cmd.Parameters.AddWithValue(page.Description);
        cmd.Parameters.AddWithValue(page.Tier);
        cmd.Parameters.AddWithValue(page.Body);
        cmd.Parameters.AddWithValue(page.WorkspaceId);
        cmd.Parameters.AddWithValue(page.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue(page.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue((object?)page.Trigger ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.Agents ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.Tools ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.Industry ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.DefaultFormat ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.Role ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.RecommendedLevel ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.FieldsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)page.FilenameTemplate ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        string kind,
        string slug,
        string workspaceId = "",
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "DELETE FROM knowledge_pages WHERE kind = $1 AND slug = $2 AND workspace_id = $3";
        cmd.Parameters.AddWithValue(kind);
        cmd.Parameters.AddWithValue(slug);
        cmd.Parameters.AddWithValue(workspaceId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<KnowledgePage>> ReadPagesAsync(
        NpgsqlCommand cmd,
        CancellationToken ct)
    {
        var results = new List<KnowledgePage>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var n10 = await reader.IsDBNullAsync(10, ct).ConfigureAwait(false);
            var n11 = await reader.IsDBNullAsync(11, ct).ConfigureAwait(false);
            var n12 = await reader.IsDBNullAsync(12, ct).ConfigureAwait(false);
            var n13 = await reader.IsDBNullAsync(13, ct).ConfigureAwait(false);
            var n14 = await reader.IsDBNullAsync(14, ct).ConfigureAwait(false);
            var n15 = await reader.IsDBNullAsync(15, ct).ConfigureAwait(false);
            var n16 = await reader.IsDBNullAsync(16, ct).ConfigureAwait(false);
            var n17 = await reader.IsDBNullAsync(17, ct).ConfigureAwait(false);
            var n18 = await reader.IsDBNullAsync(18, ct).ConfigureAwait(false);
            var n19 = await reader.IsDBNullAsync(19, ct).ConfigureAwait(false);

            results.Add(new KnowledgePage(
                KnowledgeId:      reader.GetString(0),
                Kind:             reader.GetString(1),
                Slug:             reader.GetString(2),
                Name:             reader.GetString(3),
                Description:      reader.GetString(4),
                Tier:             reader.GetString(5),
                Body:             reader.GetString(6),
                WorkspaceId:      reader.GetString(7),
                CreatedAt:        ParseTimestamp(reader.GetString(8)),
                UpdatedAt:        ParseTimestamp(reader.GetString(9)),
                Trigger:          n10 ? null : reader.GetString(10),
                Agents:           n11 ? null : reader.GetString(11),
                Tools:            n12 ? null : reader.GetString(12),
                Industry:         n13 ? null : reader.GetString(13),
                DefaultFormat:    n14 ? null : reader.GetString(14),
                Category:         n15 ? null : reader.GetString(15),
                Role:             n16 ? null : reader.GetString(16),
                RecommendedLevel: n17 ? null : reader.GetString(17),
                FieldsJson:       n18 ? null : reader.GetString(18),
                FilenameTemplate: n19 ? null : reader.GetString(19)));
        }
        return results;
    }

    // Accepts both ISO 8601 round-trip strings (written by UpsertAsync) and raw Unix
    // epoch seconds (written by unixepoch() in SQL seed migrations like V037).
    private static DateTimeOffset ParseTimestamp(string value) =>
        long.TryParse(value, out var unix)
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : DateTimeOffset.Parse(value, null, DateTimeStyles.RoundtripKind);
}
