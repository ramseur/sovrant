using Sovrant.Agents.Models;

namespace Sovrant.Agents.Templates;

/// <summary>
/// Loads agent templates from a source (disk, database, cloud, etc.).
/// Multiple loaders can be composed — later loaders override earlier ones.
/// </summary>
public interface ITemplateLoader
{
    /// <summary>Loads templates and returns them keyed by name.</summary>
    IReadOnlyDictionary<string, AgentTemplate> Load();
}
