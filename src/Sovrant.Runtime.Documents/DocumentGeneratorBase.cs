using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Documents;

/// <summary>
/// Shared plumbing for generators: creates the run scope, writes the
/// produced bytes through <see cref="IArtifactStore"/>, and packages the
/// <see cref="DocumentResult"/>. Generators only need to produce the raw
/// output bytes in <see cref="RenderAsync"/>.
/// </summary>
public abstract class DocumentGeneratorBase : IDocumentGenerator
{
    private static readonly TimeSpan s_defaultAccessUrlTtl = TimeSpan.FromMinutes(15);

    private readonly IArtifactStore _artifactStore;

    protected DocumentGeneratorBase(IArtifactStore artifactStore)
    {
        _artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
    }

    public abstract DocumentFormat Format { get; }

    protected abstract string ContentType { get; }

    /// <summary>
    /// Produces the raw bytes for the document. Implementations do not
    /// touch the artifact store — <see cref="GenerateAsync"/> handles that.
    /// </summary>
    protected abstract Task<byte[]> RenderAsync(DocumentRequest request, CancellationToken ct);

    public async Task<DocumentResult> GenerateAsync(DocumentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("FileName is required.", nameof(request));
        if (request.Body is null)
            throw new ArgumentException("Body is required.", nameof(request));

        var bytes = await RenderAsync(request, ct).ConfigureAwait(false);

        var handle = await _artifactStore.CreateRunScopeAsync(request.Scope, ct).ConfigureAwait(false);
        using (var ms = new MemoryStream(bytes))
            await _artifactStore.WriteAsync(handle, request.FileName, ms, ContentType, ct).ConfigureAwait(false);

        var url = await _artifactStore
            .GetAccessUrlAsync(handle, request.FileName, s_defaultAccessUrlTtl, ct)
            .ConfigureAwait(false);

        return new DocumentResult
        {
            Handle = handle,
            RelativePath = request.FileName,
            ContentType = ContentType,
            SizeBytes = bytes.LongLength,
            AccessUrl = url,
        };
    }
}
