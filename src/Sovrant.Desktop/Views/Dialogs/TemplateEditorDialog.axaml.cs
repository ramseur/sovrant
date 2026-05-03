using Avalonia.Controls;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views.Dialogs;

public partial class TemplateEditorDialog : Window
{
    private readonly MarkdownEditorViewModel? _vm;
    private string? _result;

    public TemplateEditorDialog()
    {
        InitializeComponent();
    }

    public TemplateEditorDialog(MarkdownEditorViewModel vm) : this()
    {
        _vm = vm;
        DataContext = vm;
        vm.Saved += OnSaved;
        vm.Cancelled += OnCancelled;
        Closed += OnClosed;
    }

    private void OnSaved(object? sender, string source)
    {
        _result = source;
        Close(source);
    }

    private void OnCancelled(object? sender, System.EventArgs e) => Close(null);

    private void OnClosed(object? sender, System.EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.Saved -= OnSaved;
            _vm.Cancelled -= OnCancelled;
            _vm.Dispose();
        }
    }

    /// <summary>Result is null when cancelled, or the saved markdown source.</summary>
    public string? Result => _result;
}
