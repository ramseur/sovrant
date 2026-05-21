using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Commands;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Session;

namespace Sovrant.Desktop.ViewModels;

public partial class ChatViewModel : ViewModelBase, IDisposable
{
    private readonly IRuntimeSessionPool _sessionPool;
    private readonly ISessionStore _sessionStore;
    private readonly SlashCommandDispatcher _commandDispatcher;
    private readonly DesktopConfirmationHandler? _confirmationHandler;
    private readonly ActiveContextViewModel _activeContext;
    private readonly ActiveSessionsViewModel _activeSessions;
    private readonly SovrantConfig _config;
    private readonly IMcpServerStore? _mcpStore;

    // True while a turn for this session is owned by _activeSessions.
    private bool _isBackgroundSession;

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

    public ActiveContextViewModel ActiveContext => _activeContext;
    public ObservableCollection<MessageViewModel> Messages { get; } = [];
    public ObservableCollection<CommandSuggestion> CommandSuggestions { get; } = [];
    public ObservableCollection<McpConnectionItem> Connections { get; } = [];

    public bool HasConnections => Connections.Count > 0;

    public string ConnectionsLabel
    {
        get
        {
            var total = Connections.Count;
            if (total == 0) return "(none)";
            var selected = Connections.Count(c => c.IsSelected);
            return selected == total ? "(all)" : $"({selected}/{total})";
        }
    }

    /// <summary>Raised after a turn completes (message sent and response received).</summary>
    public event Action? TurnCompleted;

    public ChatViewModel(IRuntimeSessionPool sessionPool, ISessionStore sessionStore,
        SlashCommandDispatcher commandDispatcher, ActiveContextViewModel activeContext,
        ActiveSessionsViewModel activeSessions,
        SovrantConfig config,
        DesktopConfirmationHandler? confirmationHandler = null,
        IMcpServerStore? mcpStore = null)
    {
        _sessionPool = sessionPool;
        _sessionStore = sessionStore;
        _commandDispatcher = commandDispatcher;
        _confirmationHandler = confirmationHandler;
        _activeContext = activeContext;
        _activeSessions = activeSessions;
        _config = config;
        _mcpStore = mcpStore;
        _sessionId = $"session-{Guid.NewGuid():N}";

        if (_confirmationHandler is not null)
            _confirmationHandler.ConfirmationRequested += OnConfirmationRequested;

        _ = RefreshConnectionsAsync();
    }

    public async Task RefreshConnectionsAsync()
    {
        if (_mcpStore is null) return;
        try
        {
            var configs = await _mcpStore.GetAllAsync().ConfigureAwait(false);
            var names = configs.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var existing = Connections.ToDictionary(c => c.Name, StringComparer.Ordinal);
                Connections.Clear();
                foreach (var name in names)
                {
                    var item = existing.TryGetValue(name, out var prev)
                        ? prev
                        : new McpConnectionItem(name) { IsSelected = false };
                    if (!existing.ContainsKey(name))
                    {
                        item.PropertyChanged += (_, __) =>
                            OnPropertyChanged(nameof(ConnectionsLabel));
                    }
                    Connections.Add(item);
                }
                OnPropertyChanged(nameof(HasConnections));
                OnPropertyChanged(nameof(ConnectionsLabel));
            });
        }
        catch (System.Data.Common.DbException) { /* MCP table not yet provisioned — empty list is fine */ }
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

    public async Task LoadSessionAsync(string sessionId, CancellationToken ct = default)
    {
        SessionId = sessionId;
        Messages.Clear();

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

        var assistantMsg = new MessageViewModel { Role = "assistant", ModelName = _config.Model };
        var isFirstMessage = Messages.All(m => m.Role != "user");

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
            await Dispatcher.UIThread.InvokeAsync(() => TurnCompleted?.Invoke());
        }

#pragma warning disable CA2000 // ownership transferred to ActiveSessionsViewModel
        var bgCts = new CancellationTokenSource();
#pragma warning restore CA2000
        _isBackgroundSession = true;
        _activeSessions.TryRegister(SessionId, title ?? SessionId, bgCts);
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

            var pooled = await _sessionPool.GetOrCreateAsync(SessionId, ct: token).ConfigureAwait(false);

            if (Connections.Count > 0)
            {
                var selected = Connections.Where(c => c.IsSelected).Select(c => c.Name).ToList();
                var allSelected = selected.Count == Connections.Count;
                pooled.Config.AllowedMcpServers = allSelected ? null : selected;
                await _sessionStore.SetMcpConnectionsAsync(SessionId, pooled.Config.AllowedMcpServers, ownerUserId: App.SovrantUserId, ct: token)
                    .ConfigureAwait(false);
            }

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
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSending = false;
                assistantMsg.CompleteStreaming();
                TurnCompleted?.Invoke();
            });
        }
    }

    /// <summary>
    /// Called by MainViewModel when the user navigates back to a parked background chat.
    /// Replays buffered events and re-subscribes to live events.
    /// </summary>
    public void ReAttachToBackground()
    {
        if (!_activeSessions.HasSession(SessionId)) return;
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

        if (_isBackgroundSession && _activeSessions.HasSession(SessionId))
        {
            _activeSessions.Cancel(SessionId);
            _isBackgroundSession = false;
            return;
        }

    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_isBackgroundSession && _activeSessions.HasSession(SessionId))
                _activeSessions.Detach(SessionId);

            if (_confirmationHandler is not null)
                _confirmationHandler.ConfirmationRequested -= OnConfirmationRequested;
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
