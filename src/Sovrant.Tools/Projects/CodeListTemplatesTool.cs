using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Projects.Templates;

namespace Sovrant.Tools.Projects;

/// <summary>
/// Phase 73 — Lists all registered <see cref="IProjectTemplate"/> implementations,
/// optionally filtered by language. Companion to <see cref="CodeCreateTool"/>.
/// </summary>
public sealed class CodeListTemplatesTool : ITool
{
    private static readonly ToolDefinition s_definition = new("CodeListTemplates", CreateSchema())
    {
        Description =
            "List available project scaffold templates, optionally filtered by language. " +
            "Returns each template's ID, name, description, language, kind, and declared parameters. " +
            "Use the 'template_id' from results when calling CodeCreate.",
    };

    private readonly ProjectTemplateRegistry _registry;

    public CodeListTemplatesTool(ProjectTemplateRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var language = input.GetStringProp("language");

        IEnumerable<IProjectTemplate> templates = string.IsNullOrWhiteSpace(language)
            ? _registry.All.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            : _registry.ForLanguage(language).OrderBy(t => t.Kind, StringComparer.OrdinalIgnoreCase);

        var result = templates.Select(t => new
        {
            id = t.Id,
            name = t.Name,
            description = t.Description,
            language = t.Language,
            kind = t.Kind,
            parameters = t.Parameters.Select(p => new
            {
                name = p.Name,
                description = p.Description,
                required = p.Required,
                @default = p.Default,
            }).ToArray(),
        }).ToArray();

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            count = result.Length,
            languages = _registry.Languages.OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToArray(),
            templates = result,
        }));
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "language": {
                    "type": "string",
                    "description": "Optional language filter (e.g. 'node', 'dotnet', 'python'). Omit to list all templates."
                }
            }
        }
        """).RootElement;
}
