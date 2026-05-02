namespace Sovrant.Runtime.Config;

/// <summary>
/// Bootstrap-only configuration: the minimum set of paths and secrets that
/// must be resolved before the DI container is built. Loaded by
/// <see cref="BootstrapConfigLoader"/> with layered precedence
/// (CLI &gt; env vars &gt; project file &gt; user file &gt; defaults).
/// </summary>
/// <remarks>
/// Unlike <see cref="SovrantConfig"/>, which carries runtime preferences and is
/// safe to mutate per-session, <c>BootstrapConfig</c> is fixed for the process
/// lifetime — these paths and secrets bind to long-lived singletons
/// (storage provider, log sinks, credential store, auth middleware).
/// </remarks>
public sealed record BootstrapConfig
{
    /// <summary>Path to the SQLite database file. Default: <c>~/.sovrant/data/sovrant.db</c>.</summary>
    public string? DbPath { get; init; }

    /// <summary>
    /// Path to the rolling log file. The token <c>{Date}</c> is replaced with <c>yyyyMMdd</c>.
    /// Default: <c>~/.sovrant/logs/sovrant-{Date}.log</c>.
    /// </summary>
    public string? LogFile { get; init; }

    /// <summary>Root directory for artifact storage. Default: <c>~/.sovrant/artifacts</c>.</summary>
    public string? ArtifactsRoot { get; init; }

    /// <summary>
    /// Path to the AES-GCM master keystore file. Default:
    /// <c>~/.sovrant/credentials/.keystore</c>. The directory is also used
    /// to store per-credential <c>{sha256}.enc</c> blobs.
    /// </summary>
    public string? KeystorePath { get; init; }

    /// <summary>
    /// Static bearer token for server auth. When set, the API enforces it as
    /// the only accepted token. When absent, server requests are unauthenticated
    /// (suitable only for local development).
    /// </summary>
    public string? ServerToken { get; init; }
}
