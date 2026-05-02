using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Trust;
using Sovrant.Runtime.Workspaces;
using DocFormat = Sovrant.Runtime.Documents.DocumentFormat;

namespace Sovrant.Tools.Documents;

/// <summary>
/// Phase 66.4 — renders a registered <see cref="IDocumentTemplate"/>
/// into an artifact. Validates the caller's <c>data</c> object against
/// the template's declared fields, runs <see cref="IDocumentTemplate.Render"/>,
/// then pipes the result into the matching generator so the
/// artifact write + URL path matches <c>DocumentGenerate</c>.
/// </summary>
public sealed class DocumentFromTemplateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("DocumentFromTemplate", CreateSchema())
    {
        Description =
            "Render a registered document template (e.g. 'business/invoice', 'legal/nda') into a run-scoped artifact. " +
            "The template declares which fields it needs — pass them under 'data' as a JSON object. " +
            "Use 'DocumentListTemplates' first to see available templates and their fields. " +
            "Returns the artifact path + access URL, identical to DocumentGenerate.",
    };

    private readonly ITemplateRegistry _templates;
    private readonly IDocumentGeneratorRegistry _generators;
    private readonly IDocumentTrustGate _trustGate;

    public DocumentFromTemplateTool(
        ITemplateRegistry templates,
        IDocumentGeneratorRegistry generators,
        IDocumentTrustGate trustGate)
    {
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _generators = generators ?? throw new ArgumentNullException(nameof(generators));
        _trustGate = trustGate ?? throw new ArgumentNullException(nameof(trustGate));
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var templateId = input.GetStringProp("template_id");
        if (string.IsNullOrWhiteSpace(templateId))
            return "Error: 'template_id' is required.";

        var runId = input.GetStringProp("run_id");
        if (string.IsNullOrWhiteSpace(runId))
            return "Error: 'run_id' is required — documents are written into a run-scoped artifact.";

        if (!input.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return "Error: 'data' is required and must be a JSON object matching the template's fields.";

        if (!_templates.TryResolve(templateId, out var template) || template is null)
            return $"Error: unknown template '{templateId}'. Use DocumentListTemplates to see available templates.";

        var decision = _trustGate.Evaluate(template, data);
        if (!decision.IsAllowed)
            return $"Error: trust gate refused generation. {decision.DenyReason}";

        TemplateRenderResult rendered;
        try
        {
            rendered = template.Render(data);
        }
        catch (TemplateValidationException ex)
        {
            return $"Error: {ex.Message}";
        }

        var format = rendered.Format;
        var formatOverride = input.GetStringProp("format");
        if (!string.IsNullOrWhiteSpace(formatOverride))
        {
            if (!TryParseFormat(formatOverride, out var overridden))
                return $"Error: unknown format override '{formatOverride}'.";
            format = overridden;
        }

        var fileName = input.GetStringProp("file_name");
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = rendered.DefaultFileName;

        var scope = new ArtifactScope
        {
            WorkspaceId = input.GetStringProp("workspace_id", WorkspaceIdentity.DefaultPersonal()),
            ProjectId = input.GetStringProp("project_id", ArtifactScope.DefaultProjectId),
            RunId = runId,
        };

        var title = input.GetStringProp("title");
        if (string.IsNullOrWhiteSpace(title)) title = rendered.Title ?? template.Name;

        var request = new DocumentRequest
        {
            Format = format,
            Body = rendered.Body,
            Title = title,
            FileName = fileName,
            Scope = scope,
        };

        try
        {
            var generator = _generators.Resolve(format);
            var result = await generator.GenerateAsync(request, ct).ConfigureAwait(false);

            return JsonSerializer.Serialize(new
            {
                status = "generated",
                template_id = template.Id,
                format = FormatName(format),
                path = result.RelativePath,
                content_type = result.ContentType,
                size_bytes = result.SizeBytes,
                access_url = result.AccessUrl?.ToString(),
                run_id = scope.RunId,
                workspace_id = scope.WorkspaceId,
                project_id = scope.ProjectId,
            });
        }
        catch (NotSupportedException ex)
        {
            return $"Error: {ex.Message}";
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

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

    private static bool TryParseFormat(string raw, out DocFormat format)
    {
        if (string.Equals(raw, "markdown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "md", StringComparison.OrdinalIgnoreCase))
        { format = DocFormat.Markdown; return true; }
        if (string.Equals(raw, "pdf", StringComparison.OrdinalIgnoreCase))
        { format = DocFormat.Pdf; return true; }
        if (string.Equals(raw, "structured_pdf", StringComparison.OrdinalIgnoreCase))
        { format = DocFormat.StructuredPdf; return true; }
        if (string.Equals(raw, "word", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "docx", StringComparison.OrdinalIgnoreCase))
        { format = DocFormat.Word; return true; }
        if (string.Equals(raw, "excel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "xlsx", StringComparison.OrdinalIgnoreCase))
        { format = DocFormat.Excel; return true; }
        if (string.Equals(raw, "powerpoint", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "pptx", StringComparison.OrdinalIgnoreCase))
        { format = DocFormat.PowerPoint; return true; }
        format = default; return false;
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "template_id": {
                    "type": "string",
                    "description": "Canonical template ID (e.g. 'business/invoice', 'business/meeting-notes', 'legal/nda', 'finance/expense-report', 'real-estate/property-listing')."
                },
                "data": {
                    "type": "object",
                    "description": "The data object matching the template's declared fields. Call DocumentListTemplates to see the schema for a specific template."
                },
                "format": {
                    "type": "string",
                    "enum": ["markdown", "pdf", "structured_pdf", "word", "excel", "powerpoint"],
                    "description": "Optional format override. Defaults to the template's DefaultFormat."
                },
                "title": {
                    "type": "string",
                    "description": "Optional title override. Defaults to the template's rendered title."
                },
                "file_name": {
                    "type": "string",
                    "description": "Optional file name override. Defaults to the template's suggested name."
                },
                "run_id": {
                    "type": "string",
                    "description": "Run/session ID that scopes the artifact. Required."
                },
                "workspace_id": {
                    "type": "string",
                    "description": "Workspace ID (defaults to 'personal')."
                },
                "project_id": {
                    "type": "string",
                    "description": "Project ID (defaults to 'default-project')."
                }
            },
            "required": ["template_id", "data", "run_id"]
        }
        """).RootElement;
}
