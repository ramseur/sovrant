using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Config;

/// <summary>
/// Phase 148 — one switch for how environment variables relate to what admins set in the app, for
/// both provider keys (Phase 144) and settings:
/// <list type="bullet">
/// <item><b>Off (default):</b> env values <em>seed</em> the database — written once when nothing is stored
/// yet — and after that the stored value wins, so changes made in the app stick.</item>
/// <item><b>On (<c>SOVRANT_ENV_OVERRIDE=true</c>):</b> env values are final. The app shows those controls
/// disabled with a note, and saving them is refused.</item>
/// </list>
/// <c>SOVRANT_ENV_KEYS_OVERRIDE</c> (Phase 144, keys only) is still accepted as the older name.
/// </summary>
public static class EnvOverride
{
    public const string Variable = "SOVRANT_ENV_OVERRIDE";
    public const string LegacyKeysVariable = "SOVRANT_ENV_KEYS_OVERRIDE";

    public static bool IsOn(Func<string, string?>? env = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        return IsTrue(env(Variable)) || IsTrue(env(LegacyKeysVariable));
    }

    /// <summary>The note shown next to a control that's set by the environment.</summary>
    public static string Note(string variable) =>
        $"Set by the server's environment ({variable}). To change it here, turn off {Variable}.";

    private static bool IsTrue(string? value)
    {
        value = value?.Trim();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }
}

/// <summary>
/// Every setting that has an environment variable, and what the <see cref="EnvOverride"/> rule does
/// with it: seed it into the global settings (override off) or lock it (override on).
/// </summary>
public static class EnvBackedSettings
{
    /// <summary>Setting key → its environment variable.</summary>
    public static IReadOnlyDictionary<string, string> Variables { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WorkspaceSettingsKeys.GovernanceLevel] = "SOVRANT_GOVERNANCE_LEVEL",
        [WorkspaceSettingsKeys.GovernanceAuditLog] = "SOVRANT_GOVERNANCE_AUDIT_LOG",
        [WorkspaceSettingsKeys.GovernanceBlockedCommands] = "SOVRANT_GOVERNANCE_BLOCKED_COMMANDS",
        [WorkspaceSettingsKeys.GovernanceProtectedFiles] = "SOVRANT_GOVERNANCE_PROTECTED_FILES",
        [WorkspaceSettingsKeys.GovernanceSecretPatterns] = "SOVRANT_GOVERNANCE_SECRET_PATTERNS",
        [WorkspaceSettingsKeys.GovernanceHostFileTools] = "SOVRANT_GOVERNANCE_HOST_FILE_TOOLS",
        [WorkspaceSettingsKeys.GovernanceMemberFileTools] = "SOVRANT_GOVERNANCE_MEMBER_FILE_TOOLS",
        [WorkspaceSettingsKeys.TrustBoundaryEnabled] = "SOVRANT_TRUSTBOUNDARY_ENABLED",
        [WorkspaceSettingsKeys.SanitizerEnabled] = "SOVRANT_TRUSTBOUNDARY_SANITIZER_ENABLED",
        [WorkspaceSettingsKeys.SanitizerMode] = "SOVRANT_TRUSTBOUNDARY_SANITIZER_MODE",
        [WorkspaceSettingsKeys.SanitizerAllowList] = "SOVRANT_TRUSTBOUNDARY_ALLOW_LIST",
        [WorkspaceSettingsKeys.SanitizerCorporateDomains] = "SOVRANT_TRUSTBOUNDARY_CORPORATE_DOMAINS",
        [WorkspaceSettingsKeys.SanitizerExemptProviders] = "SOVRANT_TRUSTBOUNDARY_EXEMPT_PROVIDERS",
        [WorkspaceSettingsKeys.SanitizerLogRedactions] = "SOVRANT_TRUSTBOUNDARY_LOG_REDACTIONS",
        [WorkspaceSettingsKeys.IntentVerificationEnabled] = "SOVRANT_TRUSTBOUNDARY_INTENT_ENABLED",
        [WorkspaceSettingsKeys.IntentBlockHarmful] = "SOVRANT_TRUSTBOUNDARY_INTENT_BLOCK_HARMFUL",
        [WorkspaceSettingsKeys.IntentClarifyAmbiguous] = "SOVRANT_TRUSTBOUNDARY_INTENT_CLARIFY",
        [WorkspaceSettingsKeys.CompactThreshold] = "SOVRANT_COMPACT_THRESHOLD",
        [WorkspaceSettingsKeys.AgentMaxConcurrent] = "SOVRANT_AGENT_MAX_CONCURRENT",
        [WorkspaceSettingsKeys.AgentTaskTimeoutSeconds] = "SOVRANT_AGENT_TASK_TIMEOUT_SECONDS",
        [WorkspaceSettingsKeys.ExecutorMaxReplans] = "SOVRANT_EXECUTOR_MAX_REPLANS",
        [WorkspaceSettingsKeys.ExecutorMaxStepRetries] = "SOVRANT_EXECUTOR_MAX_STEP_RETRIES",
        [WorkspaceSettingsKeys.WorkflowMaxConcurrent] = "SOVRANT_WORKFLOW_MAX_CONCURRENT",
        [WorkspaceSettingsKeys.WorkflowPollSeconds] = "SOVRANT_WORKFLOW_POLL_SECONDS",
        [WorkspaceSettingsKeys.MaxSessions] = "SOVRANT_MAX_SESSIONS",
        [WorkspaceSettingsKeys.SessionTtlSeconds] = "SOVRANT_SESSION_TTL_SECONDS",
        [WorkspaceSettingsKeys.WebSignInIdleMinutes] = Auth.WebSignInPolicy.IdleMinutesVariable,
        [WorkspaceSettingsKeys.WebSignInMaxHours] = Auth.WebSignInPolicy.MaxSessionHoursVariable,
        [WorkspaceSettingsKeys.WebSignInRememberDays] = Auth.WebSignInPolicy.RememberDaysVariable,
        // "Keep me signed in" on/off comes from the same variable: 0 days = off.
        [WorkspaceSettingsKeys.WebSignInRememberAllowed] = Auth.WebSignInPolicy.RememberDaysVariable,
        [WorkspaceSettingsKeys.RunMaxMinutes] = Conversation.RunLimits.MaxRunMinutesVariable,
    };

    /// <summary>
    /// The variable that sets <paramref name="key"/> while the override is on, or null when the app may
    /// change it (override off, or that variable isn't set).
    /// </summary>
    public static string? LockedBy(string key, Func<string, string?>? env = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        return EnvOverride.IsOn(env) && Variables.TryGetValue(key, out var variable) && !string.IsNullOrWhiteSpace(env(variable))
            ? variable
            : null;
    }

    /// <summary>
    /// Override off: writes each set variable into the global settings once, where nothing is stored
    /// yet. Returns the keys it wrote. Does nothing with the override on (env is read directly then).
    /// </summary>
    public static async Task<IReadOnlyList<string>> SeedAsync(IWorkspaceSettingsStore store, Func<string, string?>? env = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        env ??= Environment.GetEnvironmentVariable;
        if (EnvOverride.IsOn(env)) return [];
        var seeded = new List<string>();
        foreach (var (key, variable) in Variables)
        {
            var raw = env(variable)?.Trim();
            if (string.IsNullOrEmpty(raw)) continue;
            if (key == WorkspaceSettingsKeys.WebSignInRememberAllowed)
                raw = raw == "0" ? "false" : "true";
            else if (key == WorkspaceSettingsKeys.WebSignInRememberDays && raw == "0")
                continue; // 0 means "off", not a length
            if (await store.GetGlobalAsync(key, ct).ConfigureAwait(false) is not null) continue;
            await store.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, key, raw, ct).ConfigureAwait(false);
            seeded.Add(key);
        }
        return seeded;
    }
}

