using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Default <see cref="IArtifactStoreFactory"/> that supports the <c>local</c> backend.
/// Additional backends can be registered by replacing this factory in DI.
/// </summary>
public sealed class DefaultArtifactStoreFactory : IArtifactStoreFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public DefaultArtifactStoreFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public IArtifactStore Create(string backend)
    {
        if (string.Equals(backend, "local", StringComparison.OrdinalIgnoreCase))
            return new LocalArtifactStore(_loggerFactory.CreateLogger<LocalArtifactStore>());

        throw new InvalidOperationException(
            $"Unknown artifact backend: '{backend}'. Supported: local");
    }
}
