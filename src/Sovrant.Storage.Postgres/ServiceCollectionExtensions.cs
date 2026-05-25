using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Storage.Postgres;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Postgres/Supabase storage backend.
    /// Replaces the SQLite <see cref="ISessionStore"/> and <see cref="ICredentialStore"/>
    /// with Npgsql-backed implementations.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">Npgsql connection string to the Supabase/PostgreSQL database.</param>
    /// <param name="keystorePath">
    /// Path to the AES-256-GCM master key file. Defaults to
    /// <c>~/.sovrant/credentials/.keystore</c> (same location as SQLite mode).
    /// </param>
    public static IServiceCollection AddSovrantPostgresStorage(
        this IServiceCollection services,
        string connectionString,
        string? keystorePath = null)
    {
        services.AddSingleton<IPostgresConnectionFactory>(
            _ => new PostgresConnectionFactory(connectionString));

        services.AddSingleton<ISchemaInitializer, PostgresSchemaInitializer>();

        services.AddSingleton<ISessionStore, PostgresSessionStore>();

        services.AddSingleton<ICredentialStore>(sp =>
            new PostgresCredentialStore(
                sp.GetRequiredService<IPostgresConnectionFactory>(),
                keystorePath));

        return services;
    }

    /// <summary>
    /// Builds an Npgsql connection string from Supabase project credentials.
    /// </summary>
    public static string BuildSupabaseConnectionString(string projectUrl, string serviceRoleKey)
        => PostgresConnectionFactory.BuildConnectionString(projectUrl, serviceRoleKey);

    /// <summary>
    /// Creates a standalone <see cref="IPostgresConnectionFactory"/> for ad-hoc use
    /// (e.g., admin UI before the DI container is reconfigured).
    /// </summary>
    public static IPostgresConnectionFactory CreateConnectionFactory(string connectionString)
        => new PostgresConnectionFactory(connectionString);

    /// <summary>
    /// Creates a standalone <see cref="ISchemaInitializer"/> for ad-hoc use.
    /// </summary>
    public static ISchemaInitializer CreateSchemaInitializer(string connectionString)
        => new PostgresSchemaInitializer(new PostgresConnectionFactory(connectionString));
}
