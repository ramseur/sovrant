using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views;

/// <summary>Phase 133 — window-wide host for the conversation-folder dialogs.</summary>
public partial class SessionFolderDialogOverlay : UserControl
{
    public SessionFolderDialogOverlay()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SessionFolderDialogViewModel vm)
            {
                vm.PropertyChanged += (_, e) =>
                {
                    // Focus the name box when the prompt opens.
                    if (e.PropertyName == nameof(SessionFolderDialogViewModel.IsNameOpen) && vm.IsNameOpen)
                        Dispatcher.UIThread.Post(() => NameInput.Focus(), DispatcherPriority.Input);
                };
            }
        };
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SessionFolderDialogViewModel vm)
            vm.CloseCommand.Execute(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is SessionFolderDialogViewModel vm)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
