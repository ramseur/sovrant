using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Manages OAuth 2.0 Authorization Code + PKCE flows for MCP servers that require
/// user authorization before connecting.
/// </summary>
/// <remarks>
/// Typical flow:
/// <list type="number">
///   <item>Model calls <c>McpAuth</c> tool → <see cref="GenerateAuthorizationUrlAsync"/> returns an auth URL.</item>
///   <item>User visits the URL and grants access.</item>
///   <item>Provider redirects to <c>/v1/mcp/auth/callback?code=...&amp;state=...</c>.</item>
///   <item>The callback route calls <see cref="ExchangeCodeAsync"/>, which stores the token
///         and reconnects the MCP server process with the token injected as an env var.</item>
/// </list>
/// Pending OAuth states expire after 10 minutes to limit the attack window for CSRF.
/// </remarks>
public sealed partial class McpOAuthService
{
    private static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(10);

    private readonly IMcpServerStore _serverStore;
    private readonly ICredentialStore _credentialStore;
    private readonly McpToolRegistrar _toolRegistrar;
    private readonly ILogger<McpOAuthService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly int _serverPort;

    // state → (serverName, codeVerifier, expiry, redirectUri)
    private readonly Dictionary<string, (string ServerName, string CodeVerifier, DateTimeOffset Expiry, string RedirectUri)>
        _pendingStates = new(StringComparer.Ordinal);

