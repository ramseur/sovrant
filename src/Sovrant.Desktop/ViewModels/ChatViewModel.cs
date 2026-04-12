using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Commands;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;

namespace Sovrant.Desktop.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IRuntimeSessionPool _sessionPool;
    private readonly ISessionStore _sessionStore;
    private readonly SlashCommandDispatcher _commandDispatcher;
    private readonly DesktopConfirmationHandler? _confirmationHandler;
    private readonly ActiveContextViewModel _activeContext;

    [ObservableProperty]
    private string _sessionId;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private int _tokenCount;

    [ObservableProperty]
    private bool _hasMessages;

    [ObservableProperty]
    private bool _showCommandSuggestions;

    public ActiveContextViewModel ActiveContext => _activeContext;
    public ObservableCollection<MessageViewModel> Messages { get; } = [];
    public ObservableCollection<CommandSuggestion> CommandSuggestions { get; } = [];

    /// <summary>Raised after a turn completes (message sent and response received).</summary>
    public event Action? TurnCompleted;

    public ChatViewModel(IRuntimeSessionPool sessionPool, ISessionStore sessionStore,
        SlashCommandDispatcher commandDispatcher, ActiveContextViewModel activeContext,
        DesktopConfirmationHandler? confirmationHandler = null)
    {
        _sessionPool = sessionPool;
        _sessionStore = sessionStore;
        _commandDispatcher = commandDispatcher;
        _confirmationHandler = confirmationHandler;
        _activeContext = activeContext;
        _sessionId = $"session-{Guid.NewGuid():N}";

        if (_confirmationHandler is not null)
            _confirmationHandler.ConfirmationRequested += OnConfirmationRequested;
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

        var entries = await _sessionStore.LoadAsync(sessionId, ct: ct);
        foreach (var entry in entries)
        {
            if (entry.Role is "user" or "assistant")
            {
                Messages.Add(new MessageViewModel { Role = entry.Role, Text = entry.Content, IsComplete = true });
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
        Messages.Add(new MessageViewModel { Role = "user", Text = text });

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
        IsSending = true;
        HasMessages = true;

        // Add user message only if not already added (slash command inject path).
        if (Messages.Count == 0 || Messages[^1].Role != "user" || Messages[^1].Text != text)
            Messages.Add(new MessageViewModel { Role = "user", Text = text });

        // Add assistant placeholder with thinking indicator.
        var assistantMsg = new MessageViewModel { Role = "assistant" };
        assistantMsg.StartThinking();
        Messages.Add(assistantMsg);

        try
        {
            // Wait for runtime initialization (DB migrations, model metadata) before first send.
            await App.RuntimeReady.Task.WaitAsync(ct).ConfigureAwait(false);

            var pooled = await _sessionPool.GetOrCreateAsync(SessionId, ct: ct).ConfigureAwait(false);
            await foreach (var ev in pooled.Runtime.RunTurnAsync(text, ct))
            {
                await Dispatcher.UIThread.InvokeAsync(() => HandleEvent(ev, assistantMsg));
            }
        }
        catch (OperationCanceledException) { /* expected */ }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                assistantMsg.SetError(ex.Message));
        }
        finally
        {
            IsSending = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                assistantMsg.CompleteStreaming();
                TurnCompleted?.Invoke();
            });
        }
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
                msg.CompleteStreaming();
                break;

            case RuntimeEvent.RuntimeError { Message: var errMsg }:
                msg.SetError(errMsg);
                break;

            case RuntimeEvent.PermissionDenied { ToolName: var tool, Reason: var reason }:
                msg.AppendText($"\n\n*Permission denied for {tool}: {reason}*");
                break;

            // ── Phase 59 events ─────────────────────────────────────────
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
