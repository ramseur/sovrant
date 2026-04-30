using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Capabilities;
using Sovrant.Api.Config;
using Sovrant.Api.Errors;
using Sovrant.Api.OpenAi;
using Sovrant.Api.Types;

namespace Sovrant.Api.Providers;

/// <summary>LLM provider for any OpenAI-compatible chat completions endpoint.</summary>
public class OpenAiCompatProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly IAuthProvider _auth;
    private readonly ILogger _logger;
    private readonly WebSearchOptions? _webSearch;
    private readonly IModelCapabilityRegistry? _capabilities;
    private readonly bool _hasBraveKey;
    private readonly bool _hasFirecrawlKey;

    private static readonly Action<ILogger, Exception?> _logHttpError =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, "HttpError"), "HTTP error calling OpenAI-compat provider.");
    private static readonly Action<ILogger, Exception?> _logJsonError =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, "JsonError"), "JSON error parsing OpenAI-compat response.");
    private static readonly Action<ILogger, Exception?> _logIoError =
        LoggerMessage.Define(LogLevel.Error, new EventId(3, "IoError"), "IO error reading OpenAI-compat response.");
    private static readonly Action<ILogger, string, Exception?> _logSseSkip =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "SseSkip"), "Skipping unparseable SSE data: '{Data}'.");

    /// <summary>Initializes a new instance of <see cref="OpenAiCompatProvider"/>.</summary>
    /// <param name="http">The HTTP client configured for this provider.</param>
    /// <param name="auth">The authentication provider.</param>
    /// <param name="logger">The logger.</param>
    public OpenAiCompatProvider(HttpClient http, IAuthProvider auth, ILogger logger)
        : this(http, auth, logger, webSearch: null, capabilities: null, credentials: null) { }

    /// <summary>
    /// Initializes a new instance with the dependencies required for the
    /// centralised <see cref="NativeWebSearchInjector"/> decision. When any
    /// of the optional arguments is <see langword="null"/> native injection
    /// is disabled and the provider behaves like the legacy ctor.
    /// </summary>
    public OpenAiCompatProvider(
        HttpClient http,
        IAuthProvider auth,
        ILogger logger,
        WebSearchOptions? webSearch,
        IModelCapabilityRegistry? capabilities,
        CredentialConfig? credentials)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _auth = auth;
        _logger = logger;
        _webSearch = webSearch;
        _capabilities = capabilities;
        _hasBraveKey = !string.IsNullOrWhiteSpace(credentials?.BraveApiKey);
        _hasFirecrawlKey = !string.IsNullOrWhiteSpace(credentials?.FirecrawlApiKey);
    }

    /// <inheritdoc/>
    public virtual string Name => "openai-compat";

    /// <inheritdoc/>
    public Uri BaseUrl => _http.BaseAddress ?? new Uri("about:blank");

    /// <inheritdoc/>
    public async Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        try
        {
            var (plan, dialect) = ResolvePlanAndDialect(req.Model);
            var openAiReq = FormatConverter.ToOpenAi(req with { Stream = false }, plan, dialect);
            using var httpReq = await BuildRequestAsync(openAiReq, ct).ConfigureAwait(false);
            using var response = await _http.SendAsync(httpReq, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<MessageResponse>.Fail(await ParseErrorAsync(response, ct).ConfigureAwait(false));
            }
            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var result = await JsonSerializer.DeserializeAsync(
                    stream, SovrantJsonContext.Default.OpenAiChatResponse, ct).ConfigureAwait(false);
                if (result is null)
                {
                    return Result<MessageResponse>.Fail(ApiError.Json(new JsonException("Null OpenAI response.")));
                }
                return Result<MessageResponse>.Ok(FormatConverter.FromOpenAi(result));
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
        var (plan, dialect) = ResolvePlanAndDialect(req.Model);
        var openAiReq = FormatConverter.ToOpenAi(req with { Stream = true }, plan, dialect);
        using var httpReq = await BuildRequestAsync(openAiReq, ct).ConfigureAwait(false);
        using var response = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var err = await ParseErrorAsync(response, ct).ConfigureAwait(false);
            throw new InvalidOperationException(err.Message);
        }

        var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (responseStream.ConfigureAwait(false))
        {
            var toolBuilders = new Dictionary<int, (string Id, string Name, System.Text.StringBuilder Args)>();
            bool sentMessageStart = false;
            bool sentTextBlockStart = false;
            const int textBlockIndex = 0;

            // Captured at finish_reason; usage arrives in a separate trailing chunk when
            // stream_options.include_usage=true (OpenAI sends usage AFTER finish_reason).
            string? capturedStopReason = null;
            OpenAiUsage? capturedUsage = null;

            await foreach (var item in SseParser.Create(responseStream).EnumerateAsync(ct).ConfigureAwait(false))
            {
                if (item.Data is "[DONE]" or "") continue;

                OpenAiSseChunk? chunk = null;
                try { chunk = JsonSerializer.Deserialize(item.Data, SovrantJsonContext.Default.OpenAiSseChunk); }
                catch (JsonException ex) { _logSseSkip(_logger, item.Data, ex); continue; }
                if (chunk is null) continue;

                // Capture usage from whichever chunk carries it (the trailing usage-only chunk).
                if (chunk.Usage is not null)
                    capturedUsage = chunk.Usage;

                var choice = chunk.Choices is { Count: > 0 } ? chunk.Choices[0] : null;

                if (!sentMessageStart)
                {
                    var placeholder = new MessageResponse(
                        chunk.Id, "message", "assistant", [], chunk.Model ?? req.Model, new Usage(0));
                    yield return new StreamEvent.MessageStart(placeholder);
                    sentMessageStart = true;
                }

                var delta = choice?.Delta;
                if (delta is null) continue;

                if (delta.Content is { Length: > 0 } text)
                {
                    if (!sentTextBlockStart)
                    {
                        yield return new StreamEvent.ContentBlockStart(textBlockIndex, new OutputContentBlock.TextBlock(""));
                        sentTextBlockStart = true;
                    }
                    yield return new StreamEvent.ContentBlockDelta(textBlockIndex, new ContentBlockDelta.TextDelta(text));
                }

                if (delta.ToolCalls is { Count: > 0 } tcs)
                {
                    foreach (var tc in tcs)
                    {
                        if (!toolBuilders.TryGetValue(tc.Index, out _))
                        {
                            var entry = (tc.Id ?? string.Empty, tc.Function?.Name ?? string.Empty, new System.Text.StringBuilder());
                            toolBuilders[tc.Index] = entry;
                            int toolIdx = textBlockIndex + 1 + tc.Index;
                            yield return new StreamEvent.ContentBlockStart(toolIdx,
                                new OutputContentBlock.ToolUseBlock(entry.Item1, entry.Item2, default));
                        }
                        if (tc.Function?.Arguments is { } args)
                        {
                            toolBuilders[tc.Index].Args.Append(args);
                            yield return new StreamEvent.ContentBlockDelta(
                                textBlockIndex + 1 + tc.Index,
                                new ContentBlockDelta.InputJsonDelta(args));
                        }
                    }
                }

                if (choice?.FinishReason is not null)
                {
                    if (sentTextBlockStart)
                        yield return new StreamEvent.ContentBlockStop(textBlockIndex);
                    foreach (var kvp in toolBuilders)
                        yield return new StreamEvent.ContentBlockStop(textBlockIndex + 1 + kvp.Key);

                    capturedStopReason = choice.FinishReason switch
                    {
                        "stop" => "end_turn",
                        "tool_calls" => "tool_use",
                        "length" => "max_tokens",
                        var r => r
                    };
                    // Do NOT yield break — OpenAI sends a trailing usage-only chunk after finish_reason.
                    // MessageDelta + MessageStop are emitted after the loop once usage has been captured.
                }
            }

            // Emit final events after the stream ends (usage arrives in trailing chunk).
            if (capturedStopReason is not null)
            {
                var usage = new Usage(
                    InputTokens: capturedUsage?.PromptTokens ?? 0,
                    OutputTokens: capturedUsage?.CompletionTokens ?? 0);
                yield return new StreamEvent.MessageDelta(new MessageDelta(capturedStopReason, null), usage);
                yield return new StreamEvent.MessageStop();
            }
        }
    }

    /// <summary>Builds the HTTP request to the OpenAI-compat endpoint.</summary>
    private protected virtual async Task<HttpRequestMessage> BuildRequestAsync(OpenAiChatRequest openAiReq, CancellationToken ct)
    {
        var apiKey = await _auth.GetAuthHeaderAsync(ct).ConfigureAwait(false);

        // Detect direct OpenAI API — it requires max_completion_tokens for all models
        // and rejects the deprecated max_tokens parameter.
        var effectiveBase = (_auth is Auth.IBaseUrlOverride { BaseUrl: { } ov } ? ov : _http.BaseAddress)?.ToString() ?? string.Empty;
        if (IsDirectOpenAi(effectiveBase) && openAiReq.MaxTokens is { } maxTok)
        {
            openAiReq = openAiReq with { MaxTokens = null, MaxCompletionTokens = maxTok };
        }

        var json = JsonSerializer.Serialize(openAiReq, SovrantJsonContext.Default.OpenAiChatRequest);

        // Use override base URL if available (desktop hot-swap), otherwise fall back to HttpClient.BaseAddress.
        Uri requestUri;
        if (_auth is Auth.IBaseUrlOverride { BaseUrl: { } overrideBase })
        {
            var baseStr = overrideBase.ToString();
            if (!baseStr.EndsWith('/')) baseStr += "/";
            requestUri = new Uri(new Uri(baseStr), "chat/completions");
        }
        else
        {
            requestUri = new Uri("chat/completions", UriKind.Relative);
        }

        var httpReq = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
        return httpReq;
    }

    /// <summary>Returns true when the base URL points to OpenAI's official API (not OpenRouter, etc.).</summary>
    private static bool IsDirectOpenAi(string baseUrl) =>
        baseUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the per-request <see cref="NativeWebSearchPlan"/> and
    /// <see cref="OpenAiDialect"/> using the centralised injector. Returns
    /// <c>(null, Other)</c> when the required dependencies are absent so
    /// the legacy code path remains intact.
    /// </summary>
    private (NativeWebSearchPlan? Plan, OpenAiDialect Dialect) ResolvePlanAndDialect(string model)
    {
        if (_webSearch is null) return (null, OpenAiDialect.Other);

        var effectiveBase = (_auth is IBaseUrlOverride { BaseUrl: { } ov } ? ov : _http.BaseAddress)?.ToString() ?? string.Empty;
        var dialect = OpenAiDialectResolver.Resolve(effectiveBase);
        var plan = NativeWebSearchInjector.Plan(model, _webSearch, _capabilities, _hasBraveKey, _hasFirecrawlKey);
        return (plan, dialect);
    }

    private static async Task<ApiError> ParseErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var status = (int)response.StatusCode;
        bool retryable = status is 429 or >= 500;
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
        catch (JsonException) { /* fall through */ }
        return ApiError.Api(status, null, null, body, retryable);
    }
}
