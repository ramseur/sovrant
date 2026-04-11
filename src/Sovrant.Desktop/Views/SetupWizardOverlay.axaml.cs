using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Sovrant.Desktop.Views;

public partial class SetupWizardOverlay : UserControl
{
    public SetupWizardOverlay()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Open the dropdown when the model box gets focus so users can browse all models.
        if (this.FindControl<AutoCompleteBox>("ModelBox") is { } box)
        {
            box.GotFocus += OnModelBoxGotFocus;
        }
    }

    private static void OnModelBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is AutoCompleteBox acb)
        {
            // Setting the text to itself with MinimumPrefixLength=0 triggers the popup.
            acb.IsDropDownOpen = true;
        }
    }
}