/// <summary>
/// With the override on, refuses changes to global settings the environment sets — the app shows
/// them disabled, and this makes sure nothing else (API, an old page) changes them either.
/// </summary>
public sealed class EnvLockedSettingsStore(IWorkspaceSettingsStore inner, Func<string, string?>? env = null) : IWorkspaceSettingsStore
{
    public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default) => inner.GetGlobalAsync(key, ct);
    public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default) => inner.GetAsync(workspaceId, key, ct);
    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default) => inner.GetAllAsync(workspaceId, ct);

    public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default) =>
        IsLocked(workspaceId, key) ? Task.CompletedTask : inner.SetAsync(workspaceId, key, value, ct);

    public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default) =>
        IsLocked(workspaceId, key) ? Task.CompletedTask : inner.DeleteAsync(workspaceId, key, ct);

    private bool IsLocked(string workspaceId, string key) =>
        string.IsNullOrEmpty(workspaceId) && EnvBackedSettings.LockedBy(key, env) is not null; // global ("")
}

/// <summary>For a page: whether one control is set by the environment (override on), and the note to show.</summary>
public sealed record EnvLock(string? Variable)
{
    public bool IsLocked => Variable is not null;
    public bool IsEditable => Variable is null;
    public string Note => Variable is null ? string.Empty : EnvOverride.Note(Variable);

    /// <summary>The lock for a setting key.</summary>
    public static EnvLock ForSetting(string key) => new(EnvBackedSettings.LockedBy(key));

    /// <summary>The lock for a credential (provider key).</summary>
    public static EnvLock ForCredential(string credentialKey) => new(EnvCredentialSeeder.LockedBy(credentialKey));
}
