using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Desktop.ViewModels;

public partial class ArtifactsViewModel : ViewModelBase
{
    private readonly IArtifactStore _store;
    private readonly List<ArtifactItemViewModel> _allArtifacts = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private ArtifactItemViewModel? _selectedArtifact;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    public ObservableCollection<ArtifactItemViewModel> FilteredArtifacts { get; } = [];

    public ArtifactsViewModel(IArtifactStore store)
    {
        _store = store;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void SelectArtifact(ArtifactItemViewModel artifact) => SelectedArtifact = artifact;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        _allArtifacts.Clear();
        FilteredArtifacts.Clear();

        try
        {
            var scope = new ArtifactScope();
            await foreach (var entry in _store.ListAsync(scope))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _allArtifacts.Add(new ArtifactItemViewModel
                    {
                        Path = entry.RelativePath,
                        FileName = System.IO.Path.GetFileName(entry.RelativePath),
                        Extension = System.IO.Path.GetExtension(entry.RelativePath).TrimStart('.'),
                        SizeBytes = entry.SizeBytes,
                        SizeDisplay = FormatSize(entry.SizeBytes),
                        ContentType = entry.ContentType ?? GuessContentType(entry.RelativePath),
                        LastModified = entry.LastModified,
                        RunId = entry.RunId ?? "",
                    }));
            }
        }
        finally
        {
            TotalCount = _allArtifacts.Count;
            ApplyFilter();
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedArtifactChanged(ArtifactItemViewModel? value)
    {
        if (value is null)
        {
            DetailMarkdown = string.Empty;
            return;
        }

        _ = LoadPreviewAsync(value);
    }

    private async Task LoadPreviewAsync(ArtifactItemViewModel artifact)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {artifact.FileName}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Path:** `{artifact.Path}`");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Size:** {artifact.SizeDisplay}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Type:** {artifact.ContentType}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Modified:** {artifact.LastModified:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(artifact.RunId))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Run:** `{artifact.RunId}`");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();

        // Try to read and preview text content
        if (IsTextFile(artifact.Extension) && artifact.SizeBytes < 512 * 1024) // < 512KB
        {
            try
            {
                var artifactRoot = (_store as LocalArtifactStore)?.Root;
                if (artifactRoot is not null)
                {
                    var fullPath = System.IO.Path.Combine(artifactRoot, artifact.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    if (File.Exists(fullPath))
                    {
                        var content = await File.ReadAllTextAsync(fullPath);

                        // Truncate very long files
                        if (content.Length > 10000)
                            content = content[..10000] + "\n\n... (truncated)";

                        var lang = GetLanguage(artifact.Extension);
                        sb.AppendLine("## Preview");
                        sb.AppendLine();
                        sb.AppendLine(CultureInfo.InvariantCulture, $"```{lang}");
                        sb.AppendLine(content);
                        sb.AppendLine("```");
                    }
                }
            }
            catch
            {
                sb.AppendLine("*Preview unavailable*");
            }
        }
        else if (artifact.SizeBytes >= 512 * 1024)
        {
            sb.AppendLine("*File too large to preview*");
        }
        else
        {
            sb.AppendLine("*Binary file — preview not available*");
        }

        artifact.Markdown = sb.ToString();
        DetailMarkdown = artifact.Markdown;
        // Force ContentControl to recreate MarkdownScrollViewer
        var current = SelectedArtifact;
        SelectedArtifact = null;
        SelectedArtifact = current;
    }

    private void ApplyFilter()
    {
        FilteredArtifacts.Clear();
        var query = SearchText.Trim();

        foreach (var artifact in _allArtifacts)
        {
            if (query.Length > 0
                && !artifact.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !artifact.RunId.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !artifact.ContentType.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredArtifacts.Add(artifact);
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
    };

    private static string GuessContentType(string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        return ext.ToUpperInvariant() switch
        {
            ".MD" => "text/markdown",
            ".JSON" => "application/json",
            ".CS" => "text/x-csharp",
            ".JS" => "text/javascript",
            ".TS" => "text/typescript",
            ".PY" => "text/x-python",
            ".XML" => "text/xml",
            ".YAML" or ".YML" => "text/yaml",
            ".HTML" or ".HTM" => "text/html",
            ".CSS" => "text/css",
            ".TXT" or ".LOG" => "text/plain",
            ".CSV" => "text/csv",
            ".SQL" => "text/x-sql",
            _ => "application/octet-stream",
        };
    }

    private static bool IsTextFile(string extension)
    {
        return extension.ToUpperInvariant() switch
        {
            "MD" or "JSON" or "CS" or "JS" or "TS" or "PY" or "XML" or "YAML" or "YML"
            or "HTML" or "HTM" or "CSS" or "TXT" or "LOG" or "CSV" or "SQL" or "SH"
            or "BAT" or "PS1" or "CSPROJ" or "SLN" or "AXAML" or "XAML" or "RAZOR"
            or "RST" or "TOML" or "INI" or "CFG" or "CONF" or "ENV" => true,
            _ => false,
        };
    }

    private static string GetLanguage(string extension)
    {
        return extension.ToUpperInvariant() switch
        {
            "CS" => "csharp",
            "JS" => "javascript",
            "TS" => "typescript",
            "PY" => "python",
            "MD" => "markdown",
            "JSON" => "json",
            "XML" or "AXAML" or "XAML" or "CSPROJ" => "xml",
            "HTML" or "HTM" or "RAZOR" => "html",
            "CSS" => "css",
            "SQL" => "sql",
            "YAML" or "YML" => "yaml",
            "SH" => "bash",
            "PS1" => "powershell",
            "BAT" => "batch",
            _ => "",
        };
    }
}

public partial class ArtifactItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _extension = string.Empty;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private string _sizeDisplay = string.Empty;

    [ObservableProperty]
    private string _contentType = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _lastModified;

    [ObservableProperty]
    private string _runId = string.Empty;

    public string Markdown { get; set; } = string.Empty;
}
