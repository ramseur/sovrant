using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Hooks;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Session;
using Sovrant.Api.Capabilities;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Drives an agentic loop: sends messages to the LLM, handles tool use, persists the session,
/// and streams <see cref="RuntimeEvent"/>s to the caller.
/// </summary>
public sealed partial class ConversationRuntime : IConversationRuntime
{
    private const int MaxToolRounds = 20;
    private const int MaxRepeatedToolCalls = 3;

    private readonly ISmartRouter _router;
    private readonly IToolExecutor _toolExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ISessionStore _sessionStore;
    private readonly SovrantConfig _config;
    private readonly ILogger<ConversationRuntime> _logger;
    private readonly IHookRunner _hookRunner;
    private readonly MemoryInjector? _memoryInjector;
    private readonly IModelCapabilityRegistry? _capabilityRegistry;
    private readonly Governance.IIntentGate? _intentGate;
    private readonly Metrics.CostModelLoggerFacade? _costFacade;
    private readonly List<InputMessage> _history = [];
    private string _systemPrompt;
    /// <summary>Once true, all subsequent turns expose tools (session used tools at least once).</summary>
    private bool _sessionHasUsedTools;

    [LoggerMessage(Level = LogLevel.Information, Message = "Turn started: session={SessionId}, model={Model}")]
    private static partial void LogTurnStart(ILogger logger, string sessionId, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Turn complete: duration_ms={DurationMs}, tokens_in={TokensIn}, tokens_out={TokensOut}")]
    private static partial void LogTurnComplete(ILogger logger, long durationMs, int tokensIn, int tokensOut);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tool round {Round}/{Max} for session '{SessionId}'")]
    private static partial void LogToolRound(ILogger logger, int round, int max, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Maximum tool rounds ({Max}) reached for session '{SessionId}'")]
    private static partial void LogMaxRoundsReached(ILogger logger, int max, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool call loop detected: '{ToolName}' called {Count} times consecutively for session '{SessionId}' — breaking loop")]
    private static partial void LogToolCallLoop(ILogger logger, string toolName, int count, string sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tool dispatched: tool={ToolName}")]
    private static partial void LogToolDispatched(ILogger logger, string toolName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tool complete: tool={ToolName}, duration_ms={DurationMs}, is_error={IsError}")]
    private static partial void LogToolResult(ILogger logger, string toolName, long durationMs, bool isError);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLM request failed: {Error}")]
    private static partial void LogRequestFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM request failed (attempt {Attempt}/{Max}): {Error} — retrying in {DelayMs} ms")]
    private static partial void LogRetry(ILogger logger, int attempt, int max, string error, int delayMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Context compacted for session '{SessionId}': {Kept} messages summarized at {Tokens} input tokens")]
    private static partial void LogCompacted(ILogger logger, string sessionId, int kept, int tokens);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to inject multi-layered memory into system prompt")]
    private static partial void LogMemoryInjectionFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Provider selected: provider={Provider}")]
    private static partial void LogProviderSelected(ILogger logger, string provider);

    private string _sessionId = Guid.NewGuid().ToString("N");
    private string? _ownerUserId;

    /// <inheritdoc/>
    public string SessionId => _sessionId;

    public ConversationRuntime(
        ISmartRouter router,
        IToolExecutor toolExecutor,
        IToolRegistry toolRegistry,
        ISessionStore sessionStore,
        SovrantConfig config,
        ILogger<ConversationRuntime> logger,
        IHookRunner? hookRunner = null,
        MemoryInjector? memoryInjector = null,
        string? systemPromptOverride = null,
        IModelCapabilityRegistry? capabilityRegistry = null,
        Governance.IIntentGate? intentGate = null,
        Metrics.CostModelLoggerFacade? costFacade = null)
    {
        _router = router;
        _toolExecutor = toolExecutor;
        _toolRegistry = toolRegistry;
        _sessionStore = sessionStore;
        _config = config;
        _logger = logger;
        _hookRunner = hookRunner ?? NullHookRunner.Instance;
        _memoryInjector = memoryInjector;
        _capabilityRegistry = capabilityRegistry;
        _intentGate = intentGate;
        _costFacade = costFacade;
        _systemPrompt = systemPromptOverride ?? BuildSystemPrompt();
    }

    /// <inheritdoc/>
    public async Task InitializeSessionAsync(
        string? sessionId,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        // Even for transient (null sessionId) runtimes we still want to stamp
        // the caller's user id on the auto-generated session row so Phase 38
        // ownership is enforced for stateless one-shot requests as well.
        _ownerUserId = ownerUserId;

        if (sessionId is null) return;

        _sessionId = sessionId;
        _history.Clear();

        var entries = await _sessionStore.LoadAsync(sessionId, ownerUserId, ct).ConfigureAwait(false);
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

        // Inject multi-layered memory (session summaries, learned patterns, instincts)
        if (_memoryInjector is not null)
        {
            try
            {
                var project = Directory.GetCurrentDirectory();
                var memorySection = await _memoryInjector.BuildMemorySectionAsync(project, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(memorySection))
                    _systemPrompt += memorySection;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Memory injection is best-effort — never block session initialization.
                LogMemoryInjectionFailed(_logger, ex);
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<RuntimeEvent> RunTurnAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var turnSw = Stopwatch.StartNew();
        LogTurnStart(_logger, SessionId, _config.Model);

        // Ambient logging scope — session_id and model appear on every log line within this turn.
        using var sessionScope = _logger.BeginScope(
            new Dictionary<string, object?> { ["session_id"] = SessionId, ["model"] = _config.Model });

        _history.Add(InputMessage.UserText(userMessage));
        await AppendSessionEntryAsync("user", userMessage, ct).ConfigureAwait(false);

        var round = 0;
        var consecutiveToolCalls = new Dictionary<string, int>(StringComparer.Ordinal);
        while (round < MaxToolRounds)
        {
            using var turnScope = _logger.BeginScope(
                new Dictionary<string, object?> { ["turn"] = round });

            if (round > 0)
                LogToolRound(_logger, round, MaxToolRounds, SessionId);

            var allTools = _toolRegistry.GetDefinitions();
            // Phase 59a — On the first round, use the semantic intent gate (if
            // available) to decide whether tools should be exposed. Falls back
            // to the legacy keyword matcher when no gate is registered.
            // Once a session has used tools, always expose them on subsequent turns
            // so the LLM can continue multi-step work.
            IReadOnlyList<ToolDefinition> tools;
            if (round > 0 || _sessionHasUsedTools)
            {
                // Tool-use rounds and sessions that already used tools always get tools.
                tools = FilterToolsForModel(allTools);
            }
            else if (round == 0)
            {
                if (_intentGate is not null)
                {
                    var gateResult = await _intentGate.ClassifyAsync(
                        userMessage, _history.Count / 2, ct).ConfigureAwait(false);

                    if (gateResult.NeedsClarification)
                        yield return new RuntimeEvent.ClarificationNeeded(
                            gateResult.SuggestedClarification ?? "Could you clarify what you'd like me to do?");

                    tools = gateResult.RequiresTools
                        ? FilterToolsForModel(allTools)
                        : [];
                }
                else
                {
                    // Legacy fallback — will be removed once IIntentGate is fully validated.
                    tools = LooksLikeToolRequest(userMessage)
                        ? FilterToolsForModel(allTools)
                        : [];
                }
            }
            else
            {
                tools = FilterToolsForModel(allTools);
            }
            var request = new MessagesRequest(
                _config.Model,
                CapMaxTokens(_config.Model, _config.MaxTokens),
                _history)
            {
                System = _systemPrompt,
                Tools = tools.Count > 0 ? tools : null,
                Stream = true,   // runtime always uses streaming internally
            };

            // RouteAsync can throw if all providers are unhealthy.
            // Catch outside yield (yield-in-try/catch is not permitted in iterators).
            Sovrant.Api.Providers.ILlmProvider? provider = null;
            string? routingError = null;
            try
            {
                if (_router.IntentRoutingEnabled)
                {
                    var decision = await _router.RouteWithIntentAsync(request, ct).ConfigureAwait(false);
                    provider = decision.Provider;
                    if (decision.ResolvedModel is not null)
                        request = request with { Model = decision.ResolvedModel };
                }
                else
                {
                    provider = await _router.RouteAsync(request, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException ex) { routingError = ex.Message; }

            if (routingError is not null)
            {
                yield return new RuntimeEvent.RuntimeError(routingError);
                yield break;
            }

            if (provider is null)
            {
                yield return new RuntimeEvent.RuntimeError("No provider available after routing.");
                yield break;
            }
            var resolvedProvider = provider;
            LogProviderSelected(_logger, resolvedProvider.Name);

            var llmSw = Stopwatch.StartNew();

            // Collect all streamed events (buffered to avoid yield-in-try/catch restriction)
            var (streamEvents, accumulated) = await CollectStreamEventsAsync(resolvedProvider, request, ct)
                .ConfigureAwait(false);

            llmSw.Stop();

            // Yield the collected text/error events to the caller
            foreach (var ev in streamEvents)
                yield return ev;

            await _router.RecordResultAsync(
                resolvedProvider.Name, accumulated.Success, llmSw.Elapsed.TotalMilliseconds, ct).ConfigureAwait(false);

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

            // Compact history if approaching token limit
            if (accumulated.InputTokens > 0)
                await MaybeCompactHistoryAsync(accumulated.InputTokens, ct).ConfigureAwait(false);

            // Process tool use blocks
            var toolUseBlocks = accumulated.Blocks.OfType<OutputContentBlock.ToolUseBlock>().ToList();

            // Detect tool-call hallucination loops: if the model keeps calling the same tool
            // repeatedly (common with some models like Gemma 4), break the loop.
            if (toolUseBlocks.Count > 0 && accumulated.StopReason == "tool_use")
            {
                _sessionHasUsedTools = true;
                var currentTools = new HashSet<string>(toolUseBlocks.Select(t => t.Name), StringComparer.Ordinal);
                bool loopDetected = false;
                foreach (var name in currentTools)
                {
                    consecutiveToolCalls.TryGetValue(name, out var count);
                    consecutiveToolCalls[name] = count + 1;
                    if (count + 1 >= MaxRepeatedToolCalls)
                    {
                        LogToolCallLoop(_logger, name, count + 1, SessionId);
                        loopDetected = true;
                    }
                }
                // Clear counters for tools NOT called this round.
                foreach (var key in consecutiveToolCalls.Keys.Except(currentTools).ToList())
                    consecutiveToolCalls.Remove(key);

                if (loopDetected)
                {
                    // Break the loop — treat as end_turn with accumulated text.
                    turnSw.Stop();
                    LogTurnComplete(_logger, turnSw.ElapsedMilliseconds, accumulated.InputTokens, accumulated.OutputTokens);
                    yield return new RuntimeEvent.TurnComplete(
                        "end_turn", accumulated.InputTokens, accumulated.OutputTokens);
                    var costEvt1 = RecordCostIfEnabled(accumulated.InputTokens, accumulated.OutputTokens);
                    if (costEvt1 is not null) yield return costEvt1;
                    yield break;
                }
            }
            else
            {
                consecutiveToolCalls.Clear();
            }

            if (toolUseBlocks.Count == 0 || accumulated.StopReason != "tool_use")
            {
                turnSw.Stop();
                LogTurnComplete(_logger, turnSw.ElapsedMilliseconds, accumulated.InputTokens, accumulated.OutputTokens);
                yield return new RuntimeEvent.TurnComplete(
                    accumulated.StopReason,
                    accumulated.InputTokens,
                    accumulated.OutputTokens);
                var costEvt2 = RecordCostIfEnabled(accumulated.InputTokens, accumulated.OutputTokens);
                if (costEvt2 is not null) yield return costEvt2;
                // Stop hook is fire-and-forget — agent does not wait for it.
                _ = _hookRunner.RunAsync(
                    HookEvent.Stop,
                    new HookContext(HookEvent.Stop, SessionId, StopReason: accumulated.StopReason),
                    CancellationToken.None)
                    .ContinueWith(static t => { if (t.IsFaulted) _ = t.Exception; }, TaskScheduler.Default);
                yield break;
            }

            // Execute all tool calls and collect results
            // Each tool call needs its own separate tool-result entry (required by OpenAI and Anthropic).
            var toolResultPairs = new List<(ToolResultContentBlock Content, bool IsError)>(toolUseBlocks.Count);
            foreach (var tu in toolUseBlocks)
            {
                // PreToolUse: blocking in strict profile — abort tool if any hook exits non-zero.
                var preCtx = new HookContext(HookEvent.PreToolUse, SessionId,
                    ToolName: tu.Name, ToolInput: tu.Input.ToString());
                var allowed = await _hookRunner.RunAsync(HookEvent.PreToolUse, preCtx, ct).ConfigureAwait(false);
                if (!allowed)
                {
                    const string blockedMsg = "Blocked by PreToolUse hook.";
                    yield return new RuntimeEvent.PermissionDenied(tu.Name, blockedMsg);
                    toolResultPairs.Add((new ToolResultContentBlock.TextBlock(blockedMsg), true));
                    await AppendSessionEntryAsync("tool_result", blockedMsg, ct,
                        toolName: tu.Name, toolUseId: tu.Id, isError: true).ConfigureAwait(false);
                    continue;
                }

                yield return new RuntimeEvent.ToolUseRequested(tu.Id, tu.Name, tu.Input);

                LogToolDispatched(_logger, tu.Name);
                var toolSw = Stopwatch.StartNew();

                var execResult = await _toolExecutor.ExecuteAsync(tu.Name, tu.Input, ct).ConfigureAwait(false);

                toolSw.Stop();
                LogToolResult(_logger, tu.Name, toolSw.ElapsedMilliseconds, execResult.IsError);

                yield return new RuntimeEvent.ToolResult(tu.Id, tu.Name, execResult.Output, execResult.IsError);

                if (!execResult.Success && execResult.Output.Contains("denied", StringComparison.OrdinalIgnoreCase))
                    yield return new RuntimeEvent.PermissionDenied(tu.Name, execResult.Output);

                // PostToolUse / PostToolUseFailure: fire-and-forget.
                var postEvent = execResult.IsError ? HookEvent.PostToolUseFailure : HookEvent.PostToolUse;
                var postCtx = new HookContext(postEvent, SessionId,
                    ToolName: tu.Name,
                    ToolOutput: execResult.Output,
                    FilePath: TryExtractFilePath(tu.Input),
                    IsError: execResult.IsError);
                _ = _hookRunner.RunAsync(postEvent, postCtx, CancellationToken.None)
                    .ContinueWith(static t => { if (t.IsFaulted) _ = t.Exception; }, TaskScheduler.Default);

                toolResultPairs.Add((new ToolResultContentBlock.TextBlock(execResult.Output), execResult.IsError));

                await AppendSessionEntryAsync("tool_result", execResult.Output, ct,
                    toolName: tu.Name, toolUseId: tu.Id, isError: execResult.IsError)
                    .ConfigureAwait(false);
            }

            // Append ONE tool-result InputContentBlock per tool call so the LLM receives correctly
            // paired tool_call_id → tool_result associations (OpenAI requires 1:1 pairing).
            var toolResultInputBlocks = new List<InputContentBlock>(toolUseBlocks.Count);
            for (var i = 0; i < toolUseBlocks.Count; i++)
            {
                toolResultInputBlocks.Add(new InputContentBlock.ToolResultBlock(
                    toolUseBlocks[i].Id,
                    [toolResultPairs[i].Content],
                    toolResultPairs[i].IsError));
            }
            _history.Add(new InputMessage("user", toolResultInputBlocks));

            round++;
        }

        turnSw.Stop();
        LogMaxRoundsReached(_logger, MaxToolRounds, SessionId);
        yield return new RuntimeEvent.RuntimeError($"Maximum tool rounds ({MaxToolRounds}) reached.");
    }

    /// <inheritdoc/>
    public void Reset() => _history.Clear();

    private static readonly int[] s_retryDelaysMs = [1000, 2000, 4000];

    /// <summary>
    /// Reads all SSE stream events from the provider, retrying on transient errors (up to 3 attempts).
    /// </summary>
    private async Task<(List<RuntimeEvent> Events, StreamAccumulation Accumulated)> CollectStreamEventsAsync(
        Sovrant.Api.Providers.ILlmProvider provider,
        MessagesRequest request,
        CancellationToken ct)
    {
        const int MaxAttempts = 3;

        (List<RuntimeEvent> Events, StreamAccumulation Accumulated) last = default;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            last = await AttemptCollectAsync(provider, request, ct).ConfigureAwait(false);

            if (last.Accumulated.Success) return last;

            var errEvent = last.Events.OfType<RuntimeEvent.RuntimeError>().FirstOrDefault();
            if (errEvent is null || !IsRetryableError(errEvent.Message) || attempt == MaxAttempts)
                return last;

            var delayMs = s_retryDelaysMs[attempt - 1];
            LogRetry(_logger, attempt, MaxAttempts, errEvent.Message, delayMs);
            await Task.Delay(delayMs, ct).ConfigureAwait(false);
        }

        return last;
    }

    private static bool IsRetryableError(string message) =>
        message.Contains("429", StringComparison.Ordinal) ||
        message.Contains("500", StringComparison.Ordinal) ||
        message.Contains("502", StringComparison.Ordinal) ||
        message.Contains("503", StringComparison.Ordinal) ||
        message.Contains("504", StringComparison.Ordinal) ||
        message.Contains("Provider returned error", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("too many requests", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Single attempt: reads all SSE stream events from the provider and accumulates them into structured result data.
    /// </summary>
    private async Task<(List<RuntimeEvent> Events, StreamAccumulation Accumulated)> AttemptCollectAsync(
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

                    case StreamEvent.MessageDelta { Delta.StopReason: var sr, Usage.InputTokens: var it, Usage.OutputTokens: var ot }:
                        if (sr is not null) stopReason = sr;
                        if (it > 0) inputTokens = it;   // OpenAI sends input tokens here (not in MessageStart)
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
        // Use Clone() so the returned JsonElement owns its memory independently of the
        // JsonDocument (which is otherwise eligible for GC and ArrayPool reclamation).
        if (string.IsNullOrWhiteSpace(json))
            return JsonDocument.Parse("{}").RootElement.Clone();

        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
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

    /// <summary>
    /// Summarises the oldest portion of <see cref="_history"/> using the LLM when the input
    /// token count approaches <see cref="SovrantConfig.CompactThreshold"/>.
    /// </summary>
    private async Task MaybeCompactHistoryAsync(int inputTokens, CancellationToken ct)
    {
        var threshold = _config.CompactThreshold;
        if (threshold <= 0 || inputTokens < threshold) return;
        if (_history.Count < 6) return;

        // Allow hooks to save state before context is lost.
        await _hookRunner.RunAsync(
            HookEvent.PreCompact,
            new HookContext(HookEvent.PreCompact, SessionId, InputTokens: inputTokens),
            ct).ConfigureAwait(false);

        // Keep the last 4 messages intact; summarise everything before that
        const int KeepTail = 4;
        var cutPoint = _history.Count - KeepTail;
        if (cutPoint <= 0) return;

        var toSummarize = _history.Take(cutPoint).ToList();
        var toKeep = _history.Skip(cutPoint).ToList();

        var historyText = string.Join("\n\n", toSummarize
            .Select(m => $"{m.Role.ToUpperInvariant()}: {ExtractText(m)}"));

        var summaryRequest = new MessagesRequest(
            _config.Model,
            2048,
            [InputMessage.UserText(
                "Summarise the following conversation history in 3-5 concise paragraphs. " +
                "Preserve all key facts, file paths, decisions, and technical context:\n\n" +
                historyText)])
        {
            System = "You are a concise technical summariser. Preserve important details.",
            Stream = true,
        };

        try
        {
            var provider = await _router.RouteAsync(summaryRequest, ct).ConfigureAwait(false);
            var (_, accumulated) = await AttemptCollectAsync(provider, summaryRequest, ct).ConfigureAwait(false);

            var summaryText = string.Join(string.Empty,
                accumulated.Blocks.OfType<OutputContentBlock.TextBlock>().Select(b => b.Text)).Trim();

            if (string.IsNullOrEmpty(summaryText)) return;

            _history.Clear();
            _history.Add(InputMessage.UserText($"[Conversation history summary — auto-compacted at {inputTokens} tokens]\n\n{summaryText}"));
            _history.Add(InputMessage.AssistantText("Understood. I have the context from the conversation summary."));
            _history.AddRange(toKeep);

            LogCompacted(_logger, SessionId, toSummarize.Count, inputTokens);

            await AppendSessionEntryAsync("compaction",
                $"History compacted: {toSummarize.Count} messages summarised at {inputTokens} input tokens.",
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRequestFailed(_logger, $"Compaction failed (non-fatal): {ex.Message}");
        }
    }

    private static string ExtractText(InputMessage m) =>
        string.Join(" ", m.Content.OfType<InputContentBlock.TextBlock>().Select(b => b.Text));

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

        await _sessionStore.AppendAsync(SessionId, entry, _ownerUserId, ct).ConfigureAwait(false);
    }

    private RuntimeEvent.TurnCost? RecordCostIfEnabled(int inputTokens, int outputTokens)
    {
        if (_costFacade is null) return null;
        var record = _costFacade.RecordTurn(
            _sessionId, _config.Model, inputTokens, outputTokens);
        return new RuntimeEvent.TurnCost(record.EstimatedUsd, record.Source);
    }

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder(
            "You are a highly capable AI assistant with access to tools. " +
            $"You are powered by the {_config.Model} model, served through the Sovrant runtime. " +
            "When asked what model or provider you are, state your actual model name. " +
            "When you have tools available, USE them proactively to accomplish the user's request. " +
            "Do not just describe what you would do — actually do it by calling the appropriate tools. " +
            "If you need to read a file, call the read tool. If you need to write code, call the write tool. " +
            "If you need permission for a dangerous action, call the tool and the system will prompt the user to approve. " +
            "For simple greetings and conversational messages, respond with plain text.");

        if (_config.PermissionMode == Permissions.PermissionMode.Plan)
        {
            sb.Append("\n\nYou are operating in PLAN MODE. ")
              .Append("You may only read files and gather information. ")
              .Append("You must not execute write, edit, delete, or shell operations. ")
              .Append("Describe what you would do instead. ")
              .Append("You MAY call ExitPlanMode to leave plan mode when instructed by the user.");
        }

        // Artifacts guidance — use the Artifact tool for documents, plans, reports, etc.
        var workspaceId = Environment.GetEnvironmentVariable("SOVRANT_WORKSPACE_ID")
            ?? Artifacts.ArtifactScope.DefaultWorkspaceId;
        var projectId = Environment.GetEnvironmentVariable("SOVRANT_PROJECT_ID")
            ?? Artifacts.ArtifactScope.DefaultProjectId;
        sb.Append("\n\nWhen creating documents, plans, reports, specifications, or any deliverable that is NOT a direct modification to existing source code, ")
          .Append("use the Artifact tool with action 'write' to store it. ")
          .Append("Always pass workspace_id='").Append(workspaceId)
          .Append("', project_id='").Append(projectId)
          .Append("', and run_id='").Append(_sessionId)
          .Append("'. Choose a descriptive path like 'plan.md', 'report.md', or 'docs/architecture.md'. ")
          .Append("This keeps generated outputs organized per workspace, project, and session. ")
          .Append("Only use the Write tool for modifying actual source code files in the project.");

        // Global memory: ~/.sovrant/memory.md
        var globalMemory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "memory.md");
        AppendMemoryFile(sb, globalMemory, "Global memory");

        // Project memory: .sovrant/memory.md in the working directory
        var projectMemory = Path.Combine(
            Directory.GetCurrentDirectory(), ".sovrant", "memory.md");
        AppendMemoryFile(sb, projectMemory, "Project memory");

        // Git context: branch, status, recent commits
        AppendGitContext(sb, Directory.GetCurrentDirectory());

        return sb.ToString();
    }

    private static void AppendMemoryFile(StringBuilder sb, string path, string label)
    {
        if (!File.Exists(path)) return;
        try
        {
            var content = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(content)) return;
            sb.Append("\n\n---\n")
              .Append("## ").Append(label).Append(" (").Append(path).Append(")\n\n")
              .Append(content);
        }
        catch (IOException) { /* silently skip unreadable files */ }
    }

    private static void AppendGitContext(StringBuilder sb, string workingDir)
    {
        try
        {
            // Check if we're in a git repo
            var branch = RunGit(workingDir, "rev-parse --abbrev-ref HEAD");
            if (string.IsNullOrEmpty(branch)) return;

            sb.Append("\n\n---\n## Git Context\n\n");
            sb.Append("- **Branch:** ").Append(branch).Append('\n');

            var status = RunGit(workingDir, "status --porcelain");
            if (!string.IsNullOrEmpty(status))
            {
                var lines = status.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                sb.Append("- **Working tree:** ").Append(lines.Length).Append(" changed file(s)\n");
            }
            else
            {
                sb.Append("- **Working tree:** clean\n");
            }

            var log = RunGit(workingDir, "log --oneline -5");
            if (!string.IsNullOrEmpty(log))
            {
                sb.Append("\n**Recent commits:**\n```\n").Append(log).Append("\n```\n");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Git not available or not a repo — silently skip.
        }
    }

    private static string? RunGit(string workingDir, string args)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    /// Simple heuristic to detect if the user's message likely requires tool use.
    /// Short greetings, questions, and conversational messages return false.
    /// Messages that mention files, code, commands, etc. return true.
    /// </summary>
    /// <summary>
    /// Caps max_tokens to a value the model is known to support.
    /// Older gpt-4 variants (gpt-4, gpt-4-turbo, gpt-3.5) only support 4096 output tokens.
    /// Modern models (gpt-4o, gpt-4.1, gpt-4.5, gpt-5) support 16K+.
    /// </summary>
    private static int CapMaxTokens(string model, int configured)
    {
        var name = model;
        var slash = model.LastIndexOf('/');
        if (slash >= 0) name = model[(slash + 1)..];

        // gpt-4o / gpt-4o-mini — max output 16384
        if (name.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase))
            return Math.Min(configured, 16_384);

        // gpt-4.1, gpt-4.1-mini, gpt-4.1-nano — max output 32768
        if (name.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase))
            return Math.Min(configured, 32_768);

        // gpt-4.5 — max output 16384
        if (name.StartsWith("gpt-4.5", StringComparison.OrdinalIgnoreCase))
            return Math.Min(configured, 16_384);

        // gpt-5 — pass through (high output limit)
        if (name.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
            return configured;

        // o-series reasoning models — max output varies but 100K is safe
        if (name.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
            return Math.Min(configured, 100_000);

        // Legacy gpt-4 and gpt-3.5 — cap at 4096
        if (name.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("gpt-3", StringComparison.OrdinalIgnoreCase))
            return Math.Min(configured, 4096);

        // Non-OpenAI models (DeepSeek, Claude, Llama, etc.) — pass through
        return configured;
    }

    private static bool LooksLikeToolRequest(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Very short messages (under 20 chars) are almost never tool requests.
        if (message.Length < 20)
        {
            // Unless they contain obvious tool-trigger words.
#pragma warning disable CA1308 // UseToUpperInvariant — keyword matching is lowercase by convention
            var lower = message.ToLowerInvariant();
            return lower.Contains("read", StringComparison.Ordinal)
                || lower.Contains("write", StringComparison.Ordinal)
                || lower.Contains("run", StringComparison.Ordinal)
                || lower.Contains("list", StringComparison.Ordinal)
                || lower.Contains("find", StringComparison.Ordinal)
                || lower.Contains("search", StringComparison.Ordinal)
                || lower.Contains("edit", StringComparison.Ordinal)
                || lower.Contains("create", StringComparison.Ordinal)
                || lower.Contains("delete", StringComparison.Ordinal)
                || lower.Contains("fix", StringComparison.Ordinal)
                || lower.Contains("debug", StringComparison.Ordinal);
        }

        // Longer messages — check for keywords that suggest tool use.
        var keywords = new[]
        {
            "file", "directory", "folder", "code", "function", "class", "method",
            "read", "write", "edit", "create", "delete", "remove", "rename",
            "run", "execute", "command", "shell", "bash", "powershell",
            "search", "find", "grep", "glob", "list",
            "fix", "debug", "refactor", "test", "build", "compile",
            "install", "deploy", "commit", "push", "pull",
            "api", "endpoint", "database", "migration",
            "implement", "add", "update", "modify", "change",
            "research", "look up", "fetch", "download", "web", "url", "http",
            "explain", "analyze", "review", "check", "show", "open",
        };

        var msgLower = message.ToLowerInvariant();
#pragma warning restore CA1308
        foreach (var kw in keywords)
        {
            if (msgLower.Contains(kw, StringComparison.Ordinal))
                return true;
        }

        // Check for file paths (contains / or \ with an extension).
        if (message.Contains('/', StringComparison.Ordinal) || message.Contains('\\', StringComparison.Ordinal))
            return true;

        return false;
    }

    /// <summary>
    /// Filters the tool list based on model capabilities. If the model doesn't
    /// support native tools, returns empty. If it has a MaxTools limit, truncates.
    /// </summary>
    private IReadOnlyList<ToolDefinition> FilterToolsForModel(IReadOnlyList<ToolDefinition> tools)
    {
        if (tools.Count == 0 || _capabilityRegistry is null)
            return tools;

        var caps = _capabilityRegistry.GetCapabilities(_config.Model);

        // Model doesn't support native tool calling — don't send tools.
        if (!caps.NativeTools)
            return [];

        // Respect max_tools limit if set.
        if (caps.MaxTools is > 0 and var max && tools.Count > max)
            return tools.Take(max).ToList();

        return tools;
    }

    private static string? TryExtractFilePath(JsonElement input)
    {
        foreach (var prop in new[] { "path", "file_path", "filepath" })
        {
            if (input.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
        }
        return null;
    }

    private sealed record StreamAccumulation(
        bool Success,
        string StopReason,
        int InputTokens,
        int OutputTokens,
        List<OutputContentBlock> Blocks);
}
