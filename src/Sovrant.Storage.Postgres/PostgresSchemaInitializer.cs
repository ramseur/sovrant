using System.Reflection;
using Npgsql;
using Sovrant.Runtime.Storage;

namespace Sovrant.Storage.Postgres;

/// <summary>
/// Reads the embedded PostgresSchema.sql and executes it against the target database.
/// </summary>
public sealed class PostgresSchemaInitializer(IPostgresConnectionFactory factory) : ISchemaInitializer
{
    private static readonly string _schemaSql = LoadEmbeddedSql();

    public async Task<int?> GetSchemaVersionAsync(CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT version FROM sovrant_schema_version WHERE id = 1
            """;
        try
        {
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is int v ? v : result is long l ? (int)l : null;
        }
        catch (NpgsqlException)
        {
            // Table doesn't exist yet — schema not initialized.
            return null;
        }
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = _schemaSql;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static string LoadEmbeddedSql()
    {
        // The SQL is embedded from Sovrant.Runtime, not this assembly.
        var runtimeAssembly = typeof(ISchemaInitializer).Assembly;
        var resourceName = runtimeAssembly.GetManifestResourceNames()
            .First(n => n.EndsWith("PostgresSchema.sql", StringComparison.OrdinalIgnoreCase));

        using var stream = runtimeAssembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
