using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Configuration for the governance monitor, persisted to the
/// <see cref="IWorkspaceSettingsStore"/> global row. The legacy
/// <c>governance.json</c> file is no longer read at runtime — Phase 88-F's
/// <c>LegacyConfigMigrator</c> imports any existing file into the DB on
/// first boot and renames it to <c>.bak</c>.
/// </summary>
public sealed class GovernanceConfig
{
    [JsonPropertyName("governance_level")]
    public string GovernanceLevelName { get; set; } = "standard";

    [JsonPropertyName("blocked_commands")]
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Collection<string> BlockedCommands { get; } = [];

    [JsonPropertyName("protected_files")]
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Collection<string> ProtectedFiles { get; } = [];

    [JsonPropertyName("secret_patterns")]
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Collection<string> SecretPatterns { get; } = [];

    [JsonPropertyName("audit_log")]
    public bool AuditLog { get; set; } = true;

    /// <summary>Parses the configured governance level.</summary>
    [JsonIgnore]
    public GovernanceLevel Level =>
        Enum.TryParse<GovernanceLevel>(GovernanceLevelName, ignoreCase: true, out var level)
            ? level
            : GovernanceLevel.Standard;

    /// <summary>
    /// Resolves config in env &gt; <see cref="IWorkspaceSettingsStore"/> &gt;
    /// defaults order. A null store yields defaults (overridable by env vars).
    /// </summary>
    public static GovernanceConfig Load(IWorkspaceSettingsStore? settings)
    {
        var config = new GovernanceConfig();

        config.GovernanceLevelName = WorkspaceSettingsResolver.ResolveString(
            settings, WorkspaceSettingsKeys.GovernanceLevel,
            "SOVRANT_GOVERNANCE_LEVEL", fallback: config.GovernanceLevelName)
            ?? config.GovernanceLevelName;

        config.AuditLog = WorkspaceSettingsResolver.ResolveBool(
            settings, WorkspaceSettingsKeys.GovernanceAuditLog,
            "SOVRANT_GOVERNANCE_AUDIT_LOG", fallback: config.AuditLog);

        ReplaceList(config.BlockedCommands, WorkspaceSettingsResolver.ResolveStringList(
            settings, WorkspaceSettingsKeys.GovernanceBlockedCommands,
            "SOVRANT_GOVERNANCE_BLOCKED_COMMANDS", fallback: config.BlockedCommands));

        ReplaceList(config.ProtectedFiles, WorkspaceSettingsResolver.ResolveStringList(
            settings, WorkspaceSettingsKeys.GovernanceProtectedFiles,
            "SOVRANT_GOVERNANCE_PROTECTED_FILES", fallback: config.ProtectedFiles));

        ReplaceList(config.SecretPatterns, WorkspaceSettingsResolver.ResolveStringList(
            settings, WorkspaceSettingsKeys.GovernanceSecretPatterns,
            "SOVRANT_GOVERNANCE_SECRET_PATTERNS", fallback: config.SecretPatterns));

        return config;
    }

    /// <summary>
    /// Persists this config's five governance fields to the global
    /// <see cref="IWorkspaceSettingsStore"/> row. Lists are JSON-encoded.
    /// </summary>
    public async Task SaveToStoreAsync(IWorkspaceSettingsStore store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        Validate();
        var ws = WorkspaceSettingsKeys.GlobalWorkspaceId;
        await store.SetAsync(ws, WorkspaceSettingsKeys.GovernanceLevel,
            GovernanceLevelName, ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.GovernanceAuditLog,
            AuditLog ? "true" : "false", ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.GovernanceBlockedCommands,
            JsonSerializer.Serialize(BlockedCommands), ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.GovernanceProtectedFiles,
            JsonSerializer.Serialize(ProtectedFiles), ct).ConfigureAwait(false);
        await store.SetAsync(ws, WorkspaceSettingsKeys.GovernanceSecretPatterns,
            JsonSerializer.Serialize(SecretPatterns), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if the config has fields the runtime
    /// cannot honour: an unrecognised governance level or a non-compiling secret regex.
    /// Settings UIs call this on save so a bad value never reaches the DB.
    /// </summary>
    public void Validate()
    {
        if (!Enum.TryParse<GovernanceLevel>(GovernanceLevelName, ignoreCase: true, out _))
            throw new ArgumentException(
                $"GovernanceLevelName must be a valid {nameof(GovernanceLevel)}; got '{GovernanceLevelName}'.",
                nameof(GovernanceLevelName));

        foreach (var pattern in SecretPatterns)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(
                    $"Secret pattern '{pattern}' is not a valid regex: {ex.Message}",
                    nameof(SecretPatterns), ex);
            }
        }
    }

    private static void ReplaceList(Collection<string> target, IReadOnlyList<string> source)
    {
        if (ReferenceEquals(target, source)) return;
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }
}
