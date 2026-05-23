using Sovrant.Agents.Models;

namespace Sovrant.Agents.Templates;

/// <summary>
/// Persists agent definitions to <c>.sovrant/agents/</c> (the user tier).
/// Built-in agents are read-only; editing one triggers a silent copy-on-write
/// that lands in the user tier and overrides the built-in going forward.
/// </summary>
public sealed class AgentDefinitionWriter
{
    private readonly AgentTemplateRegistry _registry;

    public AgentDefinitionWriter(AgentTemplateRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Writes <paramref name="template"/> to <c>.sovrant/agents/{name}.md</c> and
    /// updates the live registry. Always writes to the user tier; if the template
    /// was built-in the user copy takes precedence from this point on.
    /// </summary>
    public async Task SaveAsync(AgentTemplate template, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        Directory.CreateDirectory(AgentTemplateRegistry.UserAgentsDir);

        var path = AgentPath(template.Name);
        var content = AgentTemplateRegistry.Serialize(template with { IsBuiltIn = false });

        await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);

        _registry.Register(template with { IsBuiltIn = false });
    }

    /// <summary>
    /// Deletes the user-tier markdown file for <paramref name="name"/> and removes
    /// it from the live registry. If the name belongs to a built-in template, returns
    /// <see langword="false"/> (built-ins cannot be deleted).
    /// </summary>
    public bool Delete(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var existing = _registry.TryGet(name);
        if (existing?.IsBuiltIn == true) return false;

        var path = AgentPath(name);
        if (File.Exists(path))
            File.Delete(path);

        _registry.Unregister(name);
        return true;
    }

    /// <summary>Returns the kebab-case name derived from a display name.</summary>
    public static string ToKebabCase(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "new-agent";

#pragma warning disable CA1308 // kebab-case explicitly requires lowercase output
        return System.Text.RegularExpressions.Regex.Replace(
            displayName.Trim().ToLowerInvariant(),
            @"[^a-z0-9]+", "-").Trim('-');
#pragma warning restore CA1308
    }

    private static string AgentPath(string name) =>
        Path.Combine(AgentTemplateRegistry.UserAgentsDir, $"{name}.md");
}
