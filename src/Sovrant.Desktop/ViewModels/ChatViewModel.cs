using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;

namespace Sovrant.Desktop.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IRuntimeSessionPool _sessionPool;
    private readonly ISessionStore _sessionStore;
    private readonly DesktopConfirmationHandler? _confirmationHandler;

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

    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    public ChatViewModel(IRuntimeSessionPool sessionPool, ISessionStore sessionStore,
        DesktopConfirmationHandler? confirmationHandler = null)
    {
        _sessionPool = sessionPool;
        _sessionStore = sessionStore;
        _confirmationHandler = confirmationHandler;
        _sessionId = $"desktop-{Guid.NewGuid():N}";

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
        IsSending = true;
        HasMessages = true;

        // Add user message.
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
                assistantMsg.AppendText($"\n\n**Error:** {ex.Message}"));
        }
        finally
        {
            IsSending = false;
            await Dispatcher.UIThread.InvokeAsync(() => assistantMsg.CompleteStreaming());
        }
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        HasMessages = false;
        TokenCount = 0;
        SessionId = $"desktop-{Guid.NewGuid():N}";
    }

    [RelayCommand]
    private void Suggestion(string text)
    {
        InputText = text;
        if (SendCommand.CanExecute(null))
            SendCommand.Execute(null);
    }

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);

    partial void OnInputTextChanged(string value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsSendingChanged(bool value) => SendCommand.NotifyCanExecuteChanged();

    private void HandleEvent(RuntimeEvent ev, MessageViewModel msg)
    {
        switch (ev)
        {
            case RuntimeEvent.TextChunk { Text: var chunk }:
                msg.AppendText(chunk);
                break;

            case RuntimeEvent.ToolUseRequested t:
                if (msg.IsThinking) msg.StopThinking();
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
                msg.AppendText($"\n\n**Error:** {errMsg}");
                break;

            case RuntimeEvent.PermissionDenied { ToolName: var tool, Reason: var reason }:
                msg.AppendText($"\n\n*Permission denied for {tool}: {reason}*");
                break;
        }
    }
}
