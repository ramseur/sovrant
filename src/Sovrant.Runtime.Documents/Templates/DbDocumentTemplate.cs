using System.Text.Json;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// <see cref="IDocumentTemplate"/> backed by a <see cref="KnowledgePage"/> from the
/// <c>document-templates</c> kind in the knowledge store (Phase 112D). The template
/// body is a Scriban template; fields are deserialized from <c>fields_json</c>.
/// </summary>
public sealed class DbDocumentTemplate : IDocumentTemplate
{
    private readonly KnowledgePage _page;
    private readonly IReadOnlyList<TemplateField> _fields;

    public DbDocumentTemplate(KnowledgePage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page   = page;
        _fields = string.IsNullOrWhiteSpace(page.FieldsJson)
            ? Array.Empty<TemplateField>()
            : TemplateFieldSerializer.Deserialize(page.FieldsJson);
    }

    public string Id => _page.Slug;
    public string Name => _page.Name;
    public string Industry => _page.Industry ?? "general";
    public string Description => _page.Description;

    public DocumentFormat DefaultFormat =>
        Enum.TryParse<DocumentFormat>(_page.DefaultFormat, ignoreCase: true, out var fmt)
            ? fmt
            : DocumentFormat.Markdown;

    public IReadOnlyList<TemplateField> Fields => _fields;

    public TemplateRenderResult Render(JsonElement data)
    {
        TemplateData.Validate(data, _fields);

        var body     = ScribanRenderer.Render(_page.Body, data);
        var fileName = BuildFileName(data);
        var title    = ExtractTitle(body, _page.Name);

        return new TemplateRenderResult(DefaultFormat, body, fileName, title);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string BuildFileName(JsonElement data)
    {
        if (!string.IsNullOrWhiteSpace(_page.FilenameTemplate))
        {
            try { return ScribanRenderer.Render(_page.FilenameTemplate, data).Trim(); }
            catch { /* fall through to default */ }
        }

        var ext = DefaultFormat switch
        {
            DocumentFormat.Word         => "docx",
            DocumentFormat.Excel        => "xlsx",
            DocumentFormat.PowerPoint   => "pptx",
            DocumentFormat.StructuredPdf=> "pdf",
            DocumentFormat.Pdf          => "pdf",
            _                           => "md",
        };
        return $"{TemplateFormat.Slug(_page.Slug, "document")}.{ext}";
    }

    private static string? ExtractTitle(string body, string fallback)
    {
        // Extract first H1 heading as the document title
        var line = body.AsSpan().TrimStart();
        if (line.StartsWith("# ", StringComparison.Ordinal))
        {
            var newline = line.IndexOf('\n');
            var heading = (newline >= 0 ? line[2..newline] : line[2..]).ToString().Trim();
            return string.IsNullOrWhiteSpace(heading) ? fallback : heading;
        }
        return fallback;
    }
}
