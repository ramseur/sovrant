using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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
            await clipboard.SetTextAsync(msg.Text);
        }
    }
}
