using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Generators;

namespace Sovrant.Runtime.Documents.Tests;

public class DocumentGeneratorTests : IDisposable
{
    private readonly string _artifactRoot;
    private readonly LocalArtifactStore _store;

    public DocumentGeneratorTests()
    {
        _artifactRoot = Path.Combine(Path.GetTempPath(), "sovrant-doc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_artifactRoot);
        _store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _artifactRoot);
    }

    [Fact]
    public async Task Markdown_generator_writes_body_as_utf8()
    {
        var generator = new MarkdownGenerator(_store);
        var request = BuildRequest(DocumentFormat.Markdown, "# Hello\n\nWorld.", "hello.md");

        var result = await generator.GenerateAsync(request);

        Assert.Equal("hello.md", result.RelativePath);
        Assert.StartsWith("text/markdown", result.ContentType, StringComparison.Ordinal);
        Assert.True(result.SizeBytes > 0);

        using var stream = await _store.ReadAsync(result.Handle, result.RelativePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("# Hello\n\nWorld.", content);
    }

    [Fact]
    public async Task Markdown_generator_prepends_title_when_missing_h1()
    {
        var generator = new MarkdownGenerator(_store);
        var request = BuildRequest(DocumentFormat.Markdown, "Body only.", "titled.md") with { Title = "My Title" };

        var result = await generator.GenerateAsync(request);

        using var stream = await _store.ReadAsync(result.Handle, result.RelativePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.StartsWith("# My Title", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PdfSharp_generator_produces_valid_pdf_header()
    {
        var generator = new PdfSharpGenerator(_store);
        var request = BuildRequest(DocumentFormat.Pdf, "Line 1\nLine 2\nLine 3", "simple.pdf")
            with { Title = "Simple PDF" };

        var result = await generator.GenerateAsync(request);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(result.SizeBytes > 100, "PDF output should be non-trivial.");

        using var stream = await _store.ReadAsync(result.Handle, result.RelativePath);
        var header = new byte[4];
        var read = await stream.ReadAsync(header.AsMemory(0, 4));
        Assert.Equal(4, read);
        Assert.Equal((byte)'%', header[0]);
        Assert.Equal((byte)'P', header[1]);
        Assert.Equal((byte)'D', header[2]);
        Assert.Equal((byte)'F', header[3]);
    }

    [Fact]
    public async Task MigraDoc_generator_renders_markdown_into_pdf()
    {
        var generator = new MigraDocGenerator(_store);
        var body = """
                   # Heading

                   This is **bold** and *italic* and `code`.

                   - First item
                   - Second item

                   ```csharp
                   var x = 1;
                   ```
                   """;
        var request = BuildRequest(DocumentFormat.StructuredPdf, body, "report.pdf")
            with { Title = "Structured Report" };

        var result = await generator.GenerateAsync(request);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(result.SizeBytes > 500);

        using var stream = await _store.ReadAsync(result.Handle, result.RelativePath);
        var header = new byte[4];
        var read = await stream.ReadAsync(header.AsMemory(0, 4));
        Assert.Equal(4, read);
        Assert.Equal((byte)'%', header[0]);
    }

    [Fact]
    public void Registry_resolves_each_registered_format()
    {
        var generators = new IDocumentGenerator[]
        {
            new MarkdownGenerator(_store),
            new PdfSharpGenerator(_store),
            new MigraDocGenerator(_store),
        };
        var registry = new DocumentGeneratorRegistry(generators);

        Assert.Equal(DocumentFormat.Markdown, registry.Resolve(DocumentFormat.Markdown).Format);
        Assert.Equal(DocumentFormat.Pdf, registry.Resolve(DocumentFormat.Pdf).Format);
        Assert.Equal(DocumentFormat.StructuredPdf, registry.Resolve(DocumentFormat.StructuredPdf).Format);
        Assert.Equal(3, registry.SupportedFormats.Count);
    }

    [Fact]
    public void Registry_throws_for_unregistered_format()
    {
        var registry = new DocumentGeneratorRegistry(Array.Empty<IDocumentGenerator>());
        Assert.Throws<NotSupportedException>(() => registry.Resolve(DocumentFormat.Pdf));
    }

    private static DocumentRequest BuildRequest(DocumentFormat format, string body, string fileName) => new()
    {
        Format = format,
        Body = body,
        FileName = fileName,
        Scope = new ArtifactScope
        {
            WorkspaceId = "ws-test",
            ProjectId = ArtifactScope.DefaultProjectId,
            RunId = $"test-{Guid.NewGuid():N}",
        },
    };

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_artifactRoot))
                Directory.Delete(_artifactRoot, recursive: true);
        }
        catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
