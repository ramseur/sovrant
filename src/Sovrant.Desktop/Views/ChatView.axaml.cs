using Avalonia.Controls;
using Avalonia.Input;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
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
}
