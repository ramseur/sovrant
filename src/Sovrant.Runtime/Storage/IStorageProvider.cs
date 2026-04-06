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

    /// <summary>
    /// Executes a callback within a transaction. The transaction is committed
    /// if the callback completes; rolled back on exception.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
