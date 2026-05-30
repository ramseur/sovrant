namespace Sovrant.Runtime.Knowledge;

/// <summary>A single knowledge entry persisted in the DB (skill, document template, or tool template).</summary>
public sealed record KnowledgePage(
    string KnowledgeId,
    string Kind,
    string Slug,
    string Name,
    string Description,
    string Tier,
    string Body,
    string WorkspaceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    // skills-specific
    string? Trigger = null,
    string? Agents = null,
    string? Tools = null,
    // documents-specific
    string? Industry = null,
    string? DefaultFormat = null,
    // tool-templates-specific
    string? Category = null);

/// <summary>Persistent store for <see cref="KnowledgePage"/> entries.</summary>
public interface IKnowledgeStore
{
    Task<IReadOnlyList<KnowledgePage>> GetAllAsync(string kind, string workspaceId = "", CancellationToken ct = default);
    Task<KnowledgePage?> GetAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default);

    /// <summary>
    /// Returns the highest-priority row for the given kind + slug, regardless of workspace scope.
    /// User tier always wins over BuiltIn. Used by CodeCreateTool to get the active language guideline.
    /// </summary>
    Task<KnowledgePage?> GetActiveAsync(string kind, string slug, CancellationToken ct = default);

    Task UpsertAsync(KnowledgePage page, CancellationToken ct = default);
    Task DeleteAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default);
}
