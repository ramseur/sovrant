using System.Text.Json;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Phase 66 — parses the JSON returned from document-generation tools
/// into <see cref="DocumentArtifactViewModel"/>s. Mirrors
/// <c>DocumentArtifactCard.razor</c> on the web side; both surfaces
/// derive cards from the same response shape.
/// </summary>
internal static class DocumentArtifactParser
{
    public static bool ProducesArtifacts(string toolName) =>
        toolName is "DocumentGenerate" or "DocumentFromTemplate" or "DocumentPackage" or "Artifact";

    public static IEnumerable<DocumentArtifactViewModel> Parse(string toolName, string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !ProducesArtifacts(toolName))
            yield break;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { yield break; }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) yield break;

            if (toolName == "DocumentPackage" &&
                root.TryGetProperty("documents", out var docs) &&
                docs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in docs.EnumerateArray())
                {
                    var vm = ReadOne(item, isArtifactTool: false);
                    if (vm is not null) yield return vm;
                }
            }
            else if (toolName == "Artifact" &&
                     root.TryGetProperty("written", out var writtenArr) &&
                     writtenArr.ValueKind == JsonValueKind.Array)
            {
                // write_many response — each entry has path, size_bytes, access_url
                foreach (var item in writtenArr.EnumerateArray())
                {
                    var vm = ReadOne(item, isArtifactTool: true);
                    if (vm is not null) yield return vm;
                }
            }
            else
            {
                var vm = ReadOne(root, isArtifactTool: toolName == "Artifact");
                if (vm is not null) yield return vm;
            }
        }
    }

    private static DocumentArtifactViewModel? ReadOne(JsonElement el, bool isArtifactTool)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        var status = el.TryGetProperty("status", out var s) ? s.GetString() : null;
        var expectedStatus = isArtifactTool ? "written" : "generated";
        if (status != expectedStatus) return null;

        var path = el.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
        var url = el.TryGetProperty("access_url", out var u) ? u.GetString() : null;
        var size = el.TryGetProperty("size_bytes", out var sz) && sz.TryGetInt64(out var v) ? v : (long?)null;

        string format;
        string? templateId;
        if (isArtifactTool)
        {
            format = ExtensionToFormat(Path.GetExtension(path));
            templateId = null;
        }
        else
        {
            format = el.TryGetProperty("format", out var f) ? f.GetString() ?? "" : "";
            templateId = el.TryGetProperty("template_id", out var t) ? t.GetString() : null;
        }

        var localPath = TryFileUrlToPath(url);
        var name = !string.IsNullOrEmpty(localPath) ? Path.GetFileName(localPath)
                  : (!string.IsNullOrEmpty(path) ? Path.GetFileName(path) : (templateId ?? "document"));

        return new DocumentArtifactViewModel
        {
            DisplayName = name,
            Format = format.ToUpperInvariant(),
            SizeText = size is long n ? FormatSize(n) : string.Empty,
            Icon = FormatIcon(format),
            AccessUrl = url,
            LocalPath = localPath,
            TemplateId = templateId,
        };
    }

#pragma warning disable CA1308
    private static string ExtensionToFormat(string ext) => ext.TrimStart('.').ToLowerInvariant() switch
#pragma warning restore CA1308
    {
        "md" or "markdown" => "markdown",
        "pdf" => "pdf",
        "docx" => "word",
        "xlsx" => "excel",
        "pptx" => "powerpoint",
        var e when !string.IsNullOrEmpty(e) => e,
        _ => "file",
    };

    private static string? TryFileUrlToPath(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.IsFile)
            return u.LocalPath;
        return null;
    }

    private static string FormatIcon(string format)
    {
        var f = format.ToUpperInvariant();
        return f switch
        {
            "PDF" or "STRUCTURED_PDF" => "\uD83D\uDCC4",
            "WORD" or "DOCX" => "\uD83D\uDCDD",
            "EXCEL" or "XLSX" => "\uD83D\uDCCA",
            "POWERPOINT" or "PPTX" => "\uD83D\uDCFD\uFE0F",
            "MARKDOWN" or "MD" => "\uD83D\uDCC4",
            _ => "\uD83D\uDCCE",
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
