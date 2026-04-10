using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Desktop.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IRuntimeSessionPool _sessionPool;
    private readonly string _sessionId = $"desktop-{Guid.NewGuid():N}";

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private int _tokenCount;

    [ObservableProperty]
    private bool _hasMessages;

    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    public ChatViewModel(IRuntimeSessionPool sessionPool)
    {
        _sessionPool = sessionPool;
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

        // Add assistant placeholder.
        var assistantMsg = new MessageViewModel { Role = "assistant" };
        Messages.Add(assistantMsg);

        try
        {
            var pooled = await _sessionPool.GetOrCreateAsync(_sessionId, ct: ct).ConfigureAwait(false);
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
        }
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
                msg.AddToolUse(t.ToolName, t.ToolUseId);
                break;

            case RuntimeEvent.ToolResult t:
                msg.UpdateToolResult(t.ToolUseId, t.Content, t.IsError);
                break;

            case RuntimeEvent.TurnComplete t:
                TokenCount += t.InputTokens + t.OutputTokens;
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
