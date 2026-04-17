using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Generators;

namespace Sovrant.Runtime.Documents.Tests;

/// <summary>
/// Second-level validation: open the generated files back through the
/// OpenXml SDK. "Valid ZIP" is not the same as "valid .docx/.xlsx/.pptx"
/// — these tests catch schema/relationship bugs that ZIP-header checks miss.
/// </summary>
public class OpenXmlRoundTripTests : IDisposable
{
    private readonly string _artifactRoot;
    private readonly LocalArtifactStore _store;

    public OpenXmlRoundTripTests()
    {
        _artifactRoot = Path.Combine(Path.GetTempPath(), "sovrant-ox-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_artifactRoot);
        _store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _artifactRoot);
    }

    [Fact]
    public async Task Generated_docx_reopens_through_openxml()
    {
        var generator = new WordDocumentGenerator(_store);
        var request = BuildRequest(DocumentFormat.Word, "# Heading\n\nBody.\n\n- One\n- Two", "roundtrip.docx");

        var result = await generator.GenerateAsync(request);
        using var stream = await _store.ReadAsync(result.Handle, result.RelativePath);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        copy.Position = 0;

        using var wordDoc = WordprocessingDocument.Open(copy, isEditable: false);
        Assert.NotNull(wordDoc.MainDocumentPart);
        var documentBody = wordDoc.MainDocumentPart!.Document.Body;
        Assert.NotNull(documentBody);
        Assert.True(documentBody!.ChildElements.Count > 0);
    }

    [Fact]
    public async Task Generated_pptx_reopens_with_expected_slide_count()
    {
        var generator = new PowerPointGenerator(_store);
        var body = """
                   # First

                   - A

                   # Second

                   - B
                   """;
        var request = BuildRequest(DocumentFormat.PowerPoint, body, "roundtrip.pptx");

        var result = await generator.GenerateAsync(request);
        using var stream = await _store.ReadAsync(result.Handle, result.RelativePath);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        copy.Position = 0;

        using var pptx = PresentationDocument.Open(copy, isEditable: false);
        Assert.NotNull(pptx.PresentationPart);
        Assert.Equal(2, pptx.PresentationPart!.SlideParts.Count());
    }

    private static DocumentRequest BuildRequest(DocumentFormat format, string body, string fileName) => new()
    {
        Format = format,
        Body = body,
        FileName = fileName,
        Scope = new ArtifactScope
        {
            WorkspaceId = ArtifactScope.DefaultWorkspaceId,
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
