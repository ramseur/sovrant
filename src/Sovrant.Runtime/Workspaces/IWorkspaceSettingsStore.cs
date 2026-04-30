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

    /// <summary>Reserved workspace ID for global / server-default rows.</summary>
    public const string GlobalWorkspaceId = "";
}
