namespace Sovrant.Runtime.Storage;

/// <summary>
/// Initializes the Sovrant schema on a target database backend.
/// </summary>
public interface ISchemaInitializer
{
    /// <summary>
    /// Returns the current schema version recorded in the target database,
    /// or null if the schema has not been initialized.
    /// </summary>
    Task<int?> GetSchemaVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates all Sovrant tables and indexes in the target database.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);
}
