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
    string? Category = null,
    // agents-specific
    string? Role = null,
    string? RecommendedLevel = null,
    // document-templates-specific (Phase 112D)
    string? FieldsJson = null,
    string? FilenameTemplate = null);

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

    /// <summary>
    /// Resolves the effective row for a kind + slug using copy-on-write overlay layering
    /// (Phase 109): project override (<paramref name="projectId"/>) wins over the global
    /// user override (workspace_id = 'global'), which wins over the immutable base
    /// (workspace_id = ''). Returns null if nothing matches.
    /// </summary>
    Task<KnowledgePage?> GetEffectiveAsync(string kind, string slug, string projectId = "", CancellationToken ct = default);

    /// <summary>
    /// Returns the effective set of rows for a kind: every base row, with any global or
    /// project overlay shadowing the base of the same slug (project &gt; global &gt; base).
    /// Used to populate the in-memory registries (skills, agents, tool/doc templates).
    /// </summary>
    Task<IReadOnlyList<KnowledgePage>> GetAllEffectiveAsync(string kind, string projectId = "", CancellationToken ct = default);

    Task UpsertAsync(KnowledgePage page, CancellationToken ct = default);
    Task DeleteAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default);
}

/// <summary>Well-known workspace_id scopes for the copy-on-write overlay model (Phase 112).</summary>
public static class KnowledgeScope
{
    /// <summary>Immutable factory content seeded by migrations.</summary>
    public const string Base = "";

    /// <summary>User's installation-wide override/creation tier.</summary>
    public const string Global = "global";

    /// <summary>
    /// Returns a stable workspace_id for the given project root directory.
    /// Uses the normalized absolute path so the same directory always maps to the same scope.
    /// </summary>
    public static string ProjectIdFor(string directory) =>
        "project:" + Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
