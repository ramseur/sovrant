using Sovrant.Api.Auth;
using Sovrant.Api.Config;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;

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
    private readonly ICredentialStore? _credentialStore;
    private readonly WebSearchOptions? _options;
    private readonly SovrantConfig? _config;

    public WebSearchCommand(
        CredentialConfig? credentials = null,
        ICredentialStore? credentialStore = null,
        WebSearchOptions? options = null,
        SovrantConfig? config = null)
    {
        _credentials = credentials;
        _credentialStore = credentialStore;
        _options = options;
        _config = config;
    }

    public string Name => "websearch";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Select the web-search backend (auto, brave, firecrawl, native, off).";
    public string Category => "Tools";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var trimmed = (args ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(trimmed))
            return new SlashCommandResult(await BuildStatusAsync(ct).ConfigureAwait(false));

        if (TryParseBackend(trimmed, out var chosen))
        {
            if (_config is not null) _config.WebSearchOverride = chosen;
            return new SlashCommandResult(
                $"Web search backend set to '{BackendName(chosen)}' for this session.");
        }

        return new SlashCommandResult(
            $"Unknown backend '{trimmed}'. Valid: auto, brave, firecrawl, native, off.\n\n"
            + await BuildStatusAsync(ct).ConfigureAwait(false));
    }

    private async Task<string> BuildStatusAsync(CancellationToken ct)
    {
        var braveKey = await CredentialResolver.ResolveAsync(
            _credentialStore, CredentialKeys.BraveApiKey, "BRAVE_API_KEY",
            _credentials?.BraveApiKey, ct).ConfigureAwait(false);
        var firecrawlKey = await CredentialResolver.ResolveAsync(
            _credentialStore, CredentialKeys.FirecrawlApiKey, "FIRECRAWL_API_KEY",
            _credentials?.FirecrawlApiKey, ct).ConfigureAwait(false);
        var braveSet = !string.IsNullOrWhiteSpace(braveKey);
        var firecrawlSet = !string.IsNullOrWhiteSpace(firecrawlKey);
        var resolved = _config?.WebSearchOverride ?? _options?.Backend ?? WebSearchBackend.Auto;
        var source = _config?.WebSearchOverride is not null ? "session override"
                   : _options is not null ? "global default"
                   : "fallback";

        var keys = new List<string>
        {
            braveSet ? "Brave (key set)" : "Brave (no key)",
            firecrawlSet ? "FireCrawl (key set)" : "FireCrawl (no key)",
        };

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
