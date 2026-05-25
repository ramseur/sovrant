using Npgsql;

namespace Sovrant.Storage.Postgres;

internal sealed class PostgresConnectionFactory(string connectionString) : IPostgresConnectionFactory
{
    public NpgsqlConnection CreateConnection()
    {
        var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Builds an Npgsql connection string from a Supabase project URL and service role key.
    /// Supabase exposes PostgreSQL on port 5432 at db.{project-ref}.supabase.co.
    /// </summary>
    public static string BuildConnectionString(string supabaseProjectUrl, string serviceRoleKey)
    {
        // Extract the project reference from https://{ref}.supabase.co
        var uri = new Uri(supabaseProjectUrl.TrimEnd('/'));
        var host = uri.Host; // e.g. "xyz.supabase.co"
        var dbHost = "db." + host; // e.g. "db.xyz.supabase.co"

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = dbHost,
            Port = 5432,
            Database = "postgres",
            Username = "postgres",
            Password = serviceRoleKey,
            SslMode = SslMode.Require,
        };
        return builder.ConnectionString;
    }
}
