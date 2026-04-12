using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Caching;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Hooks;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Evals;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Missions;
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
        // Phase 42.5 — SovrantConfig.DbPath (from --db-path CLI flag) takes priority
        // over the SOVRANT_DB_PATH env var and the default ~/.sovrant/data/sovrant.db.
        services.AddSingleton(sp => new SqliteStorageProvider(
            sp.GetRequiredService<ILogger<SqliteStorageProvider>>(), config.DbPath));
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
            new SqliteAuditStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Governance monitor — loads governance.json from disk.
        services.AddSingleton<GovernanceConfig>(_ => GovernanceConfig.Load());
        services.AddSingleton<IGovernanceMonitor, GovernanceMonitor>();

        // Phase 59 — Agentic loop hardening: intent gate, plan approval,
        // execution governance, orchestration router, progress tracking.
        services.AddSingleton<Governance.IIntentGate, Governance.SemanticIntentGate>();
        services.AddSingleton<Governance.IPlanPresenter, Governance.PlanPresenter>();
        services.AddSingleton<Governance.PlanApprovalGate>();
        services.AddSingleton<Governance.StepToolEnforcer>();
        services.AddSingleton<Governance.IntentInjector>();
        services.AddSingleton<Governance.PlanProgressTracker>();
        services.AddSingleton<Governance.IOrchestrationRouter, Governance.HeuristicOrchestrationRouter>();

        // Phase 58 — Trust Boundary: sanitization, ethical harness, intent verification.
        services.AddSingleton<TrustBoundary.EthicalAuditLog>();
        services.AddSingleton<TrustBoundary.IEthicalHarness>(sp =>
        {
            var tbConfig = config.TrustBoundary;
            return new TrustBoundary.ContentPolicyEngine(
                tbConfig.EthicalHarness,
                sp.GetRequiredService<TrustBoundary.EthicalAuditLog>());
        });
        services.AddSingleton<TrustBoundary.IPromptSanitizer>(sp =>
        {
            var tbConfig = config.TrustBoundary;
            var corpConfig = new TrustBoundary.CorporateDataConfig
            {
                CorporateDomains = tbConfig.Sanitizer.CorporateDomains,
                AllowList = tbConfig.Sanitizer.AllowList,
            };
            IEnumerable<TrustBoundary.IPatternDetector> detectors =
            [
                new TrustBoundary.PiiDetector(),
                new TrustBoundary.CorporateDataDetector(corpConfig),
                new TrustBoundary.CustomPatternRegistry(tbConfig.Sanitizer.CustomPatterns),
            ];
            return new TrustBoundary.PromptSanitizer(detectors);
        });
        services.AddSingleton<TrustBoundary.IntentVerificationBridge>(sp =>
            new TrustBoundary.IntentVerificationBridge(
                sp.GetService<Governance.IIntentGate>(),
                sp.GetService<TrustBoundary.IEthicalHarness>()));

        // Tool registry and executor
        services.AddSingleton<IToolRegistry, InMemoryToolRegistry>();
        services.AddSingleton<IToolConfirmationHandler, DenyAllConfirmationHandler>();
        services.AddSingleton<IToolExecutor, DefaultToolExecutor>();

        // Session store — SQLite primary, optional JSONL dual-write.
        services.AddSingleton<ISessionStore>(sp =>
            new SqliteSessionStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

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

        // Mission layer (Phase 51) — long-lived goals sitting on top of the
        // engine layer with acceptance gates and an append-only event
        // journal. The store owns V011 tables; the planner/executor/gate
        // are deliberately swap-in seams so production can later plug in
        // an LLM-backed planner without touching routes or storage.
        services.AddSingleton<IMissionStore>(sp =>
            new SqliteMissionStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
        services.AddSingleton<IMissionPlanner, SimpleMissionPlanner>();
        services.AddSingleton<IAcceptanceGate, AllStepsSucceededGate>();
        services.AddSingleton<IMissionExecutor, LlmMissionExecutor>();
        services.AddSingleton<MissionExportService>();

        // Agent run store (Phase 52) — unified ledger tracking delegations,
        // swarm tasks, and mission steps in one table.
        services.AddSingleton<IAgentRunStore>(sp =>
            new SqliteAgentRunStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Artifact store (Phase 53) — tenant-scoped artifact storage.
        // Backend is selected via SOVRANT_ARTIFACTS_BACKEND (default: local).
        services.AddSingleton<IArtifactStore>(sp =>
        {
            var backend = Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_BACKEND") ?? "local";
            return string.Equals(backend, "local", StringComparison.OrdinalIgnoreCase)
                ? new LocalArtifactStore(sp.GetRequiredService<ILogger<LocalArtifactStore>>())
                : throw new InvalidOperationException(
                    $"Unknown artifact backend: '{backend}'. Supported: local");
        });
        services.AddSingleton<LegacyArtifactImporter>();

        // Eval framework (Phase 27) — SQLite-backed since Phase 49
        services.AddSingleton<IEvalResultStore>(sp =>
            new SqliteEvalResultStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
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

        // Load model capability overrides (Phase 54) — bundled + user + env.
        // Must run before live fetch so overrides take priority.
        var overrideLoader = services.GetRequiredService<Sovrant.Api.Capabilities.ModelOverrideLoader>();
        overrideLoader.LoadAll();

        // Fetch live model metadata from OpenRouter (Phase 54) — best effort,
        // registered as Live source (lowest priority after bundled/user overrides).
        var metadataFetcher = services.GetRequiredService<Sovrant.Api.Capabilities.LiveModelMetadataFetcher>();
        await metadataFetcher.FetchAsync(ct).ConfigureAwait(false);

        // Rebuild tier assignments now that live metadata is available (Phase 48).
        var tierResolver = services.GetService<Sovrant.Api.Routing.IModelTierResolver>();
        tierResolver?.Rebuild();

        // Run one-shot legacy artifact migration (Phase 53).
        var importer = services.GetRequiredService<LegacyArtifactImporter>();
        await importer.ImportIfNeededAsync(ct).ConfigureAwait(false);

        var config = services.GetRequiredService<SovrantConfig>();
        if (config.McpServers.Count == 0)
            return;

        var registrar = services.GetRequiredService<McpToolRegistrar>();
        await registrar.RegisterAllAsync(config.McpServers, ct).ConfigureAwait(false);
    }
}
