using Npgsql;

namespace Sovrant.Storage.Postgres;

/// <summary>
/// Creates Npgsql connections to the Supabase / PostgreSQL database.
/// </summary>
public interface IPostgresConnectionFactory
{
    NpgsqlConnection CreateConnection();
}
