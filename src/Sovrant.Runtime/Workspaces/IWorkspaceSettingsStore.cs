namespace Sovrant.Runtime.Workspaces;

/// <summary>
/// Key/value store for workspace-scoped runtime settings (budgets, session
/// caps, etc.). Workspace ID <c>""</c> represents the global default row.
/// Env vars still win at call sites — these values are the persistent
/// fallback.
/// </summary>
public interface IWorkspaceSettingsStore
{
    /// <summary>
    /// Returns the value for <paramref name="key"/> in the given workspace,
    /// falling back to the global row when no workspace-specific row exists.
    /// </summary>
    Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default);

    /// <summary>Returns the global value for <paramref name="key"/>, or null.</summary>
    Task<string?> GetGlobalAsync(string key, CancellationToken ct = default);

    /// <summary>Upserts a value. Pass <c>""</c> as workspace ID to set the global default.</summary>
    Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default);

    /// <summary>Deletes a key for a workspace (no-op if missing).</summary>
    Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default);

    /// <summary>Returns all settings for a workspace, merged over the global row.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default);
}

/// <summary>Well-known keys for <see cref="IWorkspaceSettingsStore"/>.</summary>
public static class WorkspaceSettingsKeys
{
    /// <summary>Per-session USD budget cap (decimal, invariant culture).</summary>
    public const string SessionBudgetUsd = "budget.session_usd";

    /// <summary>Per-project USD budget cap (decimal, invariant culture).</summary>
    public const string ProjectBudgetUsd = "budget.project_usd";

    /// <summary>Idle-session TTL in seconds before eviction (integer).</summary>
    public const string SessionTtlSeconds = "session.ttl_seconds";

    /// <summary>Maximum live sessions before LRU eviction (integer).</summary>
    public const string MaxSessions = "session.max_sessions";

    /// <summary>Max concurrent background sessions per user (int 1–5). Default 5.</summary>
    public const string MaxActiveSessions = "session.max_active_sessions";

    /// <summary>Whether background session execution is enabled (bool). Default true.</summary>
    public const string BackgroundSessionsEnabled = "session.background_enabled";

    /// <summary>Hard ceiling on re-plan requests per executor run (integer). Default 3.</summary>
    public const string ExecutorMaxReplans = "executor.max_replans";

    /// <summary>Retries allowed on a failed step before escalating to a re-plan (integer). Default 2.</summary>
    public const string ExecutorMaxStepRetries = "executor.max_step_retries";

    /// <summary>Maximum simultaneous agents per orchestration run (integer). Default 5.</summary>
    public const string AgentMaxConcurrent = "agent.max_concurrent";

    /// <summary>Per-task agent timeout in seconds (integer). Default 120.</summary>
    public const string AgentTaskTimeoutSeconds = "agent.task_timeout_seconds";

    /// <summary>Input token count that triggers conversation auto-compaction (integer). 0 disables. Default 80000.</summary>
    public const string CompactThreshold = "compact.threshold";

    /// <summary>Governance level: <c>standard</c>, <c>strict</c>, <c>permissive</c>, <c>minimal</c>, or <c>custom</c>. Env: <c>SOVRANT_GOVERNANCE_LEVEL</c>.</summary>
    public const string GovernanceLevel = "governance.level";

    /// <summary>Whether the audit log is enabled (bool, <c>true</c>/<c>false</c>). Default true.</summary>
    public const string GovernanceAuditLog = "governance.audit_log";

    /// <summary>JSON-encoded array of blocked shell commands (e.g. <c>["rm -rf /", ":(){ :|:&amp; };:"]</c>).</summary>
    public const string GovernanceBlockedCommands = "governance.blocked_commands";

    /// <summary>JSON-encoded array of protected file globs (e.g. <c>[".env", "*.key"]</c>).</summary>
    public const string GovernanceProtectedFiles = "governance.protected_files";

    /// <summary>JSON-encoded array of regex patterns identifying secrets in tool output.</summary>
    public const string GovernanceSecretPatterns = "governance.secret_patterns";

    /// <summary>Master switch for the trust boundary pipeline (bool). Default false (opt-in).</summary>
    public const string TrustBoundaryEnabled = "trustboundary.enabled";

    /// <summary>Whether the prompt sanitizer stage is enabled (bool). Default true.</summary>
    public const string SanitizerEnabled = "trustboundary.sanitizer.enabled";

    /// <summary>Sanitizer mode: <c>redact</c>, <c>warn</c>, or <c>block</c>. Default <c>redact</c>.</summary>
    public const string SanitizerMode = "trustboundary.sanitizer.mode";

    /// <summary>JSON-encoded array of internal/corporate domain suffixes treated as sensitive.</summary>
    public const string SanitizerCorporateDomains = "trustboundary.sanitizer.corporate_domains";

    /// <summary>JSON-encoded array of <see cref="TrustBoundary.CustomPattern"/> records (Name + RegexPattern + Action).</summary>
    public const string SanitizerCustomPatterns = "trustboundary.sanitizer.custom_patterns";

    /// <summary>JSON-encoded array of patterns/domains never to be redacted.</summary>
    public const string SanitizerAllowList = "trustboundary.sanitizer.allow_list";

