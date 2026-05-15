using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sovrant.Desktop.Views.Dialogs;

public partial class UserInputDialog : Window
{
    public UserInputDialog()
    {
        InitializeComponent();
    }

    public UserInputDialog(string question) : this()
    {
        QuestionText.Text = question;
    }

    private void OnSubmit(object? sender, RoutedEventArgs e) => Close(ResponseInput.Text ?? string.Empty);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(string.Empty);
}
