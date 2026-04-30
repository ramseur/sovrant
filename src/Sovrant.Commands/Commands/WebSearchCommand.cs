using Sovrant.Api.Config;
using Sovrant.Runtime.Config;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Selects the web-search backend for the current session. Per-session
/// override sits on <see cref="SovrantConfig.WebSearchOverride"/> so a
/// one-off change doesn't stomp the saved default in <c>settings.json</c>.
/// Usage: <c>/websearch</c>, <c>/websearch auto|brave|firecrawl|native|off</c>.
/// Legacy <c>enable</c>/<c>disable</c> map to <c>native</c>/<c>off</c>.
/// </summary>
public sealed class WebSearchCommand : ISlashCommand
{
    private readonly CredentialConfig? _credentials;
    private readonly WebSearchOptions? _options;
    private readonly SovrantConfig? _config;

    public WebSearchCommand(
        CredentialConfig? credentials = null,
        WebSearchOptions? options = null,
        SovrantConfig? config = null)
    {
        _credentials = credentials;
        _options = options;
        _config = config;
    }

    public string Name => "websearch";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Select the web-search backend (auto, brave, firecrawl, native, off).";
    public string Category => "Tools";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var trimmed = (args ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(trimmed))
            return Task.FromResult(new SlashCommandResult(BuildStatus()));

        if (TryParseBackend(trimmed, out var chosen))
        {
            if (_config is not null) _config.WebSearchOverride = chosen;
            return Task.FromResult(new SlashCommandResult(
                $"Web search backend set to '{BackendName(chosen)}' for this session."));
        }

        return Task.FromResult(new SlashCommandResult(
            $"Unknown backend '{trimmed}'. Valid: auto, brave, firecrawl, native, off.\n\n" + BuildStatus()));
    }

    private string BuildStatus()
    {
        var braveSet = !string.IsNullOrWhiteSpace(_credentials?.BraveApiKey ?? Environment.GetEnvironmentVariable("BRAVE_API_KEY"));
        var firecrawlSet = !string.IsNullOrWhiteSpace(_credentials?.FirecrawlApiKey ?? Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY"));
        var resolved = _config?.WebSearchOverride ?? _options?.Backend ?? WebSearchBackend.Auto;
        var source = _config?.WebSearchOverride is not null ? "session override"
                   : _options is not null ? "global default"
                   : "fallback";

        var keys = new List<string>();
        keys.Add(braveSet ? "Brave (key set)" : "Brave (no key)");
        keys.Add(firecrawlSet ? "FireCrawl (key set)" : "FireCrawl (no key)");

        return
            $"Active backend: {BackendName(resolved)} ({source})\n" +
            $"Keys: {string.Join(", ", keys)}\n\n" +
            "Usage:\n" +
            "  /websearch auto      Prefer Brave > Firecrawl > native (default)\n" +
            "  /websearch brave     Use Brave Search exclusively\n" +
            "  /websearch firecrawl Use FireCrawl exclusively\n" +
            "  /websearch native    Delegate to the model's built-in web search\n" +
            "  /websearch off       Disable web search for this session";
    }

    private static string BackendName(WebSearchBackend b) => b switch
    {
        WebSearchBackend.Auto => "auto",
        WebSearchBackend.Brave => "brave",
        WebSearchBackend.Firecrawl => "firecrawl",
        WebSearchBackend.Native => "native",
        WebSearchBackend.SearxngFuture => "searxng-future",
        WebSearchBackend.Off => "off",
        _ => "auto",
    };

    private static bool TryParseBackend(string raw, out WebSearchBackend backend)
    {
        backend = WebSearchBackend.Auto;
        // Legacy aliases.
        if (raw.Equals("enable", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            backend = WebSearchBackend.Native;
            return true;
        }
        if (raw.Equals("disable", StringComparison.OrdinalIgnoreCase))
        {
            backend = WebSearchBackend.Off;
            return true;
        }

        // Normalize "searxng-future" -> "SearxngFuture" for Enum.TryParse.
        var normalized = raw.Replace("-", "", StringComparison.Ordinal);
        return Enum.TryParse(normalized, ignoreCase: true, out backend);
    }
}
