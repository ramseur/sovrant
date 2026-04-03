using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Errors;
using Sovrant.Api.Types;

namespace Sovrant.Api.Providers;

/// <summary>LLM provider for proprietary API endpoints using Anthropic-compatible SSE format.</summary>
public sealed class ProviderApiProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly IAuthProvider _auth;
    private readonly ILogger<ProviderApiProvider> _logger;

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
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _auth = auth;
        _logger = logger;
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
        var apiKey = await _auth.GetAuthHeaderAsync(ct).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(req, SovrantJsonContext.Default.MessagesRequest);
        var httpReq = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpReq.Headers.Add("x-api-key", apiKey);
        httpReq.Headers.Add("anthropic-version", "2023-06-01");
        return httpReq;
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
