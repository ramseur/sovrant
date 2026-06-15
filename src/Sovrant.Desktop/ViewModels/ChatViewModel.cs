using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Commands;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Session;

namespace Sovrant.Desktop.ViewModels;

public partial class ChatViewModel : ViewModelBase, IDisposable
{
    private readonly IRuntimeSessionPool _sessionPool;
    private readonly ISessionStore _sessionStore;
    private readonly IAuditStore _auditStore;
    private readonly IKnowledgeAttributionStore? _attributionStore;
    private readonly SlashCommandDispatcher _commandDispatcher;
    private readonly DesktopConfirmationHandler? _confirmationHandler;
    private readonly ActiveContextViewModel _activeContext;
    private readonly ActiveSessionsViewModel _activeSessions;
    private readonly SovrantConfig _config;

    // True while a turn for this session is owned by _activeSessions.
    private bool _isBackgroundSession;

    private string? _agentSystemPrompt;

    [ObservableProperty]
    private string? _agentName;

    [ObservableProperty]
    private string _sessionId;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private int _tokenCount;

    [ObservableProperty]
    private decimal _sessionCost;

    [ObservableProperty]
    private bool _hasMessages;

    [ObservableProperty]
    private bool _showCommandSuggestions;

    // Phase 99 — defaults to private; reads stored state on session resume.
    [ObservableProperty]
    private bool _isSessionPrivate = true;

    public ActiveContextViewModel ActiveContext => _activeContext;
    public ObservableCollection<MessageViewModel> Messages { get; } = [];
    public ObservableCollection<CommandSuggestion> CommandSuggestions { get; } = [];
    /// <summary>Active MCP servers are managed by <see cref="ActiveContextViewModel"/>, not per-chat.</summary>
    public IReadOnlyList<string> ActiveMcpServers => _activeContext.ActiveMcpServers;

    /// <summary>Raised after a turn completes (message sent and response received).</summary>
    public event Action? TurnCompleted;

    public ChatViewModel(IRuntimeSessionPool sessionPool, ISessionStore sessionStore,
        IAuditStore auditStore,
        SlashCommandDispatcher commandDispatcher, ActiveContextViewModel activeContext,
        ActiveSessionsViewModel activeSessions,
        SovrantConfig config,
        DesktopConfirmationHandler? confirmationHandler = null,
        IKnowledgeAttributionStore? attributionStore = null)
    {
        _sessionPool = sessionPool;
        _sessionStore = sessionStore;
        _auditStore = auditStore;
        _attributionStore = attributionStore;
        _commandDispatcher = commandDispatcher;
        _confirmationHandler = confirmationHandler;
        _activeContext = activeContext;
        _activeSessions = activeSessions;
        _config = config;
        _sessionId = $"session-{Guid.NewGuid():N}";

        if (_confirmationHandler is not null)
            _confirmationHandler.ConfirmationRequested += OnConfirmationRequested;

        _activeContext.PropertyChanged += OnActiveContextPropertyChanged;
    }

