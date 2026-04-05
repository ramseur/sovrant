using Sovrant.Api.Routing;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Config;

/// <summary>Root configuration for the Sovrant runtime.</summary>
public sealed class SovrantConfig
{
    /// <summary>The model identifier to use for LLM requests.</summary>
    public string Model { get; init; } = "gpt-4o-mini";

    /// <summary>The maximum number of output tokens per request.</summary>
    public int MaxTokens { get; init; } = 8192;

    /// <summary>Controls how the runtime handles potentially destructive tool invocations.</summary>
    public PermissionMode PermissionMode { get; init; } = PermissionMode.Default;

    /// <summary>Controls whether the router uses smart multi-provider routing or a fixed provider.</summary>
    public RouterMode RouterMode { get; init; } = RouterMode.Smart;

    /// <summary>The scoring strategy used when routing to the optimal provider.</summary>
    public RouterStrategy RouterStrategy { get; init; } = RouterStrategy.Balanced;

    /// <summary>Optional base URL override for the LLM API.</summary>
    public Uri? BaseUrl { get; init; }

    /// <summary>Optional API key override. Defaults to the <c>LLM_API_KEY</c> environment variable.</summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Input token count that triggers context auto-compaction (history summarisation).
    /// Set to 0 to disable. Default: 80000. Override via <c>SOVRANT_COMPACT_THRESHOLD</c>.
    /// </summary>
    public int CompactThreshold { get; init; } = 80_000;

    /// <summary>MCP server configurations keyed by server name.</summary>
    public IReadOnlyDictionary<string, McpServerConfig> McpServers { get; init; } =
        new Dictionary<string, McpServerConfig>(StringComparer.Ordinal);

    /// <summary>
    /// LSP (Language Server Protocol) server configurations keyed by language identifier.
    /// Example: <c>{ "csharp": { "command": "omnisharp", "args": ["--languageserver"] } }</c>
    /// </summary>
    public IReadOnlyDictionary<string, LspServerConfigEntry> LspServers { get; init; } =
        new Dictionary<string, LspServerConfigEntry>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Configuration for a single LSP server in settings.json.</summary>
public sealed class LspServerConfigEntry
{
    /// <summary>The command to launch the language server.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Command-line arguments.</summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>Additional environment variables for the process.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>Configuration for a single MCP server.</summary>
public sealed class McpServerConfig
{
    /// <summary>The command to launch the MCP server process.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Command-line arguments for the MCP server process.</summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>Additional environment variables to set for the MCP server process.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Optional OAuth 2.0 configuration for MCP servers that require authorization.
    /// When present, the <c>McpAuth</c> tool can initiate the OAuth flow to obtain
    /// an access token, which is then injected as an environment variable on reconnect.
    /// </summary>
    public McpOAuthConfig? OAuthConfig { get; init; }
}

/// <summary>
/// OAuth 2.0 (Authorization Code + PKCE) configuration for an MCP server.
/// </summary>
public sealed class McpOAuthConfig
{
    /// <summary>The OAuth client ID registered with the authorization server.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// The OAuth client secret. Leave empty for public clients (PKCE-only flows).
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>The authorization endpoint URL — where users are redirected to log in.</summary>
    public Uri? AuthorizationUrl { get; init; }

    /// <summary>The token endpoint URL — where the authorization code is exchanged for tokens.</summary>
    public Uri? TokenUrl { get; init; }

    /// <summary>OAuth scopes to request during authorization.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>
    /// The name of the environment variable to set when reconnecting the MCP server process
    /// after a successful OAuth flow. The access token value is injected under this name.
    /// Example: <c>"GITHUB_TOKEN"</c>.
    /// </summary>
    public string TokenEnvVar { get; init; } = string.Empty;

    /// <summary>
    /// Optional redirect URI override.
    /// Defaults to <c>http://localhost:{SOVRANT_PORT}/v1/mcp/auth/callback</c>.
    /// Must match a URI registered with the OAuth provider.
    /// </summary>
    public Uri? RedirectUri { get; init; }
}
