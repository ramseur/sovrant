using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Packages;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Trust;
using DocFormat = Sovrant.Runtime.Documents.DocumentFormat;

namespace Sovrant.Tools.Documents;

/// <summary>
/// Phase 66 — renders a <see cref="DocumentPackage"/> (multiple templates
/// against the same data object) in one call. Each item runs through the
/// trust gate and the matching generator independently; partial success
/// is reported in the results array.
/// </summary>
public sealed class DocumentPackageTool : ITool
{
    private static readonly ToolDefinition s_definition = new("DocumentPackage", CreateSchema())
    {
        Description =
            "Render a registered document package (e.g. 'real-estate/closing-package') by rendering all of its " +
            "templates against the same data object in one call. Returns one result per included document. " +
            "Use 'DocumentListPackages' first to see available packages.",
    };

    private readonly IDocumentPackageRegistry _packages;
    private readonly ITemplateRegistry _templates;
    private readonly IDocumentGeneratorRegistry _generators;
    private readonly IDocumentTrustGate _trustGate;

    public DocumentPackageTool(
        IDocumentPackageRegistry packages,
        ITemplateRegistry templates,
        IDocumentGeneratorRegistry generators,
        IDocumentTrustGate trustGate)
    {
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _generators = generators ?? throw new ArgumentNullException(nameof(generators));
        _trustGate = trustGate ?? throw new ArgumentNullException(nameof(trustGate));
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var packageId = input.GetStringProp("package_id");
        if (string.IsNullOrWhiteSpace(packageId))
            return "Error: 'package_id' is required.";

        var runId = input.GetStringProp("run_id");
        if (string.IsNullOrWhiteSpace(runId))
            return "Error: 'run_id' is required — documents are written into a run-scoped artifact.";

        if (!input.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return "Error: 'data' is required and must be a JSON object containing fields for every template in the package.";

        if (!_packages.TryResolve(packageId, out var package) || package is null)
            return $"Error: unknown package '{packageId}'.";

        var scope = new ArtifactScope
        {
            WorkspaceId = input.GetStringProp("workspace_id", ArtifactScope.DefaultWorkspaceId),
            ProjectId = input.GetStringProp("project_id", ArtifactScope.DefaultProjectId),
            RunId = runId,
        };

        var results = new List<object>(package.Items.Count);

        foreach (var item in package.Items)
        {
            if (!_templates.TryResolve(item.TemplateId, out var template) || template is null)
            {
                results.Add(new
                {
                    template_id = item.TemplateId,
                    status = "error",
                    error = "unknown template — package references a template not registered in the runtime.",
                });
                continue;
            }

            var decision = _trustGate.Evaluate(template, data);
            if (!decision.IsAllowed)
            {
                results.Add(new
                {
                    template_id = template.Id,
                    status = "refused",
                    error = decision.DenyReason,
                });
                continue;
            }

            TemplateRenderResult rendered;
            try { rendered = template.Render(data); }
            catch (TemplateValidationException ex)
            {
                results.Add(new { template_id = template.Id, status = "error", error = ex.Message });
                continue;
            }

            var format = item.Format ?? rendered.Format;
            var request = new DocumentRequest
            {
                Format = format,
                Body = rendered.Body,
                Title = rendered.Title ?? template.Name,
                FileName = rendered.DefaultFileName,
                Scope = scope,
            };

            try
            {
                var generator = _generators.Resolve(format);
                var result = await generator.GenerateAsync(request, ct).ConfigureAwait(false);
                results.Add(new
                {
                    template_id = template.Id,
                    status = "generated",
                    format = FormatName(format),
                    path = result.RelativePath,
                    content_type = result.ContentType,
                    size_bytes = result.SizeBytes,
                    access_url = result.AccessUrl?.ToString(),
                });
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
            {
                results.Add(new { template_id = template.Id, status = "error", error = ex.Message });
            }
        }

        var generatedCount = results.Count(r =>
            r.GetType().GetProperty("status")?.GetValue(r) as string == "generated");

        return JsonSerializer.Serialize(new
        {
            status = generatedCount == package.Items.Count ? "complete" : "partial",
            package_id = package.Id,
            package_name = package.Name,
            run_id = scope.RunId,
            workspace_id = scope.WorkspaceId,
            project_id = scope.ProjectId,
            generated = generatedCount,
            total = package.Items.Count,
            documents = results,
        });
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

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "package_id": {
                    "type": "string",
                    "description": "Canonical package ID (e.g. 'real-estate/closing-package', 'business/onboarding-package', 'healthcare/intake-package')."
                },
                "data": {
                    "type": "object",
                    "description": "Data object covering the union of fields required by every template in the package."
                },
                "run_id": {
                    "type": "string",
                    "description": "Run/session ID that scopes the artifacts. Required."
                },
                "workspace_id": { "type": "string", "description": "Workspace ID (defaults to 'personal')." },
                "project_id": { "type": "string", "description": "Project ID (defaults to 'default-project')." }
            },
            "required": ["package_id", "data", "run_id"]
        }
        """).RootElement;
}
