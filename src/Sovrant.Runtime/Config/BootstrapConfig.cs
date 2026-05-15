namespace Sovrant.Runtime.Config;

/// <summary>
/// Bootstrap-only configuration: the minimum set of paths and secrets that
/// must be resolved before the DI container is built. Loaded by
/// <see cref="BootstrapConfigLoader"/> from environment variables and an
/// optional <c>.env</c> file in the current working directory.
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

    // ── TLS ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Path to a PFX certificate file (or PEM certificate when <see cref="TlsKeyPath"/> is also set).
    /// When set, Kestrel binds an additional HTTPS listener on <see cref="TlsHttpsPort"/>.
    /// TLS is disabled when this is null or empty.
    /// </summary>
    public string? TlsCertPath { get; init; }

    /// <summary>
    /// Passphrase for a PFX certificate file. Ignored when using PEM+key.
    /// </summary>
    public string? TlsCertPassword { get; init; }

    /// <summary>
    /// Path to the PEM private-key file when using separate cert/key PEM files
    /// instead of a combined PFX. Leave null when <see cref="TlsCertPath"/> is a PFX.
    /// </summary>
    public string? TlsKeyPath { get; init; }

    /// <summary>
    /// HTTPS port for the Kestrel listener. Defaults to 5443 (Server) or 5101 (Web)
    /// when null. Ignored when <see cref="TlsCertPath"/> is not set.
    /// </summary>
    public string? TlsHttpsPort { get; init; }

    /// <summary>Returns true when a certificate path is configured and the file exists.</summary>
    public bool HasTls =>
        !string.IsNullOrEmpty(TlsCertPath) && File.Exists(TlsCertPath);

    /// <summary>
    /// Returns the configured HTTPS port, or <paramref name="defaultPort"/> when
    /// <see cref="TlsHttpsPort"/> is null or unparseable.
    /// </summary>
    public int GetHttpsPort(int defaultPort) =>
        int.TryParse(TlsHttpsPort, out var port) && port > 0 ? port : defaultPort;
}
