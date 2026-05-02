using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Capabilities;
using Sovrant.Api.Config;
using Sovrant.Api.Errors;
using Sovrant.Api.OpenAi;
using Sovrant.Api.Types;

namespace Sovrant.Api.Providers;

/// <summary>LLM provider for native messages API endpoints using the provider SSE format.</summary>
public sealed class ProviderApiProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly IAuthProvider _auth;
    private readonly ILogger<ProviderApiProvider> _logger;
    private readonly WebSearchOptions? _webSearch;
    private readonly IModelCapabilityRegistry? _capabilities;
    private readonly IApiKeyResolver? _keyResolver;
    private readonly string? _providerKeyFallback;
    private readonly bool _hasBraveKey;
    private readonly bool _hasFirecrawlKey;

    private static readonly Action<ILogger, Exception?> _logHttpError =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, "HttpError"), "HTTP error calling provider API.");
    private static readonly Action<ILogger, Exception?> _logJsonError =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, "JsonError"), "JSON error parsing provider API response.");
    private static readonly Action<ILogger, Exception?> _logIoError =
        LoggerMessage.Define(LogLevel.Error, new EventId(3, "IoError"), "IO error reading provider API response.");
    private static readonly Action<ILogger, string, Exception?> _logSseWarning =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "SseWarning"), "Failed to deserialize SSE event type '{EventType}'.");

    /// <summary>Initializes a new instance of <see cref="ProviderApiProvider"/>.</summary>
    /// <param name="http">The HTTP client configured for this provider.</param>
    /// <param name="auth">The authentication provider.</param>
    /// <param name="logger">The logger.</param>
    public ProviderApiProvider(HttpClient http, IAuthProvider auth, ILogger<ProviderApiProvider> logger)
        : this(http, auth, logger, webSearch: null, capabilities: null, credentials: null, keyResolver: null) { }

    /// <summary>
    /// Initializes a new instance with the dependencies required for the
    /// centralised <see cref="NativeWebSearchInjector"/> decision. When
    /// <paramref name="webSearch"/> is <see langword="null"/> Anthropic's
    /// <c>web_search_20250305</c> server tool is never injected — the
    /// legacy behaviour. <paramref name="keyResolver"/> enables per-request
    /// resolution of <c>provider.api_key</c> from the credential store; when
    /// null we fall back to <paramref name="auth"/> for the legacy single-key path.
    /// </summary>
    public ProviderApiProvider(
        HttpClient http,
        IAuthProvider auth,
        ILogger<ProviderApiProvider> logger,
        WebSearchOptions? webSearch,
        IModelCapabilityRegistry? capabilities,
        CredentialConfig? credentials,
        IApiKeyResolver? keyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _auth = auth;
        _logger = logger;
        _webSearch = webSearch;
        _capabilities = capabilities;
        _keyResolver = keyResolver;
        _providerKeyFallback = credentials?.ProviderApiKey;
        _hasBraveKey = !string.IsNullOrWhiteSpace(credentials?.BraveApiKey);
        _hasFirecrawlKey = !string.IsNullOrWhiteSpace(credentials?.FirecrawlApiKey);
    }

    /// <inheritdoc/>
    public string Name => "provider-api";

    /// <inheritdoc/>
    public Uri BaseUrl => _http.BaseAddress ?? new Uri("about:blank");

    /// <inheritdoc/>
    public async Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        try
        {
            using var httpReq = await BuildRequestAsync(req with { Stream = false }, ct).ConfigureAwait(false);
            using var response = await _http.SendAsync(httpReq, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<MessageResponse>.Fail(await ParseErrorAsync(response, ct).ConfigureAwait(false));
            }
            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var result = await JsonSerializer.DeserializeAsync(
                    stream, SovrantJsonContext.Default.MessageResponse, ct).ConfigureAwait(false);
                return result is null
                    ? Result<MessageResponse>.Fail(ApiError.Json(new JsonException("Null response.")))
                    : Result<MessageResponse>.Ok(result);
            }
        }
        catch (HttpRequestException ex) { _logHttpError(_logger, ex); return Result<MessageResponse>.Fail(ApiError.Http(ex)); }
        catch (JsonException ex) { _logJsonError(_logger, ex); return Result<MessageResponse>.Fail(ApiError.Json(ex)); }
        catch (IOException ex) { _logIoError(_logger, ex); return Result<MessageResponse>.Fail(ApiError.IoError(ex)); }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<StreamEvent> StreamAsync(
        MessagesRequest req,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        using var httpReq = await BuildRequestAsync(req with { Stream = true }, ct).ConfigureAwait(false);
        using var response = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (responseStream.ConfigureAwait(false))
        {
            await foreach (var item in SseParser.Create(responseStream).EnumerateAsync(ct).ConfigureAwait(false))
            {
                if (item.EventType is "message_stop") { yield break; }
                if (item.EventType is "ping" or "") { continue; }
                StreamEvent? evt = null;
                try { evt = JsonSerializer.Deserialize(item.Data, SovrantJsonContext.Default.StreamEventInfo); }
                catch (JsonException ex) { _logSseWarning(_logger, item.EventType, ex); }
                if (evt is not null) { yield return evt; }
            }
        }
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(MessagesRequest req, CancellationToken ct)
    {
        // Prefer the dedicated provider.api_key resolver (env > store > snapshot)
        // when wired. Falls through to the shared IAuthProvider when the resolver
        // returns nothing or isn't supplied (legacy single-key deployments).
        string? apiKey = null;
        if (_keyResolver is not null)
        {
            apiKey = await _keyResolver.ResolveAsync(
                CredentialKeys.ProviderApiKey, "PROVIDER_API_KEY",
                _providerKeyFallback, ct).ConfigureAwait(false);
        }
        if (string.IsNullOrEmpty(apiKey))
            apiKey = await _auth.GetAuthHeaderAsync(ct).ConfigureAwait(false);
        var plan = ResolvePlan(req.Model);

        // When the plan suppresses the WebSearch function tool, drop it from the
        // outgoing tools list before serialisation so the model can't reach for it.
        var requestForWire = req;
        if (plan?.SuppressFunctionTool == true && req.Tools is { Count: > 0 } tools)
        {
            var filtered = tools.Where(t => !string.Equals(t.Name, "WebSearch", StringComparison.Ordinal)).ToList();
            requestForWire = req with { Tools = filtered.Count > 0 ? filtered : null };
        }

        var json = JsonSerializer.Serialize(requestForWire, SovrantJsonContext.Default.MessagesRequest);

        // When native injection is requested, merge Anthropic's web_search_20250305
        // server tool into the tools array. Anthropic's server tools have a `type`
        // marker that ToolDefinition can't carry, so we patch the JSON tree directly.
        if (plan?.InjectNative == true)
            json = AddAnthropicWebSearchServerTool(json);

        var httpReq = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpReq.Headers.Add("x-api-key", apiKey);
        httpReq.Headers.Add("anthropic-version", "2023-06-01");
        return httpReq;
    }

    /// <summary>
    /// Resolves the per-request plan. Returns <see langword="null"/> when
    /// the optional dependencies aren't wired so the legacy behaviour of
    /// not injecting any server tools is preserved.
    /// </summary>
    private NativeWebSearchPlan? ResolvePlan(string model)
    {
        if (_webSearch is null) return null;
        return NativeWebSearchInjector.Plan(model, _webSearch, _capabilities, _hasBraveKey, _hasFirecrawlKey);
    }

    /// <summary>
    /// Appends <c>{ "type": "web_search_20250305", "name": "web_search" }</c>
    /// to the request's <c>tools</c> array, creating the array when absent.
    /// Returns the original JSON unchanged on parse failure.
    /// </summary>
    internal static string AddAnthropicWebSearchServerTool(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject root) return json;

            var serverTool = new JsonObject
            {
                ["type"] = "web_search_20250305",
                ["name"] = "web_search",
            };

            if (root["tools"] is JsonArray arr)
                arr.Add(serverTool);
            else
                root["tools"] = new JsonArray(serverTool);

            return root.ToJsonString();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static async Task<ApiError> ParseErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var status = (int)response.StatusCode;
        bool retryable = status is 429 or 529 or >= 500;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorEl))
            {
                var errorType = errorEl.TryGetProperty("type", out var t) ? t.GetString() : null;
                var message = errorEl.TryGetProperty("message", out var m) ? m.GetString() : null;
                return ApiError.Api(status, errorType, message, body, retryable);
            }
        }
        catch (JsonException) { /* fall through to raw body */ }
        return ApiError.Api(status, null, null, body, retryable);
    }
}
