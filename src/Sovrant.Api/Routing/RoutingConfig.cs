using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Sovrant.Api.Routing;

/// <summary>
/// Configuration for intent-aware model routing. Loaded from
/// <c>.sovrant/routing.json</c> with env var overrides.
/// </summary>
public sealed class RoutingConfig
{
    /// <summary>Whether intent-based routing is enabled. Default: true when multiple models are available.</summary>
    [JsonPropertyName("intent_routing")]
    public bool IntentRouting { get; init; } = true;

    /// <summary>Default tier when no intent match or classification fails.</summary>
    [JsonPropertyName("default_tier")]
    public string DefaultTier { get; init; } = ModelTier.Standard;

    /// <summary>Whether to auto-assign models to tiers from pricing data.</summary>
    [JsonPropertyName("auto_tier_assignment")]
    public bool AutoTierAssignment { get; init; } = true;

    /// <summary>
    /// Explicit tier → model ID mappings. Use <c>"auto"</c> for auto-assignment.
    /// Example: <c>{ "fast": "gpt-4o-mini", "standard": "auto", "high": "claude-opus-4-6" }</c>
    /// </summary>
    [JsonPropertyName("tier_models")]
    public IReadOnlyDictionary<string, string> TierModels { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ModelTier.Fast] = "auto",
            [ModelTier.Standard] = "auto",
            [ModelTier.High] = "auto",
        };

    /// <summary>Whether to retry with a higher-tier model on low-quality responses.</summary>
    [JsonPropertyName("escalation")]
    public bool Escalation { get; init; } = true;

    /// <summary>Maximum number of tier escalations per turn.</summary>
    [JsonPropertyName("max_escalations_per_turn")]
    public int MaxEscalationsPerTurn { get; init; } = 1;

    /// <summary>
    /// Custom routing rules. Patterns are matched against the user message text.
    /// Rules are checked in order; the first match wins.
    /// </summary>
    [JsonPropertyName("custom_rules")]
    public IReadOnlyList<CustomRoutingRule> CustomRules { get; init; } = [];
}

/// <summary>A user-defined pattern → tier routing rule.</summary>
public sealed class CustomRoutingRule
{
    /// <summary>Regex pattern to match against user input.</summary>
    [JsonPropertyName("pattern")]
    public string Pattern { get; init; } = string.Empty;

    /// <summary>The tier to use when the pattern matches.</summary>
    [JsonPropertyName("tier")]
    public string Tier { get; init; } = ModelTier.Standard;
}

/// <summary>Loads <see cref="RoutingConfig"/> from <c>.sovrant/routing.json</c>.</summary>
public static class RoutingConfigLoader
{
    private static readonly string[] SearchPaths =
    [
        Path.Combine(Environment.CurrentDirectory, ".sovrant", "routing.json"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "routing.json"),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly Action<ILogger, string, Exception?> LogLoadFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "RoutingConfigLoadFailed"),
            "Failed to load routing config from {Path}, using defaults");

    /// <summary>
    /// Loads routing config from the first <c>.sovrant/routing.json</c> found
    /// in the current directory or home directory. Returns defaults if no file exists.
    /// </summary>
    public static RoutingConfig Load(ILogger? logger = null)
    {
        foreach (var path in SearchPaths)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<RoutingConfig>(json, JsonOptions);
                return config ?? new RoutingConfig();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (logger is not null)
                    LogLoadFailed(logger, path, ex);
            }
        }

        // Check env var override for intent routing toggle
        var envToggle = Environment.GetEnvironmentVariable("SOVRANT_INTENT_ROUTING");
        if (envToggle is not null)
        {
            var enabled = envToggle.Equals("true", StringComparison.OrdinalIgnoreCase)
                || envToggle.Equals("1", StringComparison.Ordinal);
            return new RoutingConfig { IntentRouting = enabled };
        }

        return new RoutingConfig();
    }
}
