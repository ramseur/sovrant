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
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
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
        SovrantConfig? config = null,
        BootstrapConfig? bootstrap = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        config ??= ConfigLoader.Load();
        bootstrap ??= BootstrapConfigLoader.Load();

        // Register config as singleton
        services.AddSingleton(config);
        services.AddSingleton(bootstrap);

        // Hot-reload registry (Bucket-B step 4) — every LiveSettings<T>
        // self-registers with this so the Settings UI can fan out reloads.
        services.AddSingleton<Workspaces.LiveSettingsRegistry>();

        // Storage provider (Phase 32) — SQLite by default.
        // The DB path comes from BootstrapConfig (CLI > env > sovrant.config > default).
        services.AddSingleton(sp => new SqliteStorageProvider(
            sp.GetRequiredService<ILogger<SqliteStorageProvider>>(), bootstrap.DbPath));
        services.AddSingleton<IStorageProvider>(sp => sp.GetRequiredService<SqliteStorageProvider>());
        services.AddSingleton<ISqliteConnectionFactory>(sp => sp.GetRequiredService<SqliteStorageProvider>());

        // Caching infrastructure (Phase 31) — in-memory by default.
        services.AddSingleton<ICacheProvider, InMemoryCacheProvider>();
        services.AddSingleton<CacheInvalidator>();

        // Register API layer (providers + router) using the built configuration
        var apiConfig = ConfigLoader.BuildConfiguration();
        var credentials = Sovrant.Api.Config.CredentialConfig.Resolve(apiConfig);
        services.AddSingleton(credentials);
        services.AddLlmProviders(apiConfig, credentials);

        // Bucket-C: replace the static IAuthProvider that AddLlmProviders registered
        // with one that prefers the encrypted credential store when no env var is set.
        // Web/Desktop further override this with MutableAuthProvider for live edits;
        // the latest registration always wins so their UI hot-swap path is unaffected.
        services.AddSingleton<Sovrant.Api.Auth.IAuthProvider>(sp =>
            new Mcp.CredentialStoreAuthProvider(
                sp.GetRequiredService<Mcp.ICredentialStore>(),
                Sovrant.Api.Auth.CredentialKeys.LlmApiKey,
                fallback: credentials.LlmApiKey));

        // Override the bootstrap IApiKeyResolver registered by AddLlmProviders with the
        // store-aware version now that ICredentialStore is available. Used by
        // LiveModelMetadataFetcher (OpenRouter key) and ProviderApiProvider's auth header.
        services.AddSingleton<Sovrant.Api.Auth.IApiKeyResolver, Mcp.CredentialStoreApiKeyResolver>();

        // Permission policy — mutable so EnterPlanMode/ExitPlanMode tools can toggle it at runtime.
        // The server overrides both IPermissionPolicy and IPermissionModeAccessor with its own
        // MutableServerConfig-backed implementations.
        var cliPolicy = new MutableCliPermissionPolicy(config.PermissionMode);
        services.AddSingleton<IPermissionPolicy>(cliPolicy);
        services.AddSingleton<IPermissionModeAccessor>(cliPolicy);

        // Hook runner — loads enabled hooks from IHookStore at construction.
        services.AddSingleton<IHookStore>(sp =>
            new SqliteHookStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
        services.AddSingleton<IHookRunner, HookRunner>();

        // Audit store — SQLite primary, optional JSONL dual-write.
        services.AddSingleton<IAuditStore>(sp =>
            new SqliteAuditStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Governance monitor — loads from env > workspace_settings DB > defaults.
        // Wrapped in ILiveSettings so the Settings UI can hot-reload secret
        // patterns / blocked commands / protected files without a restart.
        services.AddSingleton<Workspaces.LiveSettings<GovernanceConfig>>(sp =>
        {
            var live = new Workspaces.LiveSettings<GovernanceConfig>(
                () => GovernanceConfig.Load(
                    sp.GetService<Workspaces.IWorkspaceSettingsStore>()));
            sp.GetRequiredService<Workspaces.LiveSettingsRegistry>().Register(live);
            return live;
        });
        services.AddSingleton<Workspaces.ILiveSettings<GovernanceConfig>>(
            sp => sp.GetRequiredService<Workspaces.LiveSettings<GovernanceConfig>>());
        services.AddSingleton(sp => sp.GetRequiredService<Workspaces.ILiveSettings<GovernanceConfig>>().Current);
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
        // Hot-reloadable since Bucket-B step 4: the live wrapper subscribes the
        // sanitizer / ethical harness so they atomically swap their detector list
        // and compiled custom-blocked regexes when the Settings UI saves changes.
        services.AddSingleton<Workspaces.LiveSettings<TrustBoundary.TrustBoundaryConfig>>(sp =>
        {
            var live = new Workspaces.LiveSettings<TrustBoundary.TrustBoundaryConfig>(
                () => TrustBoundary.TrustBoundaryConfig.Resolve(
                    config.TrustBoundary,
                    sp.GetService<Workspaces.IWorkspaceSettingsStore>()));
            sp.GetRequiredService<Workspaces.LiveSettingsRegistry>().Register(live);
            return live;
        });
        services.AddSingleton<Workspaces.ILiveSettings<TrustBoundary.TrustBoundaryConfig>>(
            sp => sp.GetRequiredService<Workspaces.LiveSettings<TrustBoundary.TrustBoundaryConfig>>());
        services.AddSingleton(sp =>
            sp.GetRequiredService<Workspaces.ILiveSettings<TrustBoundary.TrustBoundaryConfig>>().Current);
        services.AddSingleton<TrustBoundary.EthicalAuditLog>();
        services.AddSingleton<TrustBoundary.IEthicalHarness>(sp =>
            new TrustBoundary.ContentPolicyEngine(
                sp.GetRequiredService<Workspaces.ILiveSettings<TrustBoundary.TrustBoundaryConfig>>(),
                sp.GetRequiredService<TrustBoundary.EthicalAuditLog>()));
        services.AddSingleton<TrustBoundary.IPromptSanitizer>(sp =>
            new TrustBoundary.PromptSanitizer(
                sp.GetRequiredService<Workspaces.ILiveSettings<TrustBoundary.TrustBoundaryConfig>>()));
        services.AddSingleton<TrustBoundary.IntentVerificationBridge>(sp =>
            new TrustBoundary.IntentVerificationBridge(
                sp.GetService<Governance.IIntentGate>(),
                sp.GetService<TrustBoundary.IEthicalHarness>()));

        // Tool registry and executor
        services.AddSingleton<IToolRegistry, InMemoryToolRegistry>();
        services.AddSingleton<IToolConfirmationHandler, DenyAllConfirmationHandler>();
        // Phase 87 Track E — per-turn approval cache for "always allow this turn".
        services.AddSingleton<IPerTurnApprovalCache, PerTurnApprovalCache>();
        services.AddSingleton<IToolExecutor, DefaultToolExecutor>();

        // Session store — SQLite primary, optional JSONL dual-write.
        services.AddSingleton<ISessionStore>(sp =>
            new SqliteSessionStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Token usage tracking
        services.AddSingleton<ITokenUsageStore>(sp =>
            new SqliteTokenUsageStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Memory system (Phase 25 + Phase 81) — SQLite-backed multi-layered memory plus
        // user-saved workspace_memory rows merged at injection time.
        services.AddSingleton<IMemoryStore>(sp =>
            new SqliteMemoryStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
        services.AddSingleton<MemoryInjector>(sp =>
            new MemoryInjector(
                sp.GetRequiredService<IMemoryStore>(),
                sp.GetRequiredService<ILogger<MemoryInjector>>(),
                sp.GetService<IWorkspaceService>()));
        services.AddSingleton<SessionEndMemoryHandler>();

        // Workspace service (Phase 35)
        services.AddSingleton<IWorkspaceService>(sp =>
            new SqliteWorkspaceStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Workspace settings store — budgets, session caps, and other
        // runtime-mutable knobs that previously lived in env vars only.
        services.AddSingleton<IWorkspaceSettingsStore>(sp =>
            new SqliteWorkspaceSettingsStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Phase 88-A — per-user preference store. Replaces the user-facing
        // fields previously written to ~/.sovrant/settings.json. API keys
        // never land here; only references (provider profile id) do.
        services.AddSingleton<IUserPreferenceStore>(sp =>
            new SqliteUserPreferenceStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Phase 88-B — provider profile store. Replaces ~/.sovrant/providers.json.
        // Each row holds non-secret metadata (name, base url, default model,
        // max tokens) plus a credential_id reference whose plaintext key
        // lives only in the encrypted ICredentialStore.
        services.AddSingleton<IProviderProfileStore>(sp =>
            new SqliteProviderProfileStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Phase 88-F — legacy config migrator. Runs once on startup; ingests
        // ~/.sovrant/{settings,providers,governance}.json into the DB +
        // credential store and renames the originals to *.json.bak. Idempotent
        // (a missing source file is a no-op).
        services.AddSingleton<LegacyConfigMigrator>();

        // Phase 90 / Phase 89 MVP — Command Center aggregator. Read-only
        // flattener over missions, agent_runs, and sessions; powers the
        // /command cockpit page (Web + Desktop) and GET /v1/command-center/state.
        services.AddSingleton<Sovrant.Runtime.CommandCenter.CommandCenterAggregator>();

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

        // Phase 85 — password hasher + identity service
        services.AddSingleton<Sovrant.Runtime.Auth.IPasswordHasher,
            Sovrant.Runtime.Auth.Argon2idPasswordHasher>();
        services.AddSingleton<Sovrant.Runtime.Auth.IIdentityService>(sp =>
            new Sovrant.Runtime.Auth.SqliteIdentityService(
                sp.GetRequiredService<IUserService>(),
                sp.GetRequiredService<Sovrant.Runtime.Auth.ITokenService>(),
                sp.GetRequiredService<Sovrant.Runtime.Auth.IPasswordHasher>(),
                sp.GetRequiredService<ISqliteConnectionFactory>(),
                sp.GetRequiredService<ILogger<Sovrant.Runtime.Auth.SqliteIdentityService>>()));

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

        // Executor tuning (Bucket-B) — env > workspace_settings > defaults
        // for re-plan / retry caps, wrapped in ILiveSettings so the Settings
        // UI can hot-reload without a process restart.
        services.AddSingleton<Workspaces.LiveSettings<Engine.ExecutorOptions>>(sp =>
        {
            var live = new Workspaces.LiveSettings<Engine.ExecutorOptions>(
                () => Engine.ExecutorOptions.Resolve(sp.GetService<Workspaces.IWorkspaceSettingsStore>()));
            sp.GetRequiredService<Workspaces.LiveSettingsRegistry>().Register(live);
            return live;
        });
        services.AddSingleton<Workspaces.ILiveSettings<Engine.ExecutorOptions>>(
            sp => sp.GetRequiredService<Workspaces.LiveSettings<Engine.ExecutorOptions>>());
        services.AddSingleton(sp => sp.GetRequiredService<Workspaces.ILiveSettings<Engine.ExecutorOptions>>().Current);

        // Compaction threshold (Bucket-B) — single-scalar live wrapper so the
        // Settings UI can adjust SOVRANT_COMPACT_THRESHOLD without restart.
        services.AddSingleton<Workspaces.LiveSettings<Conversation.CompactionSettings>>(sp =>
        {
            var live = new Workspaces.LiveSettings<Conversation.CompactionSettings>(
                () => Conversation.CompactionSettings.Resolve(
                    sp.GetService<Workspaces.IWorkspaceSettingsStore>(),
                    fallback: config.CompactThreshold));
            sp.GetRequiredService<Workspaces.LiveSettingsRegistry>().Register(live);
            return live;
        });
        services.AddSingleton<Workspaces.ILiveSettings<Conversation.CompactionSettings>>(
            sp => sp.GetRequiredService<Workspaces.LiveSettings<Conversation.CompactionSettings>>());

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

        // Autonomous-driver layer (Phase 67) — named strategies for advancing
        // a mission forward. The LLM driver wraps IMissionExecutor; additional
        // drivers (swarm, external orchestrator) register alongside it and are
        // resolved by name through DriverRegistry.
        services.AddSingleton<IAutonomousDriver, LlmAutonomousDriver>();
        services.AddSingleton<DriverRegistry>();

        // Agent run store (Phase 52) — unified ledger tracking delegations,
        // swarm tasks, and mission steps in one table.
        services.AddSingleton<IAgentRunStore>(sp =>
            new SqliteAgentRunStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Coordination event store and group PM store (Phase 57)
        services.AddSingleton<ICoordinationEventStore>(sp =>
            new SqliteCoordinationEventStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
        services.AddSingleton<IGroupPMStore>(sp =>
            new SqliteGroupPMStore(sp.GetRequiredService<ISqliteConnectionFactory>()));

        // Artifact store (Phase 53) — tenant-scoped artifact storage.
        // Backend is selected via SOVRANT_ARTIFACTS_BACKEND (default: local).
        services.AddSingleton<IArtifactStoreFactory, DefaultArtifactStoreFactory>();
        services.AddSingleton<IArtifactStore>(sp =>
        {
            var backend = Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_BACKEND") ?? "local";
            return sp.GetRequiredService<IArtifactStoreFactory>().Create(backend);
        });
        services.AddSingleton<LegacyArtifactImporter>();
        services.AddSingleton<WorkspaceIdentityMigrator>(sp =>
        {
            var store = sp.GetRequiredService<IArtifactStore>();
            // The factory currently only produces LocalArtifactStore for the
            // local backend; the migrator only makes sense against an on-disk
            // tree, so other backends produce a no-op migrator.
            var root = store is LocalArtifactStore local ? local.Root : string.Empty;
            return new WorkspaceIdentityMigrator(root, sp.GetRequiredService<ILogger<WorkspaceIdentityMigrator>>());
        });

        // Eval framework (Phase 27) — SQLite-backed since Phase 49
        services.AddSingleton<IEvalResultStore>(sp =>
            new SqliteEvalResultStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
        services.AddSingleton<IEvalRunner, EvalRunner>();

        // MCP
        services.AddSingleton<IMcpClientFactory, SovrantMcpClientFactory>();
        services.AddSingleton<McpClientRegistry>();
        services.AddSingleton<McpToolRegistrar>();
        services.AddSingleton<ICredentialStore>(sp =>
            new SqliteCredentialStore(sp.GetRequiredService<ISqliteConnectionFactory>(), bootstrap.KeystorePath));
        // MCP/LSP server-entry stores (V019) — metadata only; secrets live in
        // ICredentialStore under "mcp.{name}.client_secret" / "access_token".
        services.AddSingleton<IMcpServerStore>(sp =>
            new SqliteMcpServerStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
        services.AddSingleton<ILspServerStore>(sp =>
            new SqliteLspServerStore(sp.GetRequiredService<ISqliteConnectionFactory>()));
        services.AddHttpClient("McpOAuth", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<McpOAuthService>();

        // Cost tracking (Phase 55)
        var costProvider = Environment.GetEnvironmentVariable("SOVRANT_COST_PROVIDER") ?? "openrouter";
        if (!string.Equals(costProvider, "none", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<Metrics.OpenRouterPricingClient>();
            services.AddSingleton<Metrics.OpenRouterPricingClient>();
            services.AddSingleton<Metrics.ModelIdNormaliser>();
            services.AddSingleton<Metrics.ICostModel, Metrics.OpenRouterCostModel>();
            services.AddSingleton<Metrics.CostMetricsLogger>();
            services.AddSingleton<Metrics.BudgetEnforcer>();
            services.AddSingleton<Metrics.CostModelLoggerFacade>();
            services.AddSingleton<Metrics.CostDashboardService>();
        }
        else
        {
            services.AddSingleton<Metrics.ICostModel>(Metrics.NullCostModel.Instance);
        }

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

        // Phase 88-F — one-shot legacy config import. Reads
        // ~/.sovrant/{settings,providers,governance}.json into the DB +
        // credential store, then renames the originals to *.json.bak so
        // subsequent boots skip them. Runs after migrations so the new
        // tables exist; runs before MCP bootstrap so any provider-related
        // setup downstream sees the imported state.
        var legacyMigrator = services.GetRequiredService<LegacyConfigMigrator>();
        var sovrantUserId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        await legacyMigrator.RunAsync(sovrantUserId, ct).ConfigureAwait(false);

        // Phase 88-C — apply persisted user preferences and the active
        // provider's credential to the runtime SovrantConfig. The migrator
        // (88-F) imports legacy *.json into the DB on first boot; this step
        // is what makes those values visible to running code on every boot
        // thereafter. Without it, second-boot users would see SovrantConfig
        // defaults instead of their saved settings (settings.json is now
        // .bak).
#pragma warning disable CA1031 // Preference load is best-effort; any failure must not abort runtime init
#pragma warning disable CA1848 // ILoggerFactory may not be registered yet; inline call is intentional fallback
        try
        {
            await ApplyUserPreferencesAsync(services, sovrantUserId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            services.GetService<ILoggerFactory>()
                ?.CreateLogger("Sovrant.Runtime.ServiceCollectionExtensions")
                .LogWarning(ex, "Could not apply user preferences; using defaults");
        }
#pragma warning restore CA1848
#pragma warning restore CA1031

        // Load model capability overrides (Phase 54) — bundled + user + env.
        // Must run before live fetch so overrides take priority.
        var overrideLoader = services.GetRequiredService<Sovrant.Api.Capabilities.ModelOverrideLoader>();
        overrideLoader.LoadAll();

        // Fetch live model metadata from OpenRouter (Phase 54) — best effort,
        // registered as Live source (lowest priority after bundled/user overrides).
        var metadataFetcher = services.GetRequiredService<Sovrant.Api.Capabilities.LiveModelMetadataFetcher>();
        await metadataFetcher.FetchAsync(ct).ConfigureAwait(false);

        // Warm the pricing cache so sync EstimateCost calls don't block (Phase H / 9.10).
        var pricingClient = services.GetService<Sovrant.Runtime.Metrics.OpenRouterPricingClient>();
        if (pricingClient is not null)
            await pricingClient.GetSnapshotAsync(ct).ConfigureAwait(false);

        // Rebuild tier assignments now that live metadata is available (Phase 48).
        var tierResolver = services.GetService<Sovrant.Api.Routing.IModelTierResolver>();
        tierResolver?.Rebuild();

        // Run one-shot legacy artifact migration (Phase 53).
        var importer = services.GetRequiredService<LegacyArtifactImporter>();
        await importer.ImportIfNeededAsync(ct).ConfigureAwait(false);

        // Phase 87 Track D — sweep the legacy `personal/` artifact directory
        // into the canonical `ws-personal-{userId}/` layout. Idempotent.
        var workspaceMigrator = services.GetRequiredService<WorkspaceIdentityMigrator>();
        workspaceMigrator.MigrateIfNeeded();

        var mcpStore = services.GetRequiredService<IMcpServerStore>();
        var mcpServers = await mcpStore.GetAllAsync(ct).ConfigureAwait(false);
        if (mcpServers.Count == 0)
            return;

        var registrar = services.GetRequiredService<McpToolRegistrar>();
        await registrar.RegisterAllAsync(mcpServers, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Phase 88-C — pulls saved user preferences out of <see cref="Preferences.IUserPreferenceStore"/>
    /// and the active provider profile's API key out of
    /// <see cref="Mcp.ICredentialStore"/>, then mutates the singleton
    /// <see cref="SovrantConfig"/> in place. Runs once during
    /// <see cref="InitializeRuntimeAsync"/>, immediately after the legacy
    /// migrator. The DB is the source of truth — ConfigLoader's JSON-file
    /// values are now only the bootstrap default before this step runs.
    /// </summary>
    internal static async Task ApplyUserPreferencesAsync(
        IServiceProvider services, string userId, CancellationToken ct)
    {
        var prefs = services.GetRequiredService<Preferences.IUserPreferenceStore>();
        var profileStore = services.GetRequiredService<Providers.IProviderProfileStore>();
        var credentials = services.GetRequiredService<Mcp.ICredentialStore>();
        var config = services.GetRequiredService<SovrantConfig>();

        // Scalar prefs.
        var model = await prefs.GetAsync(userId, Preferences.UserPreferenceKeys.Model, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(model))
            config.Model = model;

        var maxTokensRaw = await prefs.GetAsync(userId, Preferences.UserPreferenceKeys.MaxTokens, ct).ConfigureAwait(false);
        if (int.TryParse(maxTokensRaw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var maxTokens))
            config.MaxTokens = maxTokens;

        var baseUrlRaw = await prefs.GetAsync(userId, Preferences.UserPreferenceKeys.BaseUrl, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(baseUrlRaw) && Uri.TryCreate(baseUrlRaw, UriKind.Absolute, out var baseUrl))
            config.BaseUrl = baseUrl;

        var permModeRaw = await prefs.GetAsync(userId, Preferences.UserPreferenceKeys.PermissionMode, ct).ConfigureAwait(false);
        if (Enum.TryParse<PermissionMode>(permModeRaw, ignoreCase: true, out var permMode))
            config.PermissionMode = permMode;

        var webSearchRaw = await prefs.GetAsync(userId, Preferences.UserPreferenceKeys.WebSearch, ct).ConfigureAwait(false);
        if (Enum.TryParse<Sovrant.Api.Config.WebSearchBackend>(webSearchRaw, ignoreCase: true, out var webSearch))
            config.WebSearchOverride = webSearch;

        // Active provider profile → API key (and BaseUrl/Model/MaxTokens
        // overrides if set on the profile). Falls back to the global
        // CredentialKeys.LlmApiKey entry for installs that came in via the
        // settings.json migrator path (no profile id pinned).
        string? apiKey = null;
        var activeProfileId = await prefs.GetAsync(
            userId, Preferences.UserPreferenceKeys.ActiveProviderProfileId, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(activeProfileId))
        {
            var profile = await profileStore.GetAsync(activeProfileId, ct).ConfigureAwait(false);
            if (profile is not null)
            {
                if (!string.IsNullOrEmpty(profile.DefaultModel) && string.IsNullOrEmpty(model))
                    config.Model = profile.DefaultModel;
                if (profile.MaxTokens.HasValue)
                    config.MaxTokens = profile.MaxTokens.Value;
                if (!string.IsNullOrEmpty(profile.BaseUrl)
                    && Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var profileBaseUrl))
                    config.BaseUrl = profileBaseUrl;
                apiKey = await credentials.RetrieveAsync(profile.CredentialId, ct).ConfigureAwait(false);
            }
        }

        if (string.IsNullOrEmpty(apiKey))
            apiKey = await credentials.RetrieveAsync(
                Sovrant.Api.Auth.CredentialKeys.LlmApiKey, ct).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(apiKey))
            config.ApiKey = apiKey;
    }
}
