using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Configuration for the governance monitor, loaded from <c>.sovrant/governance.json</c>
/// or <c>~/.sovrant/governance.json</c>.
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
    /// Loads config by merging project-local (<c>.sovrant/governance.json</c>) over
    /// global (<c>~/.sovrant/governance.json</c>), then applying the
    /// <c>SOVRANT_GOVERNANCE_LEVEL</c> env var override.
    /// </summary>
    public static GovernanceConfig Load(string? workingDirectory = null)
        => Load(workingDirectory, settings: null);

    /// <summary>
    /// Loads config with the full env &gt; <see cref="IWorkspaceSettingsStore"/> &gt;
    /// JSON files &gt; defaults precedence chain. JSON files remain the bootstrap
    /// fallback so existing <c>.sovrant/governance.json</c> setups keep working
    /// until the user re-saves through the Settings UI.
    /// </summary>
    public static GovernanceConfig Load(string? workingDirectory, IWorkspaceSettingsStore? settings)
    {
        workingDirectory ??= Directory.GetCurrentDirectory();

        var globalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "governance.json");

        var projectPath = Path.Combine(workingDirectory, ".sovrant", "governance.json");

        // Start with defaults, then layer global, then project-local — these
        // serve as the snapshot fallback.
        var config = TryLoad(globalPath) ?? new GovernanceConfig();
        var projectConfig = TryLoad(projectPath);
        if (projectConfig is not null)
            config = Merge(config, projectConfig);

        // Layer DB values over the JSON-file snapshot. Env vars (read inside the
        // resolver) win over both. List values fully replace, matching the
        // "DB is the persistent source of truth" model.
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
    /// Settings UIs call this instead of writing <c>~/.sovrant/governance.json</c>;
    /// the JSON file remains the bootstrap fallback.
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

    private static GovernanceConfig? TryLoad(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GovernanceConfig>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static GovernanceConfig Merge(GovernanceConfig baseConfig, GovernanceConfig overlay)
    {
        // Overlay wins for scalar values
        var merged = new GovernanceConfig
        {
            GovernanceLevelName = overlay.GovernanceLevelName,
            AuditLog = overlay.AuditLog,
        };

        // Merge collections (union)
        foreach (var cmd in baseConfig.BlockedCommands)
            merged.BlockedCommands.Add(cmd);
        foreach (var cmd in overlay.BlockedCommands)
        {
            if (!merged.BlockedCommands.Contains(cmd))
                merged.BlockedCommands.Add(cmd);
        }

        foreach (var f in baseConfig.ProtectedFiles)
            merged.ProtectedFiles.Add(f);
        foreach (var f in overlay.ProtectedFiles)
        {
            if (!merged.ProtectedFiles.Contains(f))
                merged.ProtectedFiles.Add(f);
        }

        foreach (var p in baseConfig.SecretPatterns)
            merged.SecretPatterns.Add(p);
        foreach (var p in overlay.SecretPatterns)
        {
            if (!merged.SecretPatterns.Contains(p))
                merged.SecretPatterns.Add(p);
        }

        return merged;
    }
}
