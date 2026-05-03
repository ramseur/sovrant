using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using Sovrant.Desktop.ViewModels;

namespace Sovrant.Desktop.Views.Shared;

public partial class MarkdownEditorView : UserControl
{
    private TextEditor? _editor;
    private MarkdownEditorViewModel? _vm;
    private bool _suppressEditorEvent;
    private bool _suppressVmEvent;

    public MarkdownEditorView()
    {
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("Editor");
        if (_editor is not null)
        {
            _editor.TextChanged += OnEditorTextChanged;
        }
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as MarkdownEditorViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            SyncEditorFromVm();
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MarkdownEditorViewModel.Source))
            SyncEditorFromVm();
    }

    private void SyncEditorFromVm()
    {
        if (_editor is null || _vm is null) return;
        if (_suppressVmEvent) return;
        if (_editor.Text == _vm.Source) return;

        _suppressEditorEvent = true;
        try
        {
            _editor.Text = _vm.Source;
        }
        finally
        {
            _suppressEditorEvent = false;
        }
    }

    private void OnEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_suppressEditorEvent || _vm is null || _editor is null) return;
        _suppressVmEvent = true;
        try
        {
            _vm.Source = _editor.Text;
        }
        finally
        {
            _suppressVmEvent = false;
        }
    }
}
