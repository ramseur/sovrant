using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api;
using Sovrant.Runtime.Caching;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Hooks;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Evals;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Tools;
using Sovrant.Runtime.Projects;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

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

        // Storage provider (Phase 32) — SQLite by default.
        services.AddSingleton<SqliteStorageProvider>();
        services.AddSingleton<IStorageProvider>(sp => sp.GetRequiredService<SqliteStorageProvider>());
        services.AddSingleton<ISqliteConnectionFactory>(sp => sp.GetRequiredService<SqliteStorageProvider>());

        // Caching infrastructure (Phase 31) — in-memory by default.
        services.AddSingleton<ICacheProvider, InMemoryCacheProvider>();
        services.AddSingleton<CacheInvalidator>();

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

        // Audit store — SQLite primary, optional JSONL dual-write.
        services.AddSingleton<IAuditStore>(sp =>
        {
            var factory = sp.GetRequiredService<ISqliteConnectionFactory>();
            IAuditStore primary = new SqliteAuditStore(factory);

            if (string.Equals(
                    Environment.GetEnvironmentVariable("SOVRANT_AUDIT_JSONL"),
                    "true", StringComparison.OrdinalIgnoreCase))
            {
                return new DualWriteAuditStore(primary, new AuditLogger());
            }

            return primary;
        });

        // Governance monitor — loads governance.json from disk.
        services.AddSingleton<GovernanceConfig>(_ => GovernanceConfig.Load());
        services.AddSingleton<IGovernanceMonitor, GovernanceMonitor>();

        // Tool registry and executor
        services.AddSingleton<IToolRegistry, InMemoryToolRegistry>();
        services.AddSingleton<IToolConfirmationHandler, DenyAllConfirmationHandler>();
        services.AddSingleton<IToolExecutor, DefaultToolExecutor>();

        // Session store — SQLite primary, optional JSONL dual-write.
        services.AddSingleton<ISessionStore>(sp =>
        {
            var factory = sp.GetRequiredService<ISqliteConnectionFactory>();
            ISessionStore primary = new SqliteSessionStore(factory);

            if (string.Equals(
                    Environment.GetEnvironmentVariable("SOVRANT_SESSION_JSONL"),
                    "true", StringComparison.OrdinalIgnoreCase))
            {
                var jsonlLogger = sp.GetRequiredService<ILogger<JsonlSessionStore>>();
                return new DualWriteSessionStore(primary, new JsonlSessionStore(jsonlLogger));
            }

            return primary;
        });

        // Token usage tracking
        services.AddSingleton<ITokenUsageStore>(sp =>
            new SqliteTokenUsageStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Memory system (Phase 25) — SQLite-backed.
        services.AddSingleton<IMemoryStore>(sp =>
            new SqliteMemoryStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
        services.AddSingleton<MemoryInjector>();
        services.AddSingleton<SessionEndMemoryHandler>();

        // Workspace service (Phase 35)
        services.AddSingleton<IWorkspaceService>(sp =>
            new SqliteWorkspaceStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Project service (Phase 36)
        services.AddSingleton<IProjectService>(sp =>
            new SqliteProjectStore(
                sp.GetRequiredService<ISqliteConnectionFactory>(),
                sp.GetRequiredService<IWorkspaceService>()));

        // User service (Phase 37)
        services.AddSingleton<IUserService>(sp =>
            new SqliteUserStore(
                sp.GetRequiredService<ISqliteConnectionFactory>(),
                sp.GetRequiredService<ILogger<SqliteUserStore>>()));

        // Token service (Phase 38) — per-user API tokens.
        services.AddSingleton<Sovrant.Runtime.Auth.ITokenService>(sp =>
            new Sovrant.Runtime.Auth.SqliteTokenService(
                sp.GetRequiredService<ISqliteConnectionFactory>(),
                sp.GetRequiredService<ILogger<Sovrant.Runtime.Auth.SqliteTokenService>>()));

        // Swarm event store (Phase 37.5) — SQLite-backed, replaces the JSONL session store.
        services.AddSingleton<ISwarmEventStore>(sp =>
            new SqliteSwarmEventStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Runtime trace store (Phase 51) — append-only structured reasoning trace
        // for the engine's planner/executor split. Crash-safe: every executor
        // state transition writes here before the side effect runs.
        services.AddSingleton<IRuntimeTraceStore>(sp =>
            new SqliteRuntimeTraceStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Mission scratchpad (Phase 51) — typed, append-only shared store for
        // parallel sub-agents within one mission to publish intermediate
        // findings the next plan wave can read.
        services.AddSingleton<IMissionScratchpadStore>(sp =>
            new SqliteMissionScratchpadStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Context compactor (Phase 51) — folds older step outcomes into a
        // summary when the run history won't fit in the planner budget.
        // Default is the naive deterministic impl; production can swap in
        // an LLM-backed summariser without touching executor or planner.
        services.AddSingleton<Engine.IContextCompactor, Engine.NaiveContextCompactor>();

        // Engine recovery (Phase 51) — closes out in-flight runs from the
        // previous process at startup so the trace log stays internally
        // consistent after a crash.
        services.AddSingleton<Engine.IEngineRecovery, Engine.EngineRecovery>();

        // Production step runner (Phase 51 step I) — bridges the executor
        // to the existing agentic loop via IRuntimeSessionPool. Tests
        // substitute a fake IStepRunner; the default composition uses
        // this one.
        services.AddSingleton<Engine.IStepRunner, Engine.LlmStepRunner>();

        // Default LlmExecutor wired to the production step runner. Tests
        // that need a bespoke step runner build their own executor.
        services.AddSingleton<Engine.IExecutor, Engine.LlmExecutor>();

        // Eval framework (Phase 27)
        services.AddSingleton<IEvalResultStore, EvalResultStore>();
        services.AddSingleton<IEvalRunner, EvalRunner>();

        // MCP
        services.AddSingleton<IMcpClientFactory, SovrantMcpClientFactory>();
        services.AddSingleton<McpClientRegistry>();
        services.AddSingleton<McpToolRegistrar>();
        services.AddSingleton<ICredentialStore>(sp =>
            new SqliteCredentialStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
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

        // Initialize SQLite storage (runs migrations).
        var storage = services.GetRequiredService<IStorageProvider>();
        await storage.InitializeAsync(ct).ConfigureAwait(false);

        var config = services.GetRequiredService<SovrantConfig>();
        if (config.McpServers.Count == 0)
            return;

        var registrar = services.GetRequiredService<McpToolRegistrar>();
        await registrar.RegisterAllAsync(config.McpServers, ct).ConfigureAwait(false);
    }
}