    private void OnActiveContextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActiveContextViewModel.ActiveAgentName)
            && _activeContext.ActiveAgentName is null)
        {
            AgentName = null;
            _agentSystemPrompt = null;
        }
    }

    private void OnConfirmationRequested(ConfirmationRequest request)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Find the current assistant message and add the confirmation inline.
            var lastAssistant = Messages.LastOrDefault(m => m.Role == "assistant");
            lastAssistant?.AddConfirmation(request);
        });
    }

    public void SetAgentScope(string agentName, string? systemPrompt)
    {
        AgentName = agentName;
        _agentSystemPrompt = systemPrompt;
        _activeContext.ActiveAgentName = agentName;
    }

    public void SeedInput(string text)
    {
        InputText = text;
    }

    public async Task LoadSessionAsync(string sessionId, CancellationToken ct = default)
    {
        SessionId = sessionId;
        Messages.Clear();

        var storedPrivacy = await _sessionStore.GetIsPrivateAsync(sessionId, ct).ConfigureAwait(false);
        IsSessionPrivate = storedPrivacy ?? true;

        var storedAgent = await _sessionStore.GetAgentNameAsync(sessionId, ct).ConfigureAwait(false);
        AgentName = storedAgent;
        _activeContext.ActiveAgentName = storedAgent;

        var entries = await _sessionStore.LoadAsync(sessionId, ownerUserId: App.SovrantUserId, ct: ct);
        foreach (var entry in entries)
        {
            if (entry.Role is "user" or "assistant")
            {
                Messages.Add(new MessageViewModel
                {
                    Role = entry.Role,
                    Text = entry.Content,
                    IsComplete = true,
                    ModelName = entry.Role == "assistant" ? entry.Model : null,
                    ProviderName = entry.Role == "assistant" ? entry.Provider : null,
                    UserDisplayName = entry.Role == "user" ? _activeContext.UserDisplayName : null,
                });
            }
        }

        HasMessages = Messages.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken ct)
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        InputText = string.Empty;

        // Try slash command dispatch first.
        if (text.StartsWith('/'))
        {
            await HandleSlashCommandAsync(text, ct);
            return;
        }

        await SendToRuntimeAsync(text, ct);
    }

    private async Task HandleSlashCommandAsync(string text, CancellationToken ct)
    {
        HasMessages = true;
        await Dispatcher.UIThread.InvokeAsync(() =>
            Messages.Add(new MessageViewModel { Role = "user", Text = text, UserDisplayName = _activeContext.UserDisplayName }));

        try
        {
            await App.RuntimeReady.Task.WaitAsync(ct).ConfigureAwait(false);

            var result = await _commandDispatcher.TryDispatchAsync(text, ct).ConfigureAwait(false);

            if (result is null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    Messages.Add(new MessageViewModel { Role = "assistant", Text = "Unknown command. Type /help for a list.", IsComplete = true }));
                return;
            }

            // Handle special actions
            if (result.ShouldClearHistory)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Messages.Clear();
                    HasMessages = false;
                    TokenCount = 0;
                    SessionCost = 0;
                    SessionId = $"session-{Guid.NewGuid():N}";
                });
                return;
            }

            // If the command wants to inject as a user message, send it to the LLM
            if (result.InjectAsUserMessage is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    Messages.Add(new MessageViewModel { Role = "assistant", Text = "Running command...", IsComplete = true }));
                await SendToRuntimeAsync(result.InjectAsUserMessage, ct);
                return;
            }

            // Show command output
            if (!string.IsNullOrEmpty(result.Output))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    Messages.Add(new MessageViewModel { Role = "assistant", Text = result.Output, IsComplete = true }));
            }

            if (result.ShouldExit)
            {
                // Desktop: close the app
                await Dispatcher.UIThread.InvokeAsync(() => App.MainWindow?.Close());
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                Messages.Add(new MessageViewModel { Role = "assistant", Text = $"Command error: {ex.Message}", IsComplete = true }));
        }
    }

    private async Task SendToRuntimeAsync(string text, CancellationToken ct)
    {
        const int maxActive = 5;
        if (_activeSessions.RunningCount() >= maxActive)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                Messages.Add(new MessageViewModel
                {
                    Role = "assistant",
                    Text = $"Cannot start: you already have {maxActive} background session(s) running. Wait for one to finish or cancel it.",
                    IsComplete = true,
                }));
            return;
        }

        var isFirstMessage = Messages.All(m => m.Role != "user");
        // Phase 116 H — turn index = number of assistant messages already in the list.
        var turnIndex = Messages.Count(m => !m.IsUser);
        var assistantMsg = new MessageViewModel { Role = "assistant", ModelName = _config.Model, TurnIndex = turnIndex };

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Messages.Count == 0 || Messages[^1].Role != "user" || Messages[^1].Text != text)
                Messages.Add(new MessageViewModel { Role = "user", Text = text, UserDisplayName = _activeContext.UserDisplayName });
            assistantMsg.StartThinking(text);
            Messages.Add(assistantMsg);
            IsSending = true;
            HasMessages = true;
        });

        string? title = null;
        if (isFirstMessage)
        {
            title = text.Length > 60 ? text[..60] + "..." : text;
            await _sessionStore.SetTitleAsync(SessionId, title, ownerUserId: App.SovrantUserId, ct: CancellationToken.None)
                .ConfigureAwait(false);
            if (AgentName is not null)
                await _sessionStore.SetAgentNameAsync(SessionId, AgentName, ownerUserId: App.SovrantUserId, ct: CancellationToken.None)
                    .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => TurnCompleted?.Invoke());
        }

