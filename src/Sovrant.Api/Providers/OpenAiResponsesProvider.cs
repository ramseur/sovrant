using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Errors;
using Sovrant.Api.OpenAi;
using Sovrant.Api.Types;

namespace Sovrant.Api.Providers;

/// <summary>
/// LLM provider that uses the OpenAI Responses API (<c>POST /v1/responses</c>).
/// Automatically injects <c>web_search_preview</c> alongside any function tools so the
/// model can search the web without requiring a Brave or FireCrawl API key.
/// Activated when <c>LLM_WEB_SEARCH=true</c>.
/// </summary>
public sealed class OpenAiResponsesProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly IAuthProvider _auth;
    private readonly ILogger _logger;

    private static readonly Action<ILogger, Exception?> _logHttpError =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, "HttpError"), "HTTP error calling OpenAI Responses API.");
    private static readonly Action<ILogger, Exception?> _logJsonError =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, "JsonError"), "JSON error parsing OpenAI Responses API response.");
    private static readonly Action<ILogger, Exception?> _logIoError =
        LoggerMessage.Define(LogLevel.Error, new EventId(3, "IoError"), "IO error reading OpenAI Responses API response.");
    private static readonly Action<ILogger, string, Exception?> _logSseSkip =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "SseSkip"), "Skipping unparseable Responses API SSE data: '{Data}'.");

    /// <summary>Initializes a new instance of <see cref="OpenAiResponsesProvider"/>.</summary>
    /// <param name="http">HTTP client pointed at the OpenAI base URL.</param>
    /// <param name="auth">Authentication provider.</param>
    /// <param name="logger">Logger.</param>
    public OpenAiResponsesProvider(HttpClient http, IAuthProvider auth, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _auth = auth;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "openai-responses";

    /// <inheritdoc/>
    public Uri BaseUrl => _http.BaseAddress ?? new Uri("about:blank");

    /// <inheritdoc/>
    public async Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        try
        {
            var responsesReq = ResponsesFormatConverter.ToResponses(req with { Stream = false });
            using var httpReq = await BuildRequestAsync(responsesReq, ct).ConfigureAwait(false);
            using var response = await _http.SendAsync(httpReq, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return Result<MessageResponse>.Fail(await ParseErrorAsync(response, ct).ConfigureAwait(false));

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var result = await JsonSerializer.DeserializeAsync(
                    stream, SovrantJsonContext.Default.OpenAiResponsesResponse, ct).ConfigureAwait(false);
                if (result is null)
                    return Result<MessageResponse>.Fail(ApiError.Json(new JsonException("Null Responses API response.")));
                return Result<MessageResponse>.Ok(ResponsesFormatConverter.FromResponses(result));
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

        var responsesReq = ResponsesFormatConverter.ToResponses(req with { Stream = true });
        using var httpReq = await BuildRequestAsync(responsesReq, ct).ConfigureAwait(false);
        using var response = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var err = await ParseErrorAsync(response, ct).ConfigureAwait(false);
            throw new InvalidOperationException(err.Message);
        }

        var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (responseStream.ConfigureAwait(false))
        {
            // output_index → (id, callId, name, args accumulator)
            var toolBuilders = new Dictionary<int, (string Id, string CallId, string Name, StringBuilder Args)>();
            bool sentMessageStart = false;
            bool sentTextBlockStart = false;
            const int textBlockIndex = 0;

            await foreach (var item in SseParser.Create(responseStream).EnumerateAsync(ct).ConfigureAwait(false))
            {
                if (item.Data is "[DONE]" or "") continue;

                if (!sentMessageStart)
                {
                    yield return new StreamEvent.MessageStart(
                        new MessageResponse(string.Empty, "message", "assistant", [], req.Model, new Usage(0)));
                    sentMessageStart = true;
                }

                switch (item.EventType)
                {
                    case "response.output_text.delta":
                    {
                        OpenAiResponsesTextDeltaChunk? chunk = null;
                        try { chunk = JsonSerializer.Deserialize(item.Data, SovrantJsonContext.Default.OpenAiResponsesTextDeltaChunk); }
                        catch (JsonException ex) { _logSseSkip(_logger, item.Data, ex); continue; }
                        if (chunk?.Delta is { Length: > 0 } text)
                        {
                            if (!sentTextBlockStart)
                            {
                                yield return new StreamEvent.ContentBlockStart(textBlockIndex, new OutputContentBlock.TextBlock(""));
                                sentTextBlockStart = true;
                            }
                            yield return new StreamEvent.ContentBlockDelta(textBlockIndex, new ContentBlockDelta.TextDelta(text));
                        }
                        break;
                    }

                    case "response.output_item.added":
                    {
                        OpenAiResponsesOutputItemChunk? chunk = null;
                        try { chunk = JsonSerializer.Deserialize(item.Data, SovrantJsonContext.Default.OpenAiResponsesOutputItemChunk); }
                        catch (JsonException ex) { _logSseSkip(_logger, item.Data, ex); continue; }
                        if (chunk?.Item is { } addedItem &&
                            string.Equals(addedItem.Type, "function_call", StringComparison.Ordinal) &&
                            addedItem.Name is not null)
                        {
                            var callId = addedItem.CallId ?? addedItem.Id ?? string.Empty;
                            toolBuilders[chunk.OutputIndex] = (addedItem.Id ?? string.Empty, callId, addedItem.Name, new StringBuilder());
                            int toolIdx = textBlockIndex + 1 + chunk.OutputIndex;
                            yield return new StreamEvent.ContentBlockStart(toolIdx,
                                new OutputContentBlock.ToolUseBlock(callId, addedItem.Name, default));
                        }
                        break;
                    }

                    case "response.function_call_arguments.delta":
                    {
                        OpenAiResponsesFunctionCallDeltaChunk? chunk = null;
                        try { chunk = JsonSerializer.Deserialize(item.Data, SovrantJsonContext.Default.OpenAiResponsesFunctionCallDeltaChunk); }
                        catch (JsonException ex) { _logSseSkip(_logger, item.Data, ex); continue; }
                        if (chunk?.Delta is { } delta && toolBuilders.TryGetValue(chunk.OutputIndex, out var tb))
                        {
                            tb.Args.Append(delta);
                            yield return new StreamEvent.ContentBlockDelta(
                                textBlockIndex + 1 + chunk.OutputIndex,
                                new ContentBlockDelta.InputJsonDelta(delta));
                        }
                        break;
                    }

                    case "response.output_item.done":
                    {
                        OpenAiResponsesOutputItemChunk? chunk = null;
                        try { chunk = JsonSerializer.Deserialize(item.Data, SovrantJsonContext.Default.OpenAiResponsesOutputItemChunk); }
                        catch (JsonException ex) { _logSseSkip(_logger, item.Data, ex); continue; }
                        if (chunk is not null && toolBuilders.ContainsKey(chunk.OutputIndex))
                            yield return new StreamEvent.ContentBlockStop(textBlockIndex + 1 + chunk.OutputIndex);
                        break;
                    }

                    case "response.completed":
                    {
                        OpenAiResponsesCompletedChunk? chunk = null;
                        try { chunk = JsonSerializer.Deserialize(item.Data, SovrantJsonContext.Default.OpenAiResponsesCompletedChunk); }
                        catch (JsonException ex) { _logSseSkip(_logger, item.Data, ex); continue; }

                        if (sentTextBlockStart)
                            yield return new StreamEvent.ContentBlockStop(textBlockIndex);

                        var stopReason = toolBuilders.Count > 0 ? "tool_use" : "end_turn";
                        var usage = new Usage(
                            InputTokens: chunk?.Response?.Usage?.InputTokens ?? 0,
                            OutputTokens: chunk?.Response?.Usage?.OutputTokens ?? 0);
                        yield return new StreamEvent.MessageDelta(new MessageDelta(stopReason, null), usage);
                        yield return new StreamEvent.MessageStop();
                        yield break;
                    }
                }
            }

            // Stream ended without response.completed — emit final events as fallback
            if (sentMessageStart)
            {
                if (sentTextBlockStart)
                    yield return new StreamEvent.ContentBlockStop(textBlockIndex);
                var stopReason = toolBuilders.Count > 0 ? "tool_use" : "end_turn";
                yield return new StreamEvent.MessageDelta(new MessageDelta(stopReason, null), new Usage(0));
                yield return new StreamEvent.MessageStop();
            }
        }
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(OpenAiResponsesRequest req, CancellationToken ct)
    {
        var apiKey = await _auth.GetAuthHeaderAsync(ct).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(req, SovrantJsonContext.Default.OpenAiResponsesRequest);
        var httpReq = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(apiKey))
            httpReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return httpReq;
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
