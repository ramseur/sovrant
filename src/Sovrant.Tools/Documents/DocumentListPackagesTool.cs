using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Documents.Packages;

namespace Sovrant.Tools.Documents;

/// <summary>
/// Phase 66 — discovery tool for <see cref="DocumentPackage"/>s. Mirrors
/// <c>DocumentListTemplates</c> but returns package definitions: id,
/// name, description, and the list of templates included.
/// </summary>
public sealed class DocumentListPackagesTool : ITool
{
    private static readonly ToolDefinition s_definition = new("DocumentListPackages", CreateSchema())
    {
        Description =
            "List the document packages registered in the runtime. Each package bundles multiple templates " +
            "rendered against the same data object via 'DocumentPackage'.",
    };

    private readonly IDocumentPackageRegistry _packages;

    public DocumentListPackagesTool(IDocumentPackageRegistry packages)
    {
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
    }

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new
        {
            packages = _packages.All.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                description = p.Description,
                templates = p.Items.Select(i => i.TemplateId).ToArray(),
            }).ToArray(),
        });
        return Task.FromResult(json);
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        { "type": "object", "properties": {} }
        """).RootElement;
}
