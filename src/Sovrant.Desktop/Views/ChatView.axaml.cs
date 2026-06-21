using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        // Tunnel phase fires before the TextBox's own AcceptsReturn processing,
        // ensuring Enter sends and Shift+Enter inserts a newline.
        InputBox.AddHandler(KeyDownEvent, OnInputKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // No-op: MCP selection is managed globally by ActiveContextViewModel.
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not ChatViewModel vm)
            return;

        // Escape — stop current turn
        if (e.Key == Key.Escape && vm.IsSending)
        {
            vm.StopCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Ctrl+L — clear chat
        if (ctrl && !shift && e.Key == Key.L)
        {
            vm.ClearChatCommand.Execute(null);
            InputBox?.Focus();
            e.Handled = true;
            return;
        }

        // Ctrl+N — new session (same as clear)
        if (ctrl && !shift && e.Key == Key.N)
        {
            vm.ClearChatCommand.Execute(null);
            InputBox?.Focus();
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+C — copy last code block from assistant messages
        if (ctrl && shift && e.Key == Key.C)
        {
            _ = CopyLastCodeBlockAsync(vm);
            e.Handled = true;
        }
    }

    private async Task CopyLastCodeBlockAsync(ChatViewModel vm)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        // Walk messages in reverse to find the last code block in an assistant message.
        for (var i = vm.Messages.Count - 1; i >= 0; i--)
        {
            var msg = vm.Messages[i];
            if (msg.Role != "assistant" || string.IsNullOrEmpty(msg.Text))
                continue;

            var match = Regex.Match(msg.Text, @"```[\w]*\r?\n([\s\S]*?)```", RegexOptions.RightToLeft);
            if (match.Success)
            {
                await clipboard.SetValueAsync(DataFormat.Text,match.Groups[1].Value.TrimEnd());
                return;
            }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ChatViewModel vm)
        {
            vm.Messages.CollectionChanged += OnMessagesChanged;
            // Also scroll when text is appended to the last message.
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ChatViewModel.IsSending))
                    ScrollToBottom();
            };
            vm.TurnCompleted += () => InputBox?.Focus();
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (MessageScroller is not null)
        {
            MessageScroller.ScrollToEnd();
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Shift+Enter: insert a newline at the caret.
            if (sender is TextBox tb)
            {
                var pos = tb.CaretIndex;
                var text = tb.Text ?? string.Empty;
                tb.Text = text.Insert(pos, "\n");
                tb.CaretIndex = pos + 1;
            }
        }
        else
        {
            // Enter: send the message.
            if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
            }
        }

        e.Handled = true;
    }

    private async void OnCopyMessageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: MessageViewModel msg } &&
            TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetValueAsync(DataFormat.Text,msg.Text);
        }
    }

    private async void OnCopyMessageButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MessageViewModel msg } &&
            TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetValueAsync(DataFormat.Text,msg.Text);
        }
    }
}
