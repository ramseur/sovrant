using System.Text.Json;
using Sovrant.Agents.Models;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Agents.Templates;

/// <summary>
/// Persists agent definitions to <see cref="IKnowledgeStore"/> at global user scope (Phase 112).
/// Built-in agents are immutable base rows; editing one creates a User-tier overlay that
/// wins via copy-on-write — the base row is never mutated. "Delete" removes the overlay,
/// restoring the base.
/// </summary>
public sealed class AgentDefinitionWriter
{
    private readonly AgentTemplateRegistry _registry;
    private readonly IKnowledgeStore _store;

    public AgentDefinitionWriter(AgentTemplateRegistry registry, IKnowledgeStore store)
    {
        _registry = registry;
        _store = store;
    }

    /// <summary>
    /// Upserts <paramref name="template"/> into the global user tier and updates the live registry.
    /// If the template was built-in, the overlay takes precedence going forward; the base row is untouched.
    /// </summary>
    public async Task SaveAsync(AgentTemplate template, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var now = DateTimeOffset.UtcNow;
        var page = new KnowledgePage(
            KnowledgeId:      $"agent_global_{template.Name}",
            Kind:             "agents",
            Slug:             template.Name,
            Name:             template.Name,
            Description:      template.Description ?? string.Empty,
            Tier:             "User",
            Body:             template.SystemPrompt,
            WorkspaceId:      KnowledgeScope.Global,
            CreatedAt:        now,
            UpdatedAt:        now,
            Tools:            template.AllowedTools.Count > 0
                                  ? JsonSerializer.Serialize(template.AllowedTools)
                                  : null,
            Role:             template.Role.ToString(),
            RecommendedLevel: template.RecommendedLevel.ToString());

        await _store.UpsertAsync(page, ct).ConfigureAwait(false);
        _registry.Register(template with { IsBuiltIn = false });
    }

    /// <summary>
    /// Deletes the global user-tier overlay for <paramref name="name"/>. Built-in agents
    /// cannot be deleted (only their overlay can be removed, reverting to the built-in base).
    /// Returns <see langword="false"/> if no user overlay exists.
    /// </summary>
    public async Task<bool> DeleteAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Only remove User-tier overlays; BuiltIn rows are owned by the migration.
        var existing = _registry.TryGet(name);
        if (existing?.IsBuiltIn == true) return false;

        await _store.DeleteAsync("agents", name, KnowledgeScope.Global, ct).ConfigureAwait(false);
        _registry.Unregister(name);

        // Restore the BuiltIn row into the live registry if one exists.
        var baseRow = _store.GetAsync("agents", name, KnowledgeScope.Base, ct).GetAwaiter().GetResult();
        if (baseRow is not null)
        {
            if (!Enum.TryParse<AgentRole>(baseRow.Role, ignoreCase: true, out var role))
                role = AgentRole.General;
            if (!Enum.TryParse<RecommendedLevel>(baseRow.RecommendedLevel, ignoreCase: true, out var level))
                level = RecommendedLevel.Standard;
            var tools = DeserializeList(baseRow.Tools);
            _registry.Register(new AgentTemplate(baseRow.Name, role, level, tools, baseRow.Body, IsBuiltIn: true, baseRow.Description));
        }

        return true;
    }

    /// <summary>Synchronous delete shim for callers that have not yet migrated to async (legacy).</summary>
    public bool Delete(string name) => DeleteAsync(name).GetAwaiter().GetResult();

    /// <summary>Returns the kebab-case name derived from a display name.</summary>
    public static string ToKebabCase(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "new-agent";
#pragma warning disable CA1308
        return System.Text.RegularExpressions.Regex.Replace(
            displayName.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
#pragma warning restore CA1308
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
