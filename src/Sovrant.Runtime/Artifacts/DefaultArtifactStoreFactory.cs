using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Default <see cref="IArtifactStoreFactory"/> that supports the <c>local</c> backend.
/// Additional backends can be registered by replacing this factory in DI.
/// </summary>
public sealed class DefaultArtifactStoreFactory : IArtifactStoreFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly BootstrapConfig _bootstrap;

    public DefaultArtifactStoreFactory(ILoggerFactory loggerFactory, BootstrapConfig bootstrap)
    {
        _loggerFactory = loggerFactory;
        _bootstrap = bootstrap;
    }

    /// <inheritdoc/>
    public IArtifactStore Create(string backend)
    {
        if (string.Equals(backend, "local", StringComparison.OrdinalIgnoreCase))
            return new LocalArtifactStore(
                _loggerFactory.CreateLogger<LocalArtifactStore>(),
                root: _bootstrap.ArtifactsRoot);

        throw new InvalidOperationException(
            $"Unknown artifact backend: '{backend}'. Supported: local");
    }
}
