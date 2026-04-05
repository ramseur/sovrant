using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Hooks;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime;

/// <summary>Extension methods for registering Sovrant runtime services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Sovrant runtime services using the provided configuration.
    /// Also calls <see cref="Sovrant.Api.ServiceCollectionExtensions.AddLlmProviders"/> to register
    /// the API layer.
    /// </summary>
    public static IServiceCollection AddSovrantRuntime(
        this IServiceCollection services,
        SovrantConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        config ??= ConfigLoader.Load();

        // Register config as singleton
        services.AddSingleton(config);

        // Register API layer (providers + router) using the built configuration
        var apiConfig = ConfigLoader.BuildConfiguration();
        services.AddLlmProviders(apiConfig);

        // Permission policy — mutable so EnterPlanMode/ExitPlanMode tools can toggle it at runtime.
        // The server overrides both IPermissionPolicy and IPermissionModeAccessor with its own
        // MutableServerConfig-backed implementations.
        var cliPolicy = new MutableCliPermissionPolicy(config.PermissionMode);
        services.AddSingleton<IPermissionPolicy>(cliPolicy);
        services.AddSingleton<IPermissionModeAccessor>(cliPolicy);

        // Hook runner — loads hooks.json from disk on first construction.
        services.AddSingleton<IHookRunner, HookRunner>();

        // Tool registry and executor
        services.AddSingleton<IToolRegistry, InMemoryToolRegistry>();
        services.AddSingleton<IToolExecutor, DefaultToolExecutor>();

        // Session store
        services.AddSingleton<ISessionStore, JsonlSessionStore>();

        // MCP
        services.AddSingleton<IMcpClientFactory, SovrantMcpClientFactory>();
        services.AddSingleton<McpClientRegistry>();
        services.AddSingleton<McpToolRegistrar>();
        services.AddSingleton<ICredentialStore, AesGcmCredentialStore>();
        services.AddSingleton<McpOAuthService>();

        // Conversation runtime — transient so the pool creates independent instances per session.
        services.AddTransient<IConversationRuntime, ConversationRuntime>();

        // Session pool — singleton that keeps one runtime alive per session ID.
        services.AddSingleton<IRuntimeSessionPool, RuntimeSessionPool>();

        return services;
    }

    /// <summary>
    /// Initializes the runtime after the service provider is built:
    /// connects to MCP servers and seeds their tools into the registry.
    /// </summary>
    public static async Task InitializeRuntimeAsync(
        this IServiceProvider services,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = services.GetRequiredService<SovrantConfig>();
        if (config.McpServers.Count == 0)
            return;

        var registrar = services.GetRequiredService<McpToolRegistrar>();
        await registrar.RegisterAllAsync(config.McpServers, ct).ConfigureAwait(false);
    }
}
