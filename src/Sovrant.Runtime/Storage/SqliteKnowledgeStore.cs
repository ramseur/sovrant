using System.Globalization;
using Microsoft.Data.Sqlite;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Storage;

/// <summary>SQLite-backed store for knowledge pages (skills, document templates, tool templates).</summary>
internal sealed class SqliteKnowledgeStore(ISqliteConnectionFactory connectionFactory) : IKnowledgeStore
{
    private const string SelectColumns =
        "knowledge_id, kind, slug, name, description, tier, body, workspace_id, " +
        "created_at, updated_at, trigger, agents, tools, industry, default_format, category";

    public async Task<IReadOnlyList<KnowledgePage>> GetAllAsync(
        string kind,
        string workspaceId = "",
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM knowledge_pages " +
            "WHERE kind = $kind AND workspace_id = $wid ORDER BY slug";
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        return await ReadPagesAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<KnowledgePage?> GetAsync(
        string kind,
        string slug,
        string workspaceId = "",
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM knowledge_pages " +
            "WHERE kind = $kind AND slug = $slug AND workspace_id = $wid LIMIT 1";
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$slug", slug);
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        var pages = await ReadPagesAsync(cmd, ct).ConfigureAwait(false);
        return pages.Count > 0 ? pages[0] : null;
    }

    public async Task<KnowledgePage?> GetActiveAsync(
        string kind,
        string slug,
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT {SelectColumns} FROM knowledge_pages " +
            "WHERE kind = $kind AND slug = $slug " +
            "ORDER BY CASE WHEN tier = 'User' THEN 0 ELSE 1 END " +
            "LIMIT 1";
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$slug", slug);
        var pages = await ReadPagesAsync(cmd, ct).ConfigureAwait(false);
        return pages.Count > 0 ? pages[0] : null;
    }

    public async Task UpsertAsync(KnowledgePage page, CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO knowledge_pages
                (knowledge_id, kind, slug, name, description, tier, body, workspace_id,
                 created_at, updated_at, trigger, agents, tools, industry, default_format, category)
            VALUES
                ($id, $kind, $slug, $name, $desc, $tier, $body, $wid,
                 $cat, $uat, $trigger, $agents, $tools, $industry, $df, $category)
            ON CONFLICT(kind, slug, workspace_id) DO UPDATE SET
                name           = excluded.name,
                description    = excluded.description,
                tier           = excluded.tier,
                body           = excluded.body,
                updated_at     = excluded.updated_at,
                trigger        = excluded.trigger,
                agents         = excluded.agents,
                tools          = excluded.tools,
                industry       = excluded.industry,
                default_format = excluded.default_format,
                category       = excluded.category
            """;

        cmd.Parameters.AddWithValue("$id", page.KnowledgeId);
        cmd.Parameters.AddWithValue("$kind", page.Kind);
        cmd.Parameters.AddWithValue("$slug", page.Slug);
        cmd.Parameters.AddWithValue("$name", page.Name);
        cmd.Parameters.AddWithValue("$desc", page.Description);
        cmd.Parameters.AddWithValue("$tier", page.Tier);
        cmd.Parameters.AddWithValue("$body", page.Body);
        cmd.Parameters.AddWithValue("$wid", page.WorkspaceId);
        cmd.Parameters.AddWithValue("$cat", page.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$uat", page.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$trigger", (object?)page.Trigger ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$agents", (object?)page.Agents ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tools", (object?)page.Tools ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$industry", (object?)page.Industry ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$df", (object?)page.DefaultFormat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$category", (object?)page.Category ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        string kind,
        string slug,
        string workspaceId = "",
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM knowledge_pages WHERE kind = $kind AND slug = $slug AND workspace_id = $wid";
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$slug", slug);
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<KnowledgePage>> ReadPagesAsync(
        SqliteCommand cmd,
        CancellationToken ct)
    {
        var results = new List<KnowledgePage>();
        var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var _ = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var n10 = await reader.IsDBNullAsync(10, ct).ConfigureAwait(false);
            var n11 = await reader.IsDBNullAsync(11, ct).ConfigureAwait(false);
            var n12 = await reader.IsDBNullAsync(12, ct).ConfigureAwait(false);
            var n13 = await reader.IsDBNullAsync(13, ct).ConfigureAwait(false);
            var n14 = await reader.IsDBNullAsync(14, ct).ConfigureAwait(false);
            var n15 = await reader.IsDBNullAsync(15, ct).ConfigureAwait(false);

            results.Add(new KnowledgePage(
                KnowledgeId:   reader.GetString(0),
                Kind:          reader.GetString(1),
                Slug:          reader.GetString(2),
                Name:          reader.GetString(3),
                Description:   reader.GetString(4),
                Tier:          reader.GetString(5),
                Body:          reader.GetString(6),
                WorkspaceId:   reader.GetString(7),
                CreatedAt:     DateTimeOffset.Parse(reader.GetString(8), null, DateTimeStyles.RoundtripKind),
                UpdatedAt:     DateTimeOffset.Parse(reader.GetString(9), null, DateTimeStyles.RoundtripKind),
                Trigger:       n10 ? null : reader.GetString(10),
                Agents:        n11 ? null : reader.GetString(11),
                Tools:         n12 ? null : reader.GetString(12),
                Industry:      n13 ? null : reader.GetString(13),
                DefaultFormat: n14 ? null : reader.GetString(14),
                Category:      n15 ? null : reader.GetString(15)));
        }
        return results;
    }
}
