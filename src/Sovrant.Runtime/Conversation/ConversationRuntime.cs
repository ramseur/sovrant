using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Drives an agentic loop: sends messages to the LLM, handles tool use, persists the session,
/// and streams <see cref="RuntimeEvent"/>s to the caller.
/// </summary>
public sealed partial class ConversationRuntime : IConversationRuntime
{
    private const int MaxToolRounds = 20;

    private readonly ISmartRouter _router;
    private readonly IToolExecutor _toolExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ISessionStore _sessionStore;
    private readonly SovrantConfig _config;
    private readonly ILogger<ConversationRuntime> _logger;
    private readonly List<InputMessage> _history = [];
    private readonly string _systemPrompt;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting turn for session '{SessionId}'")]
    private static partial void LogTurnStart(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tool round {Round}/{Max} for session '{SessionId}'")]
    private static partial void LogToolRound(ILogger logger, int round, int max, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Maximum tool rounds ({Max}) reached for session '{SessionId}'")]
    private static partial void LogMaxRoundsReached(ILogger logger, int max, string sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLM request failed: {Error}")]
    private static partial void LogRequestFailed(ILogger logger, string error);

    private string _sessionId = Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public string SessionId => _sessionId;

    public ConversationRuntime(
        ISmartRouter router,
        IToolExecutor toolExecutor,
        IToolRegistry toolRegistry,
        ISessionStore sessionStore,
        SovrantConfig config,
        ILogger<ConversationRuntime> logger)
    {
        _router = router;
        _toolExecutor = toolExecutor;
        _toolRegistry = toolRegistry;
        _sessionStore = sessionStore;
        _config = config;
        _logger = logger;
        _systemPrompt = BuildSystemPrompt();
    }

    /// <inheritdoc/>
    public async Task InitializeSessionAsync(string? sessionId, CancellationToken ct = default)
    {
        if (sessionId is null) return;

        _sessionId = sessionId;
        _history.Clear();

        var entries = await _sessionStore.LoadAsync(sessionId, ct).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            switch (entry.Role)
            {
                case "user" when !string.IsNullOrEmpty(entry.Content):
                    _history.Add(InputMessage.UserText(entry.Content));
                    break;
                case "assistant" when !string.IsNullOrEmpty(entry.Content):
                    _history.Add(InputMessage.AssistantText(entry.Content));
                    break;
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<RuntimeEvent> RunTurnAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        LogTurnStart(_logger, SessionId);

        _history.Add(InputMessage.UserText(userMessage));
        await AppendSessionEntryAsync("user", userMessage, ct).ConfigureAwait(false);

        var round = 0;
        while (round < MaxToolRounds)
        {
            if (round > 0)
                LogToolRound(_logger, round, MaxToolRounds, SessionId);

            var tools = _toolRegistry.GetDefinitions();
            var request = new MessagesRequest(
                _config.Model,
                _config.MaxTokens,
                _history)
            {
                System = _systemPrompt,
                Tools = tools.Count > 0 ? tools : null,
                Stream = true,   // runtime always uses streaming internally
            };

            var provider = await _router.RouteAsync(request, ct).ConfigureAwait(false);
            var started = DateTimeOffset.UtcNow;

            // Collect all streamed events (buffered to avoid yield-in-try/catch restriction)
            var (streamEvents, accumulated) = await CollectStreamEventsAsync(provider, request, ct)
                .ConfigureAwait(false);

            // Yield the collected text/error events to the caller
            foreach (var ev in streamEvents)
                yield return ev;

            var durationMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds;
            await _router.RecordResultAsync(
                provider.Name, accumulated.Success, durationMs, ct).ConfigureAwait(false);

            if (!accumulated.Success)
                yield break;

            // Collect text content for session logging
            var assistantText = string.Join(string.Empty,
                accumulated.Blocks.OfType<OutputContentBlock.TextBlock>().Select(b => b.Text));

            if (!string.IsNullOrEmpty(assistantText))
            {
                await AppendSessionEntryAsync("assistant", assistantText,
                    ct, model: _config.Model,
                    inputTokens: accumulated.InputTokens,
                    outputTokens: accumulated.OutputTokens)
                    .ConfigureAwait(false);
            }

            // Build the assistant turn message (convert OutputContentBlock → InputContentBlock)
            var inputBlocks = ConvertToInputBlocks(accumulated.Blocks);
            _history.Add(new InputMessage("assistant", inputBlocks.Count > 0
                ? inputBlocks
                : [new InputContentBlock.TextBlock(string.Empty)]));

            // Process tool use blocks
            var toolUseBlocks = accumulated.Blocks.OfType<OutputContentBlock.ToolUseBlock>().ToList();
            if (toolUseBlocks.Count == 0 || accumulated.StopReason != "tool_use")
            {
                yield return new RuntimeEvent.TurnComplete(
                    accumulated.StopReason,
                    accumulated.InputTokens,
                    accumulated.OutputTokens);
                yield break;
            }

            // Execute all tool calls and collect results
            var toolResultBlocks = new List<ToolResultContentBlock>();
            foreach (var tu in toolUseBlocks)
            {
                yield return new RuntimeEvent.ToolUseRequested(tu.Id, tu.Name, tu.Input);

                var execResult = await _toolExecutor.ExecuteAsync(tu.Name, tu.Input, ct).ConfigureAwait(false);
                yield return new RuntimeEvent.ToolResult(tu.Id, tu.Name, execResult.Output, execResult.IsError);

                if (!execResult.Success && execResult.Output.Contains("denied", StringComparison.OrdinalIgnoreCase))
                    yield return new RuntimeEvent.PermissionDenied(tu.Name, execResult.Output);

                toolResultBlocks.Add(new ToolResultContentBlock.TextBlock(execResult.Output));

                await AppendSessionEntryAsync("tool_result", execResult.Output, ct,
                    toolName: tu.Name, toolUseId: tu.Id, isError: execResult.IsError)
                    .ConfigureAwait(false);
            }

            // Append tool results message and loop back
            _history.Add(InputMessage.UserToolResult(
                toolUseBlocks[0].Id,
                toolResultBlocks));

            round++;
        }

        LogMaxRoundsReached(_logger, MaxToolRounds, SessionId);
        yield return new RuntimeEvent.RuntimeError($"Maximum tool rounds ({MaxToolRounds}) reached.");
    }

    /// <inheritdoc/>
    public void Reset() => _history.Clear();

    /// <summary>
    /// Reads all SSE stream events from the provider and accumulates them into structured result data.
    /// Returns collected events for the caller to yield, plus an accumulated result struct.
    /// </summary>
    private async Task<(List<RuntimeEvent> Events, StreamAccumulation Accumulated)> CollectStreamEventsAsync(
        Sovrant.Api.Providers.ILlmProvider provider,
        MessagesRequest request,
        CancellationToken ct)
    {
        var events = new List<RuntimeEvent>();
        var textBuilders = new Dictionary<int, StringBuilder>();
        var toolUseBuilders = new Dictionary<int, (string Id, string Name, StringBuilder InputJson)>();
        var stopReason = "end_turn";
        var inputTokens = 0;
        var outputTokens = 0;
        var success = true;

        try
        {
            await foreach (var ev in provider.StreamAsync(request, ct).ConfigureAwait(false))
            {
                switch (ev)
                {
                    case StreamEvent.MessageStart { Message.Usage.InputTokens: var it }:
                        inputTokens = it;
                        break;

                    case StreamEvent.ContentBlockStart { Index: var idx, ContentBlock: OutputContentBlock.TextBlock }:
                        textBuilders[idx] = new StringBuilder();
                        break;

                    case StreamEvent.ContentBlockStart
                    {
                        Index: var idx,
                        ContentBlock: OutputContentBlock.ToolUseBlock tu
                    }:
                        toolUseBuilders[idx] = (tu.Id, tu.Name, new StringBuilder());
                        break;

                    case StreamEvent.ContentBlockDelta { Index: var idx, Delta: ContentBlockDelta.TextDelta td }:
                        if (textBuilders.TryGetValue(idx, out var tsb))
                            tsb.Append(td.Text);
                        events.Add(new RuntimeEvent.TextChunk(td.Text));
                        break;

                    case StreamEvent.ContentBlockDelta { Index: var idx, Delta: ContentBlockDelta.InputJsonDelta ij }:
                        if (toolUseBuilders.TryGetValue(idx, out var tbEntry))
                            tbEntry.InputJson.Append(ij.PartialJson);
                        break;

                    case StreamEvent.MessageDelta { Delta.StopReason: var sr, Usage.OutputTokens: var ot }:
                        if (sr is not null) stopReason = sr;
                        outputTokens = ot;
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            LogRequestFailed(_logger, ex.Message);
            success = false;
            events.Add(new RuntimeEvent.RuntimeError(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            LogRequestFailed(_logger, ex.Message);
            success = false;
            events.Add(new RuntimeEvent.RuntimeError(ex.Message));
        }

        // Build the collected content blocks
        var blocks = new List<OutputContentBlock>();

        foreach (var (_, sb) in textBuilders.OrderBy(kv => kv.Key))
        {
            var text = sb.ToString();
            if (!string.IsNullOrEmpty(text))
                blocks.Add(new OutputContentBlock.TextBlock(text));
        }

        foreach (var (_, (id, name, inputJson)) in toolUseBuilders.OrderBy(kv => kv.Key))
        {
            var inputElement = ParseToolInput(inputJson.ToString());
            blocks.Add(new OutputContentBlock.ToolUseBlock(id, name, inputElement));
        }

        var accumulated = new StreamAccumulation(success, stopReason, inputTokens, outputTokens, blocks);
        return (events, accumulated);
    }

    private static JsonElement ParseToolInput(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return JsonDocument.Parse("{}").RootElement;

        try
        {
            return JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement;
        }
    }

    private static List<InputContentBlock> ConvertToInputBlocks(List<OutputContentBlock> blocks)
    {
        var result = new List<InputContentBlock>(blocks.Count);
        foreach (var block in blocks)
        {
            switch (block)
            {
                case OutputContentBlock.TextBlock t:
                    result.Add(new InputContentBlock.TextBlock(t.Text));
                    break;
                case OutputContentBlock.ToolUseBlock tu:
                    result.Add(new InputContentBlock.ToolUseBlock(tu.Id, tu.Name, tu.Input));
                    break;
                // Thinking blocks are not sent back to the API as input
            }
        }

        return result;
    }

    private async Task AppendSessionEntryAsync(
        string role,
        string content,
        CancellationToken ct,
        string? model = null,
        int inputTokens = 0,
        int outputTokens = 0,
        string? toolName = null,
        string? toolUseId = null,
        bool isError = false)
    {
        var entry = new SessionEntry(
            Id: Guid.NewGuid().ToString("N"),
            Timestamp: DateTimeOffset.UtcNow,
            Role: role,
            Content: content)
        {
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ToolName = toolName,
            ToolUseId = toolUseId,
            IsError = isError,
        };

        await _sessionStore.AppendAsync(SessionId, entry, ct).ConfigureAwait(false);
    }

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder("You are a highly capable agentic AI assistant.");

        if (_config.PermissionMode == Permissions.PermissionMode.Plan)
        {
            sb.Append("\n\nYou are operating in PLAN MODE. ")
              .Append("You may only read files and gather information. ")
              .Append("You must not execute write, edit, delete, or shell operations. ")
              .Append("Describe what you would do instead.");
        }

        return sb.ToString();
    }

    private sealed record StreamAccumulation(
        bool Success,
        string StopReason,
        int InputTokens,
        int OutputTokens,
        List<OutputContentBlock> Blocks);
}
