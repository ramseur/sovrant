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
}
