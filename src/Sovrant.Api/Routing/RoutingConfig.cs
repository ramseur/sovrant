using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Api.Routing;

/// <summary>
/// Configuration for intent-aware model routing. All fields are loaded from
/// environment variables (or a <c>.env</c> file pre-loaded by
/// <c>BootstrapConfigLoader</c>).
/// </summary>
public sealed class RoutingConfig
{
    /// <summary>Whether intent-based routing is enabled. Env: <c>SOVRANT_INTENT_ROUTING</c>.</summary>
    public bool IntentRouting { get; init; }

    /// <summary>Default tier when no intent match or classification fails. Env: <c>SOVRANT_ROUTING_DEFAULT_TIER</c>.</summary>
    public string DefaultTier { get; init; } = ModelTier.Standard;

    /// <summary>Whether to auto-assign models to tiers from pricing data. Env: <c>SOVRANT_ROUTING_AUTO_TIER</c>.</summary>
    public bool AutoTierAssignment { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, only models with zero cost are eligible for tier assignment.
    /// Env: <c>SOVRANT_FREE_MODELS_ONLY</c>.
    /// </summary>
    public bool FreeModelsOnly { get; init; }

    /// <summary>
    /// Explicit tier → model ID mappings. Use <c>"auto"</c> for auto-assignment.
    /// Env: <c>SOVRANT_TIER_MODELS</c> (JSON object, e.g. <c>{"fast":"gpt-4o-mini","standard":"auto"}</c>).
    /// </summary>
    public IReadOnlyDictionary<string, string> TierModels { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ModelTier.Fast]     = "auto",
            [ModelTier.Standard] = "auto",
            [ModelTier.High]     = "auto",
        };

    /// <summary>Whether to retry with a higher-tier model on low-quality responses. Env: <c>SOVRANT_ROUTING_ESCALATION</c>.</summary>
    public bool Escalation { get; init; } = true;

    /// <summary>Maximum number of tier escalations per turn. Env: <c>SOVRANT_ROUTING_MAX_ESCALATIONS</c>.</summary>
    public int MaxEscalationsPerTurn { get; init; } = 1;

    /// <summary>
    /// Custom routing rules matched against user message text in order; first match wins.
    /// Env: <c>SOVRANT_ROUTING_RULES</c> (JSON array, e.g. <c>[{"pattern":"fix.*bug","tier":"high"}]</c>).
    /// </summary>
    public IReadOnlyList<CustomRoutingRule> CustomRules { get; init; } = [];
}

/// <summary>A user-defined pattern → tier routing rule.</summary>
public sealed class CustomRoutingRule
{
    /// <summary>Regex pattern matched against user input.</summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>The tier to use when the pattern matches.</summary>
    public string Tier { get; init; } = ModelTier.Standard;
}

/// <summary>
/// Loads <see cref="RoutingConfig"/> from environment variables.
/// The <c>.env</c> file (if present) is pre-loaded by <c>BootstrapConfigLoader</c>
/// before DI resolves this singleton, so all env var reads here see its values.
/// </summary>
public static class RoutingConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly Action<ILogger, string, string, Exception?> LogParseFailed =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(1, "RoutingConfigParseFailed"),
            "Failed to parse {Variable} as {Type}, using default");

    private static readonly Action<ILogger, Exception?> LogTierModelsFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, "TierModelsParseError"),
            "Failed to parse SOVRANT_TIER_MODELS as JSON object, using defaults");

    private static readonly Action<ILogger, Exception?> LogRulesFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, "RoutingRulesParseError"),
            "Failed to parse SOVRANT_ROUTING_RULES as JSON array, using empty rules");

    /// <summary>Builds a <see cref="RoutingConfig"/> from the current process environment.</summary>
    public static RoutingConfig Load(ILogger? logger = null) => new()
    {
        IntentRouting        = ReadBool("SOVRANT_INTENT_ROUTING",          false),
        FreeModelsOnly       = ReadBool("SOVRANT_FREE_MODELS_ONLY",         false),
        DefaultTier          = Env("SOVRANT_ROUTING_DEFAULT_TIER")          ?? ModelTier.Standard,
        AutoTierAssignment   = ReadBool("SOVRANT_ROUTING_AUTO_TIER",        true),
        Escalation           = ReadBool("SOVRANT_ROUTING_ESCALATION",       true),
        MaxEscalationsPerTurn = ReadInt("SOVRANT_ROUTING_MAX_ESCALATIONS",  1, logger),
        TierModels           = ReadTierModels(logger),
        CustomRules          = ReadCustomRules(logger),
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, string> ReadTierModels(ILogger? logger)
    {
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ModelTier.Fast]     = "auto",
            [ModelTier.Standard] = "auto",
            [ModelTier.High]     = "auto",
        };

        var raw = Env("SOVRANT_TIER_MODELS");
        if (raw is null) return defaults;

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(raw, JsonOptions);
            if (parsed is not null)
                return new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            if (logger is not null) LogTierModelsFailed(logger, ex);
        }

        return defaults;
    }

    private static List<CustomRoutingRule> ReadCustomRules(ILogger? logger)
    {
        var raw = Env("SOVRANT_ROUTING_RULES");
        if (raw is null) return [];

        try
        {
            return JsonSerializer.Deserialize<List<CustomRoutingRule>>(raw, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            if (logger is not null) LogRulesFailed(logger, ex);
            return [];
        }
    }

    private static bool ReadBool(string name, bool defaultValue)
    {
        var v = Env(name);
        if (v is null) return defaultValue;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("1", StringComparison.Ordinal);
    }

    private static int ReadInt(string name, int defaultValue, ILogger? logger)
    {
        var v = Env(name);
        if (v is null) return defaultValue;
        if (int.TryParse(v, out var i)) return i;
        if (logger is not null) LogParseFailed(logger, name, "integer", null);
        return defaultValue;
    }

    private static string? Env(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? null : v;
    }
}
