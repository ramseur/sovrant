using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Generators;

namespace Sovrant.Runtime.Documents.Tests;

public class OfficeDocumentGeneratorTests : IDisposable
{
    private readonly string _artifactRoot;
    private readonly LocalArtifactStore _store;

    public OfficeDocumentGeneratorTests()
    {
        _artifactRoot = Path.Combine(Path.GetTempPath(), "sovrant-office-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_artifactRoot);
        _store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _artifactRoot);
    }

    [Fact]
    public async Task Word_generator_produces_valid_docx()
    {
        var generator = new WordDocumentGenerator(_store);
        var body = """
                   # Chapter One

                   First paragraph with **bold** and *italic*.

                   - Item A
                   - Item B

                   ```
                   code sample
                   ```
                   """;
        var request = BuildRequest(DocumentFormat.Word, body, "chapter.docx") with { Title = "Chapter Title" };

        var result = await generator.GenerateAsync(request);

        Assert.EndsWith("wordprocessingml.document", result.ContentType, StringComparison.Ordinal);
        Assert.True(result.SizeBytes > 500);
        AssertZipSignature(result);
    }

    [Fact]
    public async Task Excel_generator_writes_json_rows_and_headers()
    {
        var generator = new ExcelDocumentGenerator(_store);
        var body = """{"headers":["Name","Score"],"rows":[["Ada",95],["Grace",88]]}""";
        var request = BuildRequest(DocumentFormat.Excel, body, "scores.xlsx") with { Title = "Scores" };

        var result = await generator.GenerateAsync(request);

        Assert.EndsWith("spreadsheetml.sheet", result.ContentType, StringComparison.Ordinal);
        Assert.True(result.SizeBytes > 500);
        AssertZipSignature(result);
    }

    [Fact]
    public async Task Excel_generator_falls_back_to_csv()
    {
        var generator = new ExcelDocumentGenerator(_store);
        var body = "Name,Score\nAda,95\nGrace,88";
        var request = BuildRequest(DocumentFormat.Excel, body, "csv.xlsx");

        var result = await generator.GenerateAsync(request);

        Assert.True(result.SizeBytes > 500);
        AssertZipSignature(result);
    }

    [Fact]
    public async Task PowerPoint_generator_splits_slides_on_h1()
    {
        var generator = new PowerPointGenerator(_store);
        var body = """
                   # Slide One

                   - Point A
                   - Point B

                   # Slide Two

                   Some body text.
                   """;
        var request = BuildRequest(DocumentFormat.PowerPoint, body, "deck.pptx") with { Title = "Deck" };

        var result = await generator.GenerateAsync(request);

        Assert.EndsWith("presentationml.presentation", result.ContentType, StringComparison.Ordinal);
        Assert.True(result.SizeBytes > 1000);
        AssertZipSignature(result);
    }

    [Fact]
    public void Registry_includes_office_formats_when_registered()
    {
        var generators = new IDocumentGenerator[]
        {
            new WordDocumentGenerator(_store),
            new ExcelDocumentGenerator(_store),
            new PowerPointGenerator(_store),
        };
        var registry = new DocumentGeneratorRegistry(generators);

        Assert.Equal(DocumentFormat.Word, registry.Resolve(DocumentFormat.Word).Format);
        Assert.Equal(DocumentFormat.Excel, registry.Resolve(DocumentFormat.Excel).Format);
        Assert.Equal(DocumentFormat.PowerPoint, registry.Resolve(DocumentFormat.PowerPoint).Format);
    }

    private void AssertZipSignature(DocumentResult result)
    {
        // All OpenXml + xlsx files are ZIP archives — first 2 bytes are 'PK'.
        using var stream = _store.ReadAsync(result.Handle, result.RelativePath).GetAwaiter().GetResult();
        var header = new byte[2];
        var read = stream.Read(header, 0, 2);
        Assert.Equal(2, read);
        Assert.Equal((byte)'P', header[0]);
        Assert.Equal((byte)'K', header[1]);
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
