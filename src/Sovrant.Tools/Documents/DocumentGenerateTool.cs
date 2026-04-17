using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;

namespace Sovrant.Tools.Documents;

/// <summary>
/// Phase 66 — agent-callable document generator. Wraps
/// <see cref="IDocumentGeneratorRegistry"/> + <see cref="IArtifactStore"/>
/// so any agent (across CLI/Desktop/Web/Server/MCP) can produce Markdown,
/// simple PDF, or structured PDF output and hand back an artifact path.
/// </summary>
public sealed class DocumentGenerateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("DocumentGenerate", CreateSchema())
    {
        Description =
            "Generate a document (markdown, pdf, structured_pdf) and store it as a run-scoped artifact. " +
            "Use 'markdown' for plain .md, 'pdf' for simple text PDFs (logs, raw output), and " +
            "'structured_pdf' for styled reports with headings/lists/code blocks (markdown body). " +
            "Returns the artifact path and a file:// or presigned URL.",
    };

    private readonly IDocumentGeneratorRegistry _registry;

    public DocumentGenerateTool(IDocumentGeneratorRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var formatRaw = input.GetStringProp("format", "markdown");
        if (!TryParseFormat(formatRaw, out var format))
            return $"Error: unknown format '{formatRaw}'. Valid values: markdown, pdf, structured_pdf.";

        var body = input.GetStringProp("body");
        if (string.IsNullOrWhiteSpace(body))
            return "Error: 'body' is required.";

        var fileName = input.GetStringProp("file_name");
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = DefaultFileName(format);

        var runId = input.GetStringProp("run_id");
        if (string.IsNullOrWhiteSpace(runId))
            return "Error: 'run_id' is required — documents are written into a run-scoped artifact.";

        var scope = new ArtifactScope
        {
            WorkspaceId = input.GetStringProp("workspace_id", ArtifactScope.DefaultWorkspaceId),
            ProjectId = input.GetStringProp("project_id", ArtifactScope.DefaultProjectId),
            RunId = runId,
        };

        var request = new DocumentRequest
        {
            Format = format,
            Body = body,
            Title = input.GetStringProp("title"),
            FileName = fileName,
            Scope = scope,
        };

        try
        {
            var generator = _registry.Resolve(format);
            var result = await generator.GenerateAsync(request, ct).ConfigureAwait(false);

            return JsonSerializer.Serialize(new
            {
                status = "generated",
                format = formatRaw,
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

    private static bool TryParseFormat(string raw, out DocumentFormat format)
    {
        if (string.Equals(raw, "markdown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "md", StringComparison.OrdinalIgnoreCase))
        {
            format = DocumentFormat.Markdown; return true;
        }
        if (string.Equals(raw, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            format = DocumentFormat.Pdf; return true;
        }
        if (string.Equals(raw, "structured_pdf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "report", StringComparison.OrdinalIgnoreCase))
        {
            format = DocumentFormat.StructuredPdf; return true;
        }
        format = default; return false;
    }

    private static string DefaultFileName(DocumentFormat format) => format switch
    {
        DocumentFormat.Markdown => "document.md",
        DocumentFormat.Pdf => "document.pdf",
        DocumentFormat.StructuredPdf => "document.pdf",
        _ => "document.bin",
    };

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "format": {
                    "type": "string",
                    "enum": ["markdown", "pdf", "structured_pdf"],
                    "description": "Output format. 'markdown' writes a .md file; 'pdf' writes a simple text PDF; 'structured_pdf' renders markdown into a styled PDF with headings, lists, and code blocks."
                },
                "body": {
                    "type": "string",
                    "description": "The source content. For markdown and structured_pdf this is interpreted as Markdown; for pdf it is treated as plain text."
                },
                "title": {
                    "type": "string",
                    "description": "Optional document title used in the PDF metadata and prepended as an H1/title block."
                },
                "file_name": {
                    "type": "string",
                    "description": "Relative file name (with extension) inside the run scope. Defaults to 'document.md' or 'document.pdf' based on format."
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
            "required": ["format", "body", "run_id"]
        }
        """).RootElement;
}
