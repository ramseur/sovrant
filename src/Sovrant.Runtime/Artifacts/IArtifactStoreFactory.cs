namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Creates <see cref="IArtifactStore"/> instances based on a backend identifier.
/// Allows new backends (S3, Azure Blob, R2) to be plugged in without modifying DI registration.
/// </summary>
public interface IArtifactStoreFactory
{
    /// <summary>Creates an artifact store for the given backend name (e.g. "local", "s3").</summary>
    IArtifactStore Create(string backend);
}