    private readonly object _statesLock = new();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "OAuth flow initiated for MCP server '{ServerName}' (state={State})")]
    private static partial void LogOAuthInitiated(ILogger logger, string serverName, string state);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "OAuth token exchanged successfully for MCP server '{ServerName}'")]
    private static partial void LogTokenExchanged(ILogger logger, string serverName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "OAuth callback received with unknown or expired state '{State}'")]
    private static partial void LogUnknownState(ILogger logger, string state);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "OAuth metadata discovered for endpoint '{Endpoint}'")]
    private static partial void LogMetadataDiscovered(ILogger logger, string endpoint);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "OAuth metadata discovery failed for endpoint '{Endpoint}': {Reason}")]
    private static partial void LogDiscoveryFailed(ILogger logger, string endpoint, string reason);

    public McpOAuthService(
        IMcpServerStore serverStore,
        ICredentialStore credentialStore,
        McpToolRegistrar toolRegistrar,
        IHttpClientFactory httpFactory,
        ILogger<McpOAuthService> logger)
    {
        _serverStore = serverStore;
        _credentialStore = credentialStore;
        _toolRegistrar = toolRegistrar;
        _httpFactory = httpFactory;
        _logger = logger;
        _serverPort = int.TryParse(
            Environment.GetEnvironmentVariable("SOVRANT_PORT"), out var p) ? p : 5200;
    }

    /// <summary>
    /// Generates an OAuth 2.0 Authorization Code + PKCE authorization URL for the named MCP server.
    /// </summary>
    /// <param name="serverName">The MCP server name in <see cref="IMcpServerStore"/>.</param>
    /// <param name="redirectUriOverride">
    /// Optional redirect URI override. When provided (e.g. a loopback listener URI for Desktop),
    /// it replaces the default server callback and the catalog/config value.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The authorization URL the user must visit.</returns>
    /// <exception cref="KeyNotFoundException">The server is not in the store.</exception>
    /// <exception cref="InvalidOperationException">The server has no OAuth configuration.</exception>
    public async Task<string> GenerateAuthorizationUrlAsync(
        string serverName,
        Uri? redirectUriOverride = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);

        var serverConfig = await _serverStore.GetAsync(serverName, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"MCP server '{serverName}' is not configured.");

        var oauth = serverConfig.OAuthConfig;
        if (oauth?.AuthorizationUrl is null)
            throw new InvalidOperationException(
                $"MCP server '{serverName}' does not have OAuth configuration.");

        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        var redirectUri = redirectUriOverride
            ?? oauth.RedirectUri
            ?? new Uri($"http://localhost:{_serverPort}/v1/mcp/auth/callback");

        var queryParams = new List<(string Key, string Value)>
        {
            ("response_type", "code"),
            ("client_id", oauth.ClientId),
            ("redirect_uri", redirectUri.ToString()),
            ("state", state),
            ("code_challenge", codeChallenge),
            ("code_challenge_method", "S256"),
        };

        if (oauth.Scopes.Count > 0)
            queryParams.Add(("scope", string.Join(" ", oauth.Scopes)));

        var query = string.Join("&", queryParams.Select(
            kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var baseUrl = oauth.AuthorizationUrl.ToString();
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var authUrl = $"{baseUrl}{separator}{query}";

        lock (_statesLock)
        {
            PurgeExpiredStates();
            _pendingStates[state] = (serverName, codeVerifier, DateTimeOffset.UtcNow.Add(StateExpiry), redirectUri.ToString());
        }

        LogOAuthInitiated(_logger, serverName, state[..8] + "…");
        return authUrl;
    }

    /// <summary>
    /// Exchanges an authorization code for an access token, stores the token securely,
    /// and reconnects the MCP server process with the token injected as an env var.
    /// </summary>
    /// <param name="state">The CSRF state value received in the callback.</param>
    /// <param name="code">The authorization code received in the callback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The server name that was authorized.</returns>
    public async Task<string> ExchangeCodeAsync(string state, string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(state);
        ArgumentException.ThrowIfNullOrEmpty(code);

        string serverName;
        string codeVerifier;
        string redirectUriStr;

        lock (_statesLock)
        {
            if (!_pendingStates.TryGetValue(state, out var entry)
                || entry.Expiry < DateTimeOffset.UtcNow)
            {
                LogUnknownState(_logger, state);
                throw new InvalidOperationException("OAuth state is invalid or expired.");
            }

            serverName = entry.ServerName;
            codeVerifier = entry.CodeVerifier;
            redirectUriStr = entry.RedirectUri;
            _pendingStates.Remove(state);
        }

        var serverConfig = await _serverStore.GetAsync(serverName, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"MCP server '{serverName}' is not configured.");

        var oauth = serverConfig.OAuthConfig!;
        var redirectUri = new Uri(redirectUriStr);

        // Client secret (if any) lives encrypted in the credential store, not the
        // server-config record. Confidential clients store it under
        // "mcp.{name}.client_secret"; PKCE-only public clients omit it entirely.
        var clientSecret = await _credentialStore.RetrieveAsync(
            $"mcp.{serverName}.client_secret", ct).ConfigureAwait(false);

        // Exchange code for token at the token endpoint.
        var tokenResponse = await ExchangeCodeForTokenAsync(
            oauth, code, codeVerifier, redirectUri, clientSecret, ct).ConfigureAwait(false);

        // Persist the token (AES-GCM encrypted).
        await _credentialStore.StoreAsync(
            $"mcp.{serverName}.access_token", tokenResponse.AccessToken, ct).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            await _credentialStore.StoreAsync(
                $"mcp.{serverName}.refresh_token", tokenResponse.RefreshToken, ct).ConfigureAwait(false);
        }

        LogTokenExchanged(_logger, serverName);

        // Calculate token expiry for refresh scheduling.
        DateTimeOffset? expiresAt = tokenResponse.ExpiresIn.HasValue
            ? DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn.Value)
            : null;

        // Reconnect the server with the token injected appropriately.
        if (serverConfig.Url is not null)
        {
            // HTTP transport: inject token as Authorization: Bearer header.
            var headers = new Dictionary<string, string>(serverConfig.Headers, StringComparer.Ordinal)
            {
                ["Authorization"] = $"Bearer {tokenResponse.AccessToken}",
            };
            var updatedConfig = new McpServerConfig
            {
                Url = serverConfig.Url,
                Headers = headers,
                OAuthConfig = serverConfig.OAuthConfig,
                TokenExpiresAt = expiresAt,
            };
            await _serverStore.UpsertAsync(serverName, updatedConfig, ct).ConfigureAwait(false);
            await _toolRegistrar.ReconnectServerAsync(serverName, updatedConfig, ct).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(oauth.TokenEnvVar))
        {
            // stdio transport: inject token as environment variable.
            var env = new Dictionary<string, string>(serverConfig.Env, StringComparer.Ordinal)
            {
                [oauth.TokenEnvVar] = tokenResponse.AccessToken,
            };
            var updatedConfig = new McpServerConfig
            {
                Command = serverConfig.Command,
                Args = serverConfig.Args,
                Env = env,
                OAuthConfig = serverConfig.OAuthConfig,
                TokenExpiresAt = expiresAt,
            };
            await _serverStore.UpsertAsync(serverName, updatedConfig, ct).ConfigureAwait(false);
            await _toolRegistrar.ReconnectServerAsync(serverName, updatedConfig, ct).ConfigureAwait(false);
        }

        return serverName;
    }

    // ── Discovery & token status ─────────────────────────────────────────────

    /// <summary>
    /// Attempts to discover OAuth 2.0 server metadata from the MCP server's well-known endpoint
    /// (<c>{endpoint}/.well-known/oauth-authorization-server</c>) per RFC 8414.
    /// Returns <c>null</c> if discovery is not supported or fails.
    /// </summary>
    public async Task<McpOAuthDiscovery?> DiscoverMetadataAsync(Uri mcpEndpoint, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mcpEndpoint);
        var discoveryUrl = new Uri(
            mcpEndpoint.GetLeftPart(UriPartial.Authority) + "/.well-known/oauth-authorization-server");

        try
        {
            using var http = _httpFactory.CreateClient("McpOAuth");
            using var response = await http.GetAsync(discoveryUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var doc = await response.Content
                .ReadFromJsonAsync<JsonElement>(cancellationToken: ct)
                .ConfigureAwait(false);

            var authEndpoint = doc.TryGetProperty("authorization_endpoint", out var ae)
                ? ae.GetString() : null;
            var tokenEndpoint = doc.TryGetProperty("token_endpoint", out var te)
                ? te.GetString() : null;
            var scopesSupported = doc.TryGetProperty("scopes_supported", out var ss)
                ? ss.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                : [];

            if (authEndpoint is null || tokenEndpoint is null) return null;

            LogMetadataDiscovered(_logger, mcpEndpoint.AbsoluteUri);
            return new McpOAuthDiscovery(
                new Uri(authEndpoint),
                new Uri(tokenEndpoint),
                scopesSupported);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            LogDiscoveryFailed(_logger, mcpEndpoint.AbsoluteUri, ex.Message);
            return null;
        }
    }

    /// <summary>Returns true if the named server has a stored (and not yet expired) access token.</summary>
    public async Task<bool> HasTokenAsync(string serverName, CancellationToken ct = default)
    {
        var token = await _credentialStore.RetrieveAsync($"mcp.{serverName}.access_token", ct)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(token)) return false;

        var config = await _serverStore.GetAsync(serverName, ct).ConfigureAwait(false);
        if (config?.TokenExpiresAt is { } expiry)
            return expiry > DateTimeOffset.UtcNow.AddSeconds(30); // 30s grace

        return true;
    }

    // ── PKCE helpers ─────────────────────────────────────────────────────────

    private static string GenerateCodeVerifier()
    {
        // RFC 7636 §4.1 — 43–128 characters, Base64URL alphabet.
        var bytes = new byte[48]; // 48 bytes → 64 Base64URL chars
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    // ── Token endpoint ───────────────────────────────────────────────────────

    private async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(
        McpOAuthConfig oauth,
        string code,
        string codeVerifier,
        Uri redirectUri,
        string? clientSecret,
        CancellationToken ct)
    {
        var formFields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri.ToString(),
            ["client_id"] = oauth.ClientId,
            ["code_verifier"] = codeVerifier,
        };

        if (!string.IsNullOrWhiteSpace(clientSecret))
            formFields["client_secret"] = clientSecret;

        using var http = _httpFactory.CreateClient("McpOAuth");
        using var formContent = new FormUrlEncodedContent(formFields);
        using var response = await http.PostAsync(oauth.TokenUrl, formContent, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(ct)
            .ConfigureAwait(false);

        return token ?? throw new InvalidOperationException("Token endpoint returned an empty response.");
    }

    private void PurgeExpiredStates()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _pendingStates
            .Where(kv => kv.Value.Expiry < now)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expired)
            _pendingStates.Remove(key);
    }

    // ── Token response DTO ───────────────────────────────────────────────────

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = "Bearer";

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; init; }
    }
}

/// <summary>OAuth 2.0 server metadata discovered via RFC 8414 well-known endpoint.</summary>
public sealed record McpOAuthDiscovery(
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    IReadOnlyList<string> ScopesSupported);
