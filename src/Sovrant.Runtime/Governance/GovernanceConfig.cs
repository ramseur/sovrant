using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    {
        workingDirectory ??= Directory.GetCurrentDirectory();

        var globalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "governance.json");

        var projectPath = Path.Combine(workingDirectory, ".sovrant", "governance.json");

        // Start with defaults, then layer global, then project-local.
        var config = TryLoad(globalPath) ?? new GovernanceConfig();
        var projectConfig = TryLoad(projectPath);
        if (projectConfig is not null)
            config = Merge(config, projectConfig);

        // Env var override for governance level
        var envLevel = Environment.GetEnvironmentVariable("SOVRANT_GOVERNANCE_LEVEL");
        if (!string.IsNullOrWhiteSpace(envLevel))
            config.GovernanceLevelName = envLevel;

        return config;
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
