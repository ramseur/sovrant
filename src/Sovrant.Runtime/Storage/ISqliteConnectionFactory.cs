using Microsoft.Data.Sqlite;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Internal factory for obtaining SQLite connections. Package-internal so that
/// <c>Sqlite*Store</c> implementations can get connections without leaking the
/// abstraction outside <c>Sovrant.Runtime</c>.
/// </summary>
internal interface ISqliteConnectionFactory
{
    /// <summary>Creates and opens a new read-write <see cref="SqliteConnection"/>.</summary>
    SqliteConnection CreateConnection();

    /// <summary>
    /// Phase 38 — creates and opens a read-only <see cref="SqliteConnection"/>.
    /// The connection uses <c>Mode=ReadOnly</c>, which causes SQLite to reject
    /// any statement that attempts to mutate the database. Use this for query
    /// endpoints (sessions list, usage, audit reads) so a SQL-injection bug
    /// in a read path cannot escalate to data corruption.
    /// </summary>
    SqliteConnection CreateReadOnlyConnection();
}
