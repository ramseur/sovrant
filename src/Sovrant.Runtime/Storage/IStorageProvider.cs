namespace Sovrant.Runtime.Storage;

/// <summary>
/// Backend-agnostic storage provider that manages the database lifecycle.
/// Consumers never touch the raw connection — domain-specific stores
/// (<see cref="Session.ISessionStore"/>, <see cref="Memory.IMemoryStore"/>, etc.)
/// receive an internal connection factory via DI.
/// </summary>
public interface IStorageProvider : IAsyncDisposable
{
    /// <summary>Initializes the database, runs pending migrations, and sets PRAGMAs.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Current schema version after initialization.</summary>
    int SchemaVersion { get; }

    /// <summary>On-disk path of the database file (or null for in-memory / unknown backends).</summary>
    string? DatabasePath { get; }

    /// <summary>
    /// Executes a callback within a transaction. The transaction is committed
    /// if the callback completes; rolled back on exception.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);

    /// <summary>
    /// Phase 42.5 — lightweight liveness probe for /health. Performs a cheap
    /// read query against the DB and returns <c>ok=true</c> if it succeeds,
    /// or <c>ok=false</c> with an error message if the DB is unreachable.
    /// Must be safe to call on an unauthenticated code path — no PII.
    /// </summary>
    StorageHealth CheckHealth();
}

/// <summary>Result of a lightweight DB liveness probe.</summary>
public readonly record struct StorageHealth(bool Ok, int SchemaVersion, string? Path, string? Error);
