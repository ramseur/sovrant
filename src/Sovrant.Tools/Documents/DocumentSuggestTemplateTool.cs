using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Documents.Templates;
using DocFormat = Sovrant.Runtime.Documents.DocumentFormat;

namespace Sovrant.Tools.Documents;

/// <summary>
/// Phase 66.4 — natural-language template router. Given a free-text
/// prompt ("draft me an NDA with Acme"), returns the top-N matching
/// templates with their required fields so the agent can pick one and
/// collect missing data from the user in a single call rather than
/// enumerating <see cref="DocumentListTemplatesTool"/> then guessing.
/// </summary>
public sealed class DocumentSuggestTemplateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("DocumentSuggestTemplate", CreateSchema())
    {
        Description =
            "Route a natural-language request to the best-matching document template(s). " +
            "Given a free-text prompt like 'draft me an NDA between Acme and Globex', returns the " +
            "top-N templates by keyword match with each template's id, name, industry, description, " +
            "default format, match score, matched terms, and the full field schema (name, type, " +
            "required flag, description). Use this BEFORE DocumentFromTemplate to avoid listing all " +
            "templates just to route. If no match has a meaningful score, fall back to DocumentListTemplates.",
    };

    private readonly ITemplateRegistry _templates;

    public DocumentSuggestTemplateTool(ITemplateRegistry templates)
    {
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
    }

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var prompt = input.GetStringProp("prompt");
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                error = "'prompt' is required — pass the user's natural-language request.",
            }));
        }

        var limit = input.GetIntProp("limit", 5);
        if (limit <= 0) limit = 5;
        if (limit > 20) limit = 20;

        var industry = input.GetStringProp("industry");
        var candidates = string.IsNullOrWhiteSpace(industry)
            ? _templates.All
            : _templates.Find(industry, null);

        var matches = TemplateMatcher.Rank(candidates, prompt, limit);

        var payload = new
        {
            prompt,
            count = matches.Count,
            matches = matches.Select(m => new
            {
                id = m.Template.Id,
                name = m.Template.Name,
                industry = m.Template.Industry,
                description = m.Template.Description,
                default_format = FormatName(m.Template.DefaultFormat),
                score = m.Score,
                matched_terms = m.MatchedTerms,
                required_fields = m.Template.Fields.Where(f => f.Required).Select(f => f.Name).ToArray(),
                fields = ProjectFields(m.Template.Fields),
            }).ToArray(),
        };

        return Task.FromResult(JsonSerializer.Serialize(payload));
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
                "prompt": {
                    "type": "string",
                    "description": "The user's natural-language request (e.g. 'draft me an NDA between Acme and Globex for AI research')."
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of matches to return. Default 5, max 20."
                },
                "industry": {
                    "type": "string",
                    "description": "Optional industry filter (e.g. 'legal', 'finance', 'healthcare'). Narrows candidates before ranking."
                }
            },
            "required": ["prompt"]
        }
        """).RootElement;
}
