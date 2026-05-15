using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Templates;
using DocFormat = Sovrant.Runtime.Documents.DocumentFormat;

namespace Sovrant.Tools.Documents;

/// <summary>
/// Phase 66.4 — discovery tool. Lists registered document templates
/// and their declared fields so agents can see what data a template
/// expects before calling <c>DocumentFromTemplate</c>. Supports
/// optional industry and free-text filters.
/// </summary>
public sealed class DocumentListTemplatesTool : ITool
{
    private static readonly ToolDefinition s_definition = new("DocumentListTemplates", CreateSchema())
    {
        Description =
            "List registered document templates and their schemas. Each entry includes id, name, " +
            "industry, description, default_format, and the fields the template accepts (name, type, " +
            "required flag, description). Filter by industry and/or a free-text search over id/name/description.",
    };

    private readonly ITemplateRegistry _templates;

    public DocumentListTemplatesTool(ITemplateRegistry templates)
    {
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
    }

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var industry = input.GetStringProp("industry");
        var search = input.GetStringProp("search");
        var includeFields = !input.TryGetProperty("include_fields", out var inc)
            || inc.ValueKind is not JsonValueKind.False;

        var matches = _templates
            .Find(string.IsNullOrWhiteSpace(industry) ? null : industry,
                  string.IsNullOrWhiteSpace(search) ? null : search)
            .OrderBy(t => t.Industry, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                industry = t.Industry,
                description = t.Description,
                default_format = FormatName(t.DefaultFormat),
                fields = includeFields ? ProjectFields(t.Fields) : null,
            })
            .ToList();

        var payload = JsonSerializer.Serialize(new { count = matches.Count, templates = matches });
        return Task.FromResult(payload);
    }

    private static object[] ProjectFields(IReadOnlyList<TemplateField> fields) =>
        fields.Select(f => (object)new
        {
            name = f.Name,
            type = FieldTypeName(f.Type),
            required = f.Required,
            description = f.Description,
            item_fields = f.ItemFields is not null ? ProjectFields(f.ItemFields) : null,
        }).ToArray();

    private static string FormatName(DocFormat format) => format switch
    {
        DocFormat.Markdown => "markdown",
        DocFormat.Pdf => "pdf",
        DocFormat.StructuredPdf => "structured_pdf",
        DocFormat.Word => "word",
        DocFormat.Excel => "excel",
        DocFormat.PowerPoint => "powerpoint",
        _ => "unknown",
    };

    private static string FieldTypeName(TemplateFieldType type) => type switch
    {
        TemplateFieldType.String => "string",
        TemplateFieldType.Text => "text",
        TemplateFieldType.Integer => "integer",
        TemplateFieldType.Decimal => "decimal",
        TemplateFieldType.Currency => "currency",
        TemplateFieldType.Date => "date",
        TemplateFieldType.Boolean => "boolean",
        TemplateFieldType.StringArray => "string_array",
        TemplateFieldType.ObjectArray => "object_array",
        _ => "unknown",
    };

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "industry": {
                    "type": "string",
                    "description": "Optional industry filter (e.g. 'business', 'legal', 'finance', 'real-estate')."
                },
                "search": {
                    "type": "string",
                    "description": "Optional free-text search over template id, name, and description."
                },
                "include_fields": {
                    "type": "boolean",
                    "description": "Whether to include each template's field schema. Default true."
                }
            }
        }
        """).RootElement;
}
