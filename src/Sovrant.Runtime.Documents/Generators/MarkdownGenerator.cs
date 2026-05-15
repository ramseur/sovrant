using System.Text;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Documents.Generators;

/// <summary>
/// Writes the request body verbatim as a UTF-8 Markdown file. Intentionally
/// does not reformat or round-trip through Markdig — the body is already
/// Markdown; re-parsing would only introduce drift.
/// </summary>
public sealed class MarkdownGenerator : DocumentGeneratorBase
{
    public MarkdownGenerator(IArtifactStore artifactStore) : base(artifactStore) { }

    public override DocumentFormat Format => DocumentFormat.Markdown;

    protected override string ContentType => "text/markdown; charset=utf-8";

    protected override Task<byte[]> RenderAsync(DocumentRequest request, CancellationToken ct)
    {
        var body = request.Body;
        if (!string.IsNullOrWhiteSpace(request.Title) &&
            !body.StartsWith("# ", StringComparison.Ordinal))
        {
            body = $"# {request.Title}\n\n{body}";
        }
        return Task.FromResult(Encoding.UTF8.GetBytes(body));
    }
}
