using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sovrant.Desktop.Views.Dialogs;

public partial class DeleteConfirmDialog : Window
{
    private string _requiredText = string.Empty;
    private bool _requireTypeName;

    public DeleteConfirmDialog() => InitializeComponent();

    public DeleteConfirmDialog(string itemType, string itemName, bool requireTypeName = true) : this()
    {
        _requireTypeName = requireTypeName;
        _requiredText = itemName;

        HeadingText.Text = $"Delete {itemType}";
        DeleteButton.Content = $"Delete {itemType}";

        if (requireTypeName)
        {
            BodyText.Text = $"This will permanently delete \"{itemName}\" and all of its data. This action cannot be undone.";
            HintText.Text = $"Type \"{itemName}\" to confirm:";
            ConfirmInput.Watermark = itemName;
        }
        else
        {
            BodyText.Text = $"Are you sure you want to delete \"{itemName}\"? This action cannot be undone.";
            HintText.IsVisible = false;
            ConfirmInput.IsVisible = false;
            DeleteButton.IsEnabled = true;
        }
    }

    private void OnInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_requireTypeName) return;
        DeleteButton.IsEnabled = ConfirmInput.Text == _requiredText;
    }

    private void OnDelete(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