#pragma warning disable CA2000 // ownership transferred to ActiveSessionsViewModel
        var bgCts = new CancellationTokenSource();
#pragma warning restore CA2000
        _isBackgroundSession = true;
        _activeSessions.BeginTurn(SessionId, title ?? SessionId, bgCts);
        _activeSessions.Attach(SessionId, evt =>
            Dispatcher.UIThread.Post(() => HandleEvent((RuntimeEvent)evt, assistantMsg)));

        _ = Task.Run(async () => await RunTurnAsync(text, assistantMsg, bgMode: true, bgCts.Token), CancellationToken.None);
    }

    private async Task RunTurnAsync(string text, MessageViewModel assistantMsg, bool bgMode, CancellationToken token)
    {
        _sessionPool.BeginTurn(SessionId, App.SovrantUserId);
        try
        {
            await App.RuntimeReady.Task.WaitAsync(token).ConfigureAwait(false);

            var pooled = await _sessionPool.GetOrCreateAsync(SessionId,
                agentSystemPrompt: _agentSystemPrompt,
                agentName: AgentName,
                ct: token).ConfigureAwait(false);

            // Sync privacy — session row may have just been created with default
            // is_private = true; persist any pre-message toggle the user made.
            if (!IsSessionPrivate)
                await _sessionStore.UpdatePrivacyAsync(SessionId, App.SovrantUserId, false, token).ConfigureAwait(false);

            // Apply the session-level MCP selection from ActiveContextViewModel (opt-in).
            var activeMcps = _activeContext.ActiveMcpServers;
            pooled.Config.AllowedMcpServers = activeMcps.Count > 0 ? activeMcps : null;
            await _sessionStore.SetMcpConnectionsAsync(SessionId, pooled.Config.AllowedMcpServers, ownerUserId: App.SovrantUserId, ct: token)
                .ConfigureAwait(false);

            using var _scope = SessionContext.Push(pooled.Config);
            await foreach (var ev in pooled.Runtime.RunTurnAsync(text, token))
            {
                if (bgMode)
                    _activeSessions.PushEvent(SessionId, ev);
                else
                    await Dispatcher.UIThread.InvokeAsync(() => HandleEvent(ev, assistantMsg));
            }

            if (bgMode) _activeSessions.Complete(SessionId);
        }
        catch (OperationCanceledException)
        {
            if (bgMode) _activeSessions.Fail(SessionId, "Cancelled");
        }
        catch (Exception ex)
        {
            if (bgMode) _activeSessions.Fail(SessionId, ex.Message);
            else await Dispatcher.UIThread.InvokeAsync(() => assistantMsg.SetError(ex.Message));
        }
        finally
        {
            _sessionPool.EndTurn(SessionId, App.SovrantUserId);

            // Phase 116 H — load attributions off the UI thread before the UI update.
            List<KnowledgeAttribution>? sources = null;
#pragma warning disable CA1031
            try
            {
                if (_attributionStore is not null)
                {
                    var all = await _attributionStore.GetBySessionAsync(SessionId, CancellationToken.None);
                    sources = all.Where(a => a.TurnIndex == assistantMsg.TurnIndex).ToList();
                }
            }
            catch { /* best-effort */ }
#pragma warning restore CA1031

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSending = false;
                assistantMsg.CompleteStreaming();
                if (sources is not null)
                {
                    foreach (var a in sources) assistantMsg.Sources.Add(a);
                    if (sources.Count > 0) assistantMsg.HasSources = true;
                }
                TurnCompleted?.Invoke();
            });
        }
    }

    /// <summary>
    /// Called by MainViewModel when the user navigates back to a parked background chat.
    /// Replays buffered events and re-subscribes to live events.
    /// </summary>
    public async Task ReAttachToBackground()
    {
        if (!_activeSessions.HasSession(SessionId))
        {
            // Session completed while parked in background — reload from store so the
            // full turn history (including any messages written during background run) is shown.
            await LoadSessionAsync(SessionId);
            return;
        }

        var lastAssistant = Messages.LastOrDefault(m => m.Role == "assistant");
        if (lastAssistant is null) return;

        var (buffered, isRunning) = _activeSessions.Attach(SessionId, evt =>
            Dispatcher.UIThread.Post(() => HandleEvent((RuntimeEvent)evt, lastAssistant)));

        foreach (var evt in buffered)
            HandleEvent((RuntimeEvent)evt, lastAssistant);

        IsSending = isRunning;
        _isBackgroundSession = isRunning;
    }

    [RelayCommand]
    private void Stop()
    {
        IsSending = false;

        if (_activeSessions.HasSession(SessionId))
        {
            _activeSessions.Cancel(SessionId);
        }
        _isBackgroundSession = false;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_isBackgroundSession && _activeSessions.HasSession(SessionId))
                _activeSessions.Detach(SessionId);

            if (_confirmationHandler is not null)
                _confirmationHandler.ConfirmationRequested -= OnConfirmationRequested;

            _activeContext.PropertyChanged -= OnActiveContextPropertyChanged;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    private async Task RetryLastAsync(CancellationToken ct)
    {
        // Find the last user message and remove the failed assistant response.
        string? lastUserText = null;
        for (var i = Messages.Count - 1; i >= 0; i--)
        {
            if (Messages[i].Role == "assistant")
            {
                Messages.RemoveAt(i);
                continue;
            }
            if (Messages[i].Role == "user")
            {
                lastUserText = Messages[i].Text;
                Messages.RemoveAt(i);
                break;
            }
        }

        if (lastUserText is null) return;

        // Re-send by setting InputText and invoking Send.
        InputText = lastUserText;
        if (SendCommand.CanExecute(null))
            await SendAsync(ct);
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        HasMessages = false;
        TokenCount = 0;
        SessionId = $"session-{Guid.NewGuid():N}";
    }

    [RelayCommand]
    private void Suggestion(string text)
    {
        InputText = text;
        if (SendCommand.CanExecute(null))
            SendCommand.Execute(null);
    }

    [RelayCommand]
    private async Task TogglePrivacyAsync(CancellationToken ct)
    {
        var newValue = !IsSessionPrivate;
        IsSessionPrivate = newValue;
        try
        {
            await _sessionStore.UpdatePrivacyAsync(SessionId, App.SovrantUserId, newValue, ct).ConfigureAwait(false);
            await _auditStore.LogPrivacyChangeAsync(App.SovrantUserId, "session", SessionId, newValue, ct).ConfigureAwait(false);
        }
        catch
        {
            IsSessionPrivate = !newValue;
            throw;
        }
    }

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);

    partial void OnInputTextChanged(string value)
    {
        SendCommand.NotifyCanExecuteChanged();
        UpdateCommandSuggestions(value);
    }

    partial void OnIsSendingChanged(bool value) => SendCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void AcceptSuggestion(CommandSuggestion suggestion)
    {
        InputText = $"/{suggestion.Name} ";
        ShowCommandSuggestions = false;
    }

    private void UpdateCommandSuggestions(string input)
    {
        CommandSuggestions.Clear();

        if (!input.StartsWith('/') || input.Contains(' ', StringComparison.Ordinal))
        {
            ShowCommandSuggestions = false;
            return;
        }

        var query = input[1..];
        var matches = _commandDispatcher.Commands
            .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(8);

        foreach (var cmd in matches)
            CommandSuggestions.Add(new CommandSuggestion(cmd.Name, cmd.Description));

        ShowCommandSuggestions = CommandSuggestions.Count > 0;
    }

    private void HandleEvent(RuntimeEvent ev, MessageViewModel msg)
    {
        switch (ev)
        {
            case RuntimeEvent.ModelSelected { Model: var model, ProviderName: var prov }:
                msg.ModelName = model;
                msg.ProviderName = prov;
                break;

            case RuntimeEvent.TextChunk { Text: var chunk }:
                msg.AppendText(chunk);
                break;

            case RuntimeEvent.ToolUseRequested t:
                msg.AddToolUse(t.ToolName, t.ToolUseId);
                break;

            case RuntimeEvent.ToolResult t:
                msg.UpdateToolResult(t.ToolUseId, t.Content, t.IsError);
                break;

            case RuntimeEvent.TurnComplete t:
                TokenCount += t.InputTokens + t.OutputTokens;
                msg.ModelName = t.Model;
                msg.ProviderName = t.ProviderName;
                msg.CompleteStreaming();
                IsSending = false; // hide stop button as soon as LLM is done; trailing events may still drain
                break;

            case RuntimeEvent.TurnCost { EstimatedUsd: var usd }:
                if (usd is not null)
                    SessionCost += usd.Value;
                break;

            case RuntimeEvent.RuntimeError { Message: var errMsg }:
                if (!msg.IsComplete) msg.SetError(errMsg);
                break;

            case RuntimeEvent.PermissionDenied { ToolName: var tool, Reason: var reason }:
                msg.AppendText($"\n\n*Permission denied for {tool}: {reason}*");
                break;

            case RuntimeEvent.ArtifactWritten a:
            {
                var ext = System.IO.Path.GetExtension(a.Path).TrimStart('.');
                var urlStr = a.AccessUrl?.ToString();
                var vm = new DocumentArtifactViewModel
                {
                    DisplayName = System.IO.Path.GetFileName(a.Path),
                    Format = (string.IsNullOrEmpty(a.Format) ? ext : a.Format).ToUpperInvariant(),
                    SizeText = DocumentArtifactParser.FormatSize(a.SizeBytes),
                    Icon = DocumentArtifactParser.FormatIconPublic(a.Format),
                    AccessUrl = urlStr,
                    LocalPath = a.AccessUrl is { IsFile: true } fu ? fu.LocalPath : null,
                };
                msg.AddStandaloneArtifact(vm);
                break;
            }

            // ── Phase 59 events ─────────────────────────────────────────
            case RuntimeEvent.IntentNarrated { Narration: var narration }:
                msg.IntentNarration = narration;
                msg.ThinkingText = narration;
                break;

            case RuntimeEvent.ClarificationNeeded { Question: var question }:
                msg.ClarificationQuestion = question;
                msg.AppendText($"\n\n**Clarification needed:** {question}");
                break;

            case RuntimeEvent.PlanPresented { PlanId: var planId, FormattedPlan: var plan, RequiresApproval: var needsApproval }:
                msg.PlanId = planId;
                msg.PlanContent = plan;
                msg.PlanRequiresApproval = needsApproval;
                msg.AppendText($"\n\n**Plan:**\n```\n{plan}\n```");
                if (needsApproval)
                    msg.AppendText("\n\n*This plan requires approval before execution.*");
                break;

            case RuntimeEvent.StepProgress { Current: var current, Total: var total, Intent: var intent, Status: var status }:
                msg.CurrentStep = current;
                msg.TotalSteps = total;
                msg.StepProgressText = $"Step {current}/{total}: {intent} [{status}]";
                break;
        }
    }
}

public sealed record CommandSuggestion(string Name, string Description)
{
    public string Display => $"/{Name}";
}
