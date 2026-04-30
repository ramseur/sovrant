using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Sovrant.Api.Config;

/// <summary>
/// Selects which web-search backend the runtime should use. Resolved
/// once at startup by <see cref="WebSearchOptions.Resolve"/> from the
/// standard precedence chain.
/// </summary>
public enum WebSearchBackend
{
    /// <summary>
    /// Default. Try paid keys first (Brave, then FireCrawl), fall back to
    /// the active provider's native web-search capability when available,
    /// then fall back to the LLM's general knowledge. Best for users who
    /// don't want to think about it.
    /// </summary>
    Auto = 0,

    /// <summary>Use Brave Search exclusively. Requires <c>BRAVE_API_KEY</c>.</summary>
    Brave = 1,

    /// <summary>Use FireCrawl exclusively. Requires <c>FIRECRAWL_API_KEY</c>.</summary>
    Firecrawl = 2,

    /// <summary>
    /// Use the active provider's native web search (OpenAI Responses
    /// <c>web_search_preview</c>, OpenRouter <c>plugins:[{id:"web"}]</c>,
    /// Gemini <c>tools:[{google_search:{}}]</c>, Anthropic
    /// <c>web_search_20250305</c>). The function-tool form of WebSearch
    /// is suppressed in this mode — the model handles search itself.
    /// </summary>
    Native = 3,

    /// <summary>
    /// Reserved for the planned SearXNG provider (Phase 49). Resolves
    /// like <see cref="Auto"/> until that lands so existing configs don't
    /// silently break when the value is parsed.
    /// </summary>
    SearxngFuture = 4,

    /// <summary>Disable web search entirely. The WebSearch tool returns a "disabled" message.</summary>
    Off = 5,
}

internal static partial class WebSearchOptionsLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "LLM_WEB_SEARCH=true is deprecated. Set SOVRANT_WEB_SEARCH=native instead. The legacy variable will continue to work but may be removed in a future major release.")]
    public static partial void LegacyEnvVarDeprecated(ILogger logger);
}

/// <summary>
/// Read-once snapshot of the user's web-search preference. Resolves the
/// precedence chain at startup so no consumer has to repeat the logic.
/// </summary>
public sealed class WebSearchOptions
{
    /// <summary>The chosen backend.</summary>
    public WebSearchBackend Backend { get; init; } = WebSearchBackend.Auto;

    /// <summary>
    /// True when the legacy <c>LLM_WEB_SEARCH=true</c> env var was the
    /// source of <see cref="Backend"/>. Used by the registration code to
    /// emit a one-time deprecation warning.
    /// </summary>
    public bool ResolvedFromLegacyEnv { get; init; }

    /// <summary>
    /// Resolves the backend from environment, configuration, and the
    /// legacy <c>LLM_WEB_SEARCH</c> env var (in that priority order).
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">
    /// Optional logger. When supplied and the legacy env var was used,
    /// emits a deprecation warning. Pass <see langword="null"/> from
    /// tests where logger output is not desired.
    /// </param>
    public static WebSearchOptions Resolve(IConfiguration configuration, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // 1. Explicit env var: SOVRANT_WEB_SEARCH=auto|brave|firecrawl|native|off|searxng-future
        var envValue = Environment.GetEnvironmentVariable("SOVRANT_WEB_SEARCH");
        if (TryParseBackend(envValue, out var fromEnv))
            return new WebSearchOptions { Backend = fromEnv };

        // 2. Configuration key: WebSearch:Backend
        var configValue = configuration["WebSearch:Backend"];
        if (TryParseBackend(configValue, out var fromConfig))
            return new WebSearchOptions { Backend = fromConfig };

        // 3. Legacy: LLM_WEB_SEARCH=true → Native (with deprecation warn).
        var legacy = Environment.GetEnvironmentVariable("LLM_WEB_SEARCH");
        if (string.Equals(legacy, "true", StringComparison.OrdinalIgnoreCase))
        {
            if (logger is not null) WebSearchOptionsLog.LegacyEnvVarDeprecated(logger);
            return new WebSearchOptions { Backend = WebSearchBackend.Native, ResolvedFromLegacyEnv = true };
        }

        // 4. Default
        return new WebSearchOptions { Backend = WebSearchBackend.Auto };
    }

    private static bool TryParseBackend(string? raw, out WebSearchBackend backend)
    {
        backend = WebSearchBackend.Auto;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        // Accept "searxng-future" as a synonym for the enum's SearxngFuture.
        var normalized = raw.Trim().Replace("-", "", StringComparison.Ordinal);
        return Enum.TryParse(normalized, ignoreCase: true, out backend);
    }
}
