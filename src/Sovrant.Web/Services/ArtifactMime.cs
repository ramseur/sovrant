namespace Sovrant.Web.Services;

/// <summary>Extension-to-MIME mapping shared by the artifact list page and the artifact-serving endpoint.</summary>
internal static class ArtifactMime
{
    private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".md"] = "text/markdown; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
        [".log"] = "text/plain; charset=utf-8",
        [".json"] = "application/json",
        [".xml"] = "text/xml",
        [".yaml"] = "text/yaml",
        [".yml"] = "text/yaml",
        [".toml"] = "application/toml",
        [".csv"] = "text/csv",
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".ts"] = "text/typescript",
        [".tsx"] = "text/typescript",
        [".jsx"] = "text/jsx",
        [".cs"] = "text/x-csharp",
        [".py"] = "text/x-python",
        [".rs"] = "text/x-rust",
        [".go"] = "text/x-go",
        [".java"] = "text/x-java",
        [".kt"] = "text/x-kotlin",
        [".kts"] = "text/x-kotlin",
        [".rb"] = "text/x-ruby",
        [".php"] = "application/x-php",
        [".swift"] = "text/x-swift",
        [".sh"] = "application/x-sh",
        [".bash"] = "application/x-sh",
        [".ps1"] = "application/x-powershell",
        [".sql"] = "application/sql",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip",
        [".tar"] = "application/x-tar",
        [".gz"] = "application/gzip",
    };

    public static string For(string extension) =>
        _map.TryGetValue(extension, out var mime) ? mime : "application/octet-stream";
}
