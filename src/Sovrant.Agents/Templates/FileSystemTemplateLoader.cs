using Sovrant.Agents.Models;

namespace Sovrant.Agents.Templates;

/// <summary>
/// Default <see cref="ITemplateLoader"/> that reads <c>*.md</c> files from a directory.
/// Uses the same parsing logic as <see cref="AgentTemplateRegistry"/>.
/// </summary>
public sealed class FileSystemTemplateLoader : ITemplateLoader
{
    private readonly string[] _directories;

    /// <summary>Creates a loader that scans the given directories in order (later dirs override earlier).</summary>
    public FileSystemTemplateLoader(params string[] directories)
    {
        _directories = directories;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, AgentTemplate> Load()
    {
        var result = new Dictionary<string, AgentTemplate>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in _directories)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
            {
                var template = AgentTemplateRegistry.ParseTemplateFile(file);
                if (template is not null)
                    result[template.Name] = template;
            }
        }

        return result;
    }
}
