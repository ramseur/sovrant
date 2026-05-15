using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sovrant.Desktop.ViewModels;

public sealed partial class MarkdownEditorViewModel : ViewModelBase
{
    private static readonly char[] _wordSeparators = { ' ', '\t', '\r', '\n' };

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private int _wordCount;

    private string _originalSource = string.Empty;

    public event EventHandler<string>? Saved;
    public event EventHandler? Cancelled;

    public void Load(string source, string? title = null)
    {
        _originalSource = source ?? string.Empty;
        Title = title;
        Source = _originalSource;
        IsDirty = false;
        Validate(Source);
        WordCount = CountWords(Source);
    }

    partial void OnSourceChanged(string value)
    {
        IsDirty = !string.Equals(value, _originalSource, StringComparison.Ordinal);
        Validate(value);
        WordCount = CountWords(value);
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

    [RelayCommand]
    private void Save()
    {
        if (ValidationError is not null) return;
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
}
