using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sovrant.Desktop.Views.Dialogs;

public partial class ConfirmToolDialog : Window
{
    public ConfirmToolDialog()
    {
        InitializeComponent();
    }

    public ConfirmToolDialog(string toolName, string input) : this()
    {
        ToolNameText.Text = toolName;
        InputText.Text = input;
    }

    private void OnAllow(object? sender, RoutedEventArgs e) => Close(true);
    private void OnDeny(object? sender, RoutedEventArgs e) => Close(false);
}
