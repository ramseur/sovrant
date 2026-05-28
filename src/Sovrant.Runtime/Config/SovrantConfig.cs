using Sovrant.Api.Config;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.TrustBoundary;

namespace Sovrant.Runtime.Config;

/// <summary>Root configuration for the Sovrant runtime.</summary>
public sealed class SovrantConfig
{
    /// <summary>The model identifier to use for LLM requests.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>The maximum number of output tokens per request.</summary>
    public int MaxTokens { get; set; } = 8192;

    /// <summary>Controls how the runtime handles potentially destructive tool invocations.</summary>
    public PermissionMode PermissionMode { get; set; } = PermissionMode.Default;

    /// <summary>Optional base URL override for the LLM API.</summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>Optional API key override. Defaults to the <c>LLM_API_KEY</c> environment variable.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Input token count that triggers context auto-compaction (history summarisation).
    /// Set to 0 to disable. Default: 80000. Override via <c>SOVRANT_COMPACT_THRESHOLD</c>.
    /// </summary>
    public int CompactThreshold { get; init; } = 80_000;

    /// <summary>
    /// Maps capability level names (<c>high</c>, <c>standard</c>, <c>fast</c>) to specific model
    /// identifiers for this deployment. When an agent template's <c>RecommendedLevel</c> matches
    /// a key here, the corresponding model is used instead of the default <see cref="Model"/>.
    /// Example: <c>{ "high": "claude-opus-4-6", "standard": "gpt-4o", "fast": "gpt-4o-mini" }</c>
    /// </summary>
    public IReadOnlyDictionary<string, string> ModelLevels { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Trust boundary configuration (sanitization, ethical harness, intent verification).</summary>
    public TrustBoundaryConfig TrustBoundary { get; init; } = new();

    /// <summary>
    /// Per-session override of the web-search backend. When set, takes
    /// precedence over the globally resolved <c>WebSearchOptions</c> for
    /// the session that owns this config copy. Used by the
    /// <c>/websearch</c> slash command so a one-off change doesn't stomp
    /// the saved default.
    /// </summary>
    public WebSearchBackend? WebSearchOverride { get; set; }

    /// <summary>
    /// Returns a copy of this config with <see cref="Model"/> replaced by <paramref name="model"/>.
    /// All other properties are preserved.
    /// </summary>
    internal SovrantConfig WithModel(string model) => new()
    {
        Model = model,
        MaxTokens = MaxTokens,
        PermissionMode = PermissionMode,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        CompactThreshold = CompactThreshold,
        ModelLevels = ModelLevels,
        TrustBoundary = TrustBoundary,
        WebSearchOverride = WebSearchOverride,
    };
}

/// <summary>Configuration for a single MCP server.</summary>
/// <remarks>
/// Two transports are supported. When <see cref="Url"/> is set, the HTTP/SSE
/// transport is used and <see cref="Headers"/> are attached to every request
/// (e.g. <c>Authorization: Bearer …</c>). Otherwise the stdio transport is used
/// and the server is spawned via <see cref="Command"/> + <see cref="Args"/>.
/// </remarks>
public sealed class McpServerConfig
{
    /// <summary>The command to launch the MCP server process. Ignored when <see cref="Url"/> is set.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Command-line arguments for the MCP server process.</summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>Additional environment variables to set for the MCP server process.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Endpoint URL for an HTTP/SSE-based MCP server. When set, the HTTP transport is
    /// used and <see cref="Command"/>/<see cref="Args"/> are ignored.
    /// </summary>
    public Uri? Url { get; init; }

    /// <summary>
    /// HTTP headers attached to every request when <see cref="Url"/> is set. Use this
    /// for bearer tokens, API keys, etc. — values that contain "key", "secret", "token",
    /// or "auth" are masked in UI surfaces.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Optional OAuth 2.0 configuration for MCP servers that require authorization.
    /// When present, the <c>McpAuth</c> tool can initiate the OAuth flow to obtain
    /// an access token, which is then injected as an environment variable on reconnect.
    /// </summary>
    public McpOAuthConfig? OAuthConfig { get; init; }

    /// <summary>
    /// When the current OAuth access token expires. Null if no token has been obtained
    /// or if the token endpoint did not return an <c>expires_in</c> value.
    /// </summary>
    public DateTimeOffset? TokenExpiresAt { get; init; }
}

/// <summary>
/// OAuth 2.0 (Authorization Code + PKCE) configuration for an MCP server.
/// </summary>
public sealed class McpOAuthConfig
{
    /// <summary>The OAuth client ID registered with the authorization server.</summary>
    public string ClientId { get; init; } = string.Empty;

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
