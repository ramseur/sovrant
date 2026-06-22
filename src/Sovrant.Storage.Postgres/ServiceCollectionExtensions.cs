using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Storage.Postgres;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Postgres/Supabase storage backend.
    /// Replaces the SQLite session, credential, knowledge, and MCP trust stores
    /// with Npgsql-backed implementations.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">Npgsql connection string to the Supabase/PostgreSQL database.</param>
    /// <param name="keystorePath">
    /// Legacy path to an on-disk AES-256-GCM master key file used for one-time migration.
    /// Defaults to <c>~/.sovrant/credentials/.keystore</c>. The key is stored in the DB
    /// keystore table after first use and the file is no longer required.
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

        services.AddSingleton<IMcpTrustRuleStore, PostgresMcpTrustRuleStore>();

        services.AddSingleton<IKnowledgeStore, PostgresKnowledgeStore>();

        services.AddSingleton<IKnowledgeAttributionStore, PostgresKnowledgeAttributionStore>();

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

    /// <summary>
    /// Tests whether a connection can be opened using the given connection string.
    /// Returns true on success, false on any failure (does not throw).
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Connection probe must never throw — any failure (auth, network, bad host) maps to false so the UI can surface a friendly error.")]
    public static async Task<bool> CanConnectAsync(string connectionString, CancellationToken ct = default)
    {
        try
        {
            using var conn = new Npgsql.NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Initializes the base Sovrant schema only, excluding the Supabase Auth Extension.
    /// Use for standalone PostgreSQL deployments.
    /// </summary>
    public static async Task InitializeBaseSchemaAsync(string connectionString, CancellationToken ct = default)
    {
        var initializer = new PostgresSchemaInitializer(new PostgresConnectionFactory(connectionString));
        await initializer.InitializeBaseOnlyAsync(ct).ConfigureAwait(false);
    }
}
