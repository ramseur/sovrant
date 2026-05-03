using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sovrant.Desktop.ViewModels;

public sealed partial class MarkdownEditorViewModel : ViewModelBase, IDisposable
{
    private static readonly char[] _wordSeparators = { ' ', '\t', '\r', '\n' };

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private string _previewMarkdown = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _showPreview = true;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private int _wordCount;

    private string _originalSource = string.Empty;
    private CancellationTokenSource? _debounceCts;

    public event EventHandler<string>? Saved;
    public event EventHandler? Cancelled;

    public void Load(string source, bool readOnly = false, string? title = null)
    {
        _originalSource = source ?? string.Empty;
        IsReadOnly = readOnly;
        Title = title;
        Source = _originalSource;
        IsDirty = false;
        Validate(Source);
        PreviewMarkdown = Source;
        WordCount = CountWords(Source);
    }

    partial void OnSourceChanged(string value)
    {
        IsDirty = !string.Equals(value, _originalSource, StringComparison.Ordinal);
        Validate(value);
        WordCount = CountWords(value);
        _ = SchedulePreview(value);
    }

    private async Task SchedulePreview(string value)
    {
        if (_debounceCts is not null)
            await _debounceCts.CancelAsync();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        try
        {
            await Task.Delay(200, token);
            if (!token.IsCancellationRequested)
                PreviewMarkdown = value;
        }
        catch (TaskCanceledException) { }
    }

    private void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ValidationError = null;
            return;
        }
        if (!value.StartsWith("---", StringComparison.Ordinal))
        {
            ValidationError = "Missing YAML frontmatter (must start with ---)";
            return;
        }
        var endIdx = value.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIdx < 0)
        {
            ValidationError = "Frontmatter is not closed (missing trailing ---)";
            return;
        }
        var fm = value.Substring(3, endIdx - 3);
        var hasName = false;
        foreach (var line in fm.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                var nameValue = trimmed.Substring(5).Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(nameValue))
                {
                    hasName = true;
                    break;
                }
            }
        }
        ValidationError = hasName ? null : "Frontmatter must include a non-empty 'name' field";
    }

    private static int CountWords(string value) =>
        string.IsNullOrWhiteSpace(value) ? 0 :
        value.Split(_wordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    private void Save()
    {
        if (IsReadOnly || ValidationError is not null) return;
        Saved?.Invoke(this, Source);
        _originalSource = Source;
        IsDirty = false;
    }

    [RelayCommand]
    private void Cancel()
    {
        Source = _originalSource;
        IsDirty = false;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void TogglePreview() => ShowPreview = !ShowPreview;
}
