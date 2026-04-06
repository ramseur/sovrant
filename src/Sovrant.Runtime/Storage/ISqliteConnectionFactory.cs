using Microsoft.Data.Sqlite;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Internal factory for obtaining SQLite connections. Package-internal so that
/// <c>Sqlite*Store</c> implementations can get connections without leaking the
/// abstraction outside <c>Sovrant.Runtime</c>.
/// </summary>
internal interface ISqliteConnectionFactory
{
    /// <summary>Creates and opens a new <see cref="SqliteConnection"/>.</summary>
    SqliteConnection CreateConnection();
}