    /// <summary>JSON-encoded array of provider names that bypass sanitization (e.g. <c>ollama</c>).</summary>
    public const string SanitizerExemptProviders = "trustboundary.sanitizer.exempt_providers";

    /// <summary>Whether to log redactions for audit (bool). Default false.</summary>
    public const string SanitizerLogRedactions = "trustboundary.sanitizer.log_redactions";

    /// <summary>Whether intent verification is enabled (bool). Default true.</summary>
    public const string IntentVerificationEnabled = "trustboundary.intent.enabled";

    /// <summary>Whether to clarify ambiguous user intent before sending (bool). Default true.</summary>
    public const string IntentClarifyAmbiguous = "trustboundary.intent.clarify_ambiguous";

    /// <summary>Whether to block clearly harmful intent at this layer (bool). Default true.</summary>
    public const string IntentBlockHarmful = "trustboundary.intent.block_harmful";

    /// <summary>Reserved workspace ID for global / server-default rows.</summary>
    public const string GlobalWorkspaceId = "";

    // ── Provider / LLM ───────────────────────────────────────────────────────

    /// <summary>
    /// Profile ID of the workspace-level active provider, set by an admin.
    /// Overrides each member's personal <c>provider.active_profile_id</c> user-preference.
    /// The API key for this profile is never exposed to non-admin members.
    /// </summary>
    public const string ActiveProviderProfileId = "provider.active_profile_id";

    /// <summary>
    /// Comma-separated list of global provider profile IDs enabled for this workspace.
    /// When empty or absent, no providers are available — admin must explicitly opt in.
    /// </summary>
    public const string EnabledProviderProfileIds = "provider.enabled_profile_ids";

    /// <summary>
    /// Comma-separated list of MCP server IDs (McpServerEntry.Id) enabled for this workspace.
    /// When empty or absent, no MCP servers are available to workspace members — admin must explicitly opt in.
    /// </summary>
    public const string EnabledMcpServerIds = "mcp.enabled_server_ids";

    // ── General / LLM behaviour ──────────────────────────────────────────────

    /// <summary>Max tokens per LLM response, workspace-wide override (int). Overrides user preference.</summary>
    public const string GeneralMaxTokens = "general.max_tokens";

    /// <summary>Whether response streaming is enabled workspace-wide (bool: true/false).</summary>
    public const string GeneralStreaming = "general.streaming";

    /// <summary>Whether semantic intent routing is enabled workspace-wide (bool: true/false).</summary>
    public const string GeneralIntentRouting = "general.intent_routing";

    /// <summary>Web search backend workspace-wide (string: Auto/Brave/Firecrawl/Native/Off).</summary>
    public const string GeneralWebSearchBackend = "general.web_search_backend";

    // ── Swarm orchestrator ────────────────────────────────────────────────

    /// <summary>Whether the swarm orchestrator is enabled (bool). Default false.</summary>
    public const string SwarmEnabled = "swarm.enabled";

    /// <summary>Maximum agents running concurrently within a wave (int). Default 4.</summary>
    public const string SwarmMaxConcurrent = "swarm.max_concurrent";

    /// <summary>Token budget ceiling across all tasks (int). Default 500000.</summary>
    public const string SwarmMaxTokenBudget = "swarm.max_token_budget";

    /// <summary>Per-task retry count on failure (int). Default 1.</summary>
    public const string SwarmMaxRetries = "swarm.max_retries";

    /// <summary>Whether to run a quality gate review after execution (bool). Default true.</summary>
    public const string SwarmQualityGate = "swarm.quality_gate";

    /// <summary>Minimum score (0–10) required for quality gate to pass without retry (int). Default 7.</summary>
    public const string SwarmQualityGateThreshold = "swarm.quality_gate_threshold";

    /// <summary>Whether to acquire per-file locks during parallel execution (bool). Default true.</summary>
    public const string SwarmFileLocks = "swarm.file_locks";

    /// <summary>Recommended model level for the decomposer agent (string). Default "High".</summary>
    public const string SwarmDecomposerLevel = "swarm.decomposer_level";

    /// <summary>Recommended model level for worker agents (string). Default "Standard".</summary>
    public const string SwarmWorkerLevel = "swarm.worker_level";

    /// <summary>Per-task timeout in seconds (int). Default 300.</summary>
    public const string SwarmTaskTimeoutSeconds = "swarm.task_timeout_seconds";

    /// <summary>Permission mode for swarm agents: "ask", "accept-edits", or "yolo". Default "ask".</summary>
    public const string SwarmPermissions = "swarm.permissions";

    /// <summary>JSON-encoded dict of task-type → agent-template overrides (e.g. <c>{"code_change":"coder"}</c>).</summary>
    public const string SwarmTemplateOverrides = "swarm.templates";

    /// <summary>Seconds between WorkflowSchedulerService poll ticks (integer). Default 20.</summary>
    public const string WorkflowPollSeconds = "workflow.poll_seconds";

    /// <summary>Max workflows WorkflowSchedulerService advances concurrently per tick (integer). Default 3.</summary>
    public const string WorkflowMaxConcurrent = "workflow.max_concurrent";
}
