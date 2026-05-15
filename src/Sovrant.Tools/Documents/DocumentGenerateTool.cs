using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Workspaces;
using DocFormat = Sovrant.Runtime.Documents.DocumentFormat;

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
            "Generate a document and store it as a run-scoped artifact. Supported formats: " +
            "'markdown' (.md verbatim), 'pdf' (simple text PDFs for logs/raw output), " +
            "'structured_pdf' (styled PDF from markdown — headings, lists, code), " +
            "'word' (.docx from markdown), 'excel' (.xlsx from JSON {headers, rows}), " +
            "'powerpoint' (.pptx where each H1 in markdown becomes a new slide). " +
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
            WorkspaceId = input.GetStringProp("workspace_id", WorkspaceIdentity.DefaultPersonal()),
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

    private static bool TryParseFormat(string raw, out DocFormat format)
    {
        if (string.Equals(raw, "markdown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "md", StringComparison.OrdinalIgnoreCase))
        {
            format = DocFormat.Markdown; return true;
        }
        if (string.Equals(raw, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            format = DocFormat.Pdf; return true;
        }
        if (string.Equals(raw, "structured_pdf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "report", StringComparison.OrdinalIgnoreCase))
        {
            format = DocFormat.StructuredPdf; return true;
        }
        if (string.Equals(raw, "word", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "docx", StringComparison.OrdinalIgnoreCase))
        {
            format = DocFormat.Word; return true;
        }
        if (string.Equals(raw, "excel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            format = DocFormat.Excel; return true;
        }
        if (string.Equals(raw, "powerpoint", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "pptx", StringComparison.OrdinalIgnoreCase))
        {
            format = DocFormat.PowerPoint; return true;
        }
        format = default; return false;
    }

    private static string DefaultFileName(DocFormat format) => format switch
    {
        DocFormat.Markdown => "document.md",
        DocFormat.Pdf => "document.pdf",
        DocFormat.StructuredPdf => "document.pdf",
        DocFormat.Word => "document.docx",
        DocFormat.Excel => "document.xlsx",
        DocFormat.PowerPoint => "document.pptx",
        _ => "document.bin",
    };

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "format": {
                    "type": "string",
                    "enum": ["markdown", "pdf", "structured_pdf", "word", "excel", "powerpoint"],
                    "description": "Output format. 'markdown' writes a .md file; 'pdf' writes a simple text PDF; 'structured_pdf' renders markdown into a styled PDF; 'word' writes a .docx from markdown; 'excel' expects a JSON body {headers:[...], rows:[[...]]}; 'powerpoint' writes a .pptx where each H1 starts a new slide."
                },
                "body": {
                    "type": "string",
                    "description": "The source content. Markdown/structured_pdf/word/powerpoint expect Markdown; pdf expects plain text; excel expects a JSON string {\"headers\":[\"A\",\"B\"],\"rows\":[[1,2],[3,4]]}."
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
