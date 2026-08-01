using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private string _detailText = string.Empty;

    /// <summary>When true, the detail pane renders markdown; when false, plain text.</summary>
    [ObservableProperty]
    private bool _isMarkdownPreview;

    public bool IsSelectedFolder => SelectedArtifact?.IsFolder ?? false;
    public bool IsSelectedFile => !(SelectedArtifact?.IsFolder ?? false);

    public ObservableCollection<ArtifactItemViewModel> FilteredArtifacts { get; } = [];

    public ArtifactsViewModel(IArtifactStore store)
    {
        _store = store;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void SelectArtifact(ArtifactItemViewModel artifact) => SelectedArtifact = artifact;

    [RelayCommand(CanExecute = nameof(CanDownloadRunZip))]
    private async Task DownloadRunZipAsync()
    {
        if (SelectedArtifact is null) return;
        var root = (_store as LocalArtifactStore)?.Root;
        if (root is null) return;

        // WorkspaceId and ProjectId already hold the raw segment names (may include __name suffix)
        var runDir = Path.GetFullPath(Path.Combine(root,
            SelectedArtifact.WorkspaceId,
            SelectedArtifact.ProjectId,
            SelectedArtifact.RunId));
        if (!Directory.Exists(runDir)) return;

        var downloadsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloadsDir))
            downloadsDir = Path.GetTempPath();

        var zipPath = Path.Combine(downloadsDir, $"{SanitizeForFilename(SelectedArtifact.RunId)}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);

        await Task.Run(() =>
            System.IO.Compression.ZipFile.CreateFromDirectory(
                runDir, zipPath,
                System.IO.Compression.CompressionLevel.Fastest,
                includeBaseDirectory: false))
            .ConfigureAwait(false);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{zipPath}\"",
            UseShellExecute = true,
        });
    }

    private bool CanDownloadRunZip =>
        SelectedArtifact is not null && !string.IsNullOrEmpty(SelectedArtifact.RunId);

    [RelayCommand(CanExecute = nameof(CanOpenRunFolder))]
    private void OpenRunFolder()
    {
        if (SelectedArtifact is null) return;
        var root = (_store as LocalArtifactStore)?.Root;
        if (root is null) return;

        var runDir = Path.GetFullPath(Path.Combine(root,
            SelectedArtifact.WorkspaceId,
            SelectedArtifact.ProjectId,
            SelectedArtifact.RunId));

        if (!Directory.Exists(runDir)) return;

        Process.Start(new ProcessStartInfo { FileName = runDir, UseShellExecute = true });
    }

    private bool CanOpenRunFolder =>
        SelectedArtifact is not null && !string.IsNullOrEmpty(SelectedArtifact.RunId);

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        _allArtifacts.Clear();
        FilteredArtifacts.Clear();

        try
        {
            // Scan the entire artifact root so we find artifacts across ALL workspaces.
            var root = (_store as LocalArtifactStore)?.Root;
            if (root is null || !Directory.Exists(root))
                return;

            var grouped = await Task.Run(() =>
            {
                var rawItems = new List<ArtifactItemViewModel>();
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    var fileName = Path.GetFileName(file);
                    if (string.Equals(fileName, "_manifest.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
                    var info = new FileInfo(file);

                    var segments = relativePath.Split('/');
                    if (segments.Length < 4) continue;

                    var workspaceId = segments[0];
                    var projectId = segments[1];
                    var runId = segments[2];
                    var artifactPath = string.Join('/', segments[3..]);

                    rawItems.Add(new ArtifactItemViewModel
                    {
                        FullDiskPath = file,
                        RelativeFromRoot = relativePath,
                        Path = artifactPath,
                        FileName = fileName,
                        Extension = Path.GetExtension(file).TrimStart('.'),
                        SizeBytes = info.Length,
                        SizeDisplay = FormatSize(info.Length),
                        ContentType = GuessContentType(file),
                        LastModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                        WorkspaceId = workspaceId,
                        ProjectId = projectId,
                        RunId = runId,
                    });
                }

                // Group multi-file runs into folder items.
                var result = new List<ArtifactItemViewModel>();
                foreach (var runGroup in rawItems.GroupBy(a => a.RunId))
                {
                    var files = runGroup.ToList();
                    if (files.Count > 1)
                    {
                        var firstPath = files.OrderBy(f => f.LastModified).First().Path;
                        var folderName = firstPath.Contains('/', StringComparison.Ordinal)
                            ? firstPath.Split('/')[0]
                            : runGroup.Key;
                        var totalBytes = files.Sum(f => f.SizeBytes);
                        var folder = new ArtifactItemViewModel
                        {
                            RunId = runGroup.Key,
                            IsFolder = true,
                            FolderName = folderName,
                            FileName = folderName,
                            SizeBytes = totalBytes,
                            SizeDisplay = FormatSize(totalBytes),
                            LastModified = files.Max(f => f.LastModified),
                            WorkspaceId = files[0].WorkspaceId,
                            ProjectId = files[0].ProjectId,
                        };
                        foreach (var f in files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
                            folder.FolderFiles.Add(f);
                        result.Add(folder);
                    }
                    else
                    {
                        result.Add(files[0]);
                    }
                }

                result.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));
                return result;
            }).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var item in grouped)
                    _allArtifacts.Add(item);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TotalCount = _allArtifacts.Count;
                ApplyFilter();
                IsLoading = false;
            });
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedArtifactChanged(ArtifactItemViewModel? value)
    {
        OnPropertyChanged(nameof(IsSelectedFolder));
        OnPropertyChanged(nameof(IsSelectedFile));
        OpenRunFolderCommand.NotifyCanExecuteChanged();
        DownloadRunZipCommand.NotifyCanExecuteChanged();

        if (value is null || value.IsFolder)
        {
            DetailText = string.Empty;
            IsMarkdownPreview = false;
            return;
        }

        _ = LoadPreviewAsync(value);
    }

    private async Task LoadPreviewAsync(ArtifactItemViewModel artifact)
    {
        var isMarkdown = string.Equals(artifact.Extension, "MD", StringComparison.OrdinalIgnoreCase);

        // For markdown files: render the file content as formatted markdown
        if (isMarkdown && artifact.SizeBytes < 512 * 1024 && File.Exists(artifact.FullDiskPath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(artifact.FullDiskPath).ConfigureAwait(false);

                if (content.Length > 20000)
                    content = content[..20000] + "\n\n*... (truncated at 20,000 chars)*";

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsMarkdownPreview = true;
                    DetailText = content;
                });
                return;
            }
            catch
            {
                // Fall through to plain text
            }
        }

        // For non-markdown: show metadata header + plain text content
        var sb = new StringBuilder();
        sb.AppendLine(artifact.FileName);
        sb.AppendLine(new string('=', artifact.FileName.Length));
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Path:      {artifact.Path}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Size:      {artifact.SizeDisplay}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Type:      {artifact.ContentType}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Modified:  {artifact.LastModified:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Workspace: {artifact.WorkspaceId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Project:   {artifact.ProjectId}");

        if (!string.IsNullOrEmpty(artifact.RunId))
            sb.AppendLine(CultureInfo.InvariantCulture, $"Run:       {artifact.RunId}");

        sb.AppendLine();
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();

        if (IsTextFile(artifact.Extension) && artifact.SizeBytes < 512 * 1024)
        {
            try
            {
                if (File.Exists(artifact.FullDiskPath))
                {
                    var content = await File.ReadAllTextAsync(artifact.FullDiskPath).ConfigureAwait(false);

                    if (content.Length > 20000)
                        content = content[..20000] + "\n\n... (truncated at 20,000 chars)";

                    sb.Append(content);
                }
            }
            catch
            {
                sb.AppendLine("(Preview unavailable)");
            }
        }
        else if (artifact.SizeBytes >= 512 * 1024)
        {
            sb.AppendLine("(File too large to preview)");
        }
        else
        {
            sb.AppendLine("(Binary file — preview not available)");
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsMarkdownPreview = false;
            DetailText = sb.ToString();
        });
    }

    private void ApplyFilter()
    {
        FilteredArtifacts.Clear();
        var query = SearchText.Trim();

        foreach (var artifact in _allArtifacts)
        {
            if (query.Length > 0)
            {
                bool match = artifact.IsFolder
                    ? artifact.FolderName.Contains(query, StringComparison.OrdinalIgnoreCase)
                      || artifact.FolderFiles.Any(f => f.FileName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    : artifact.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
                      || artifact.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                      || artifact.RunId.Contains(query, StringComparison.OrdinalIgnoreCase)
                      || artifact.ContentType.Contains(query, StringComparison.OrdinalIgnoreCase)
                      || artifact.WorkspaceId.Contains(query, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
            }

            FilteredArtifacts.Add(artifact);
        }
    }

    private static string SanitizeForFilename(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
        return sb.Length == 0 ? "artifacts" : sb.ToString();
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

    [ObservableProperty]
    private string _workspaceId = string.Empty;

    [ObservableProperty]
    private string _projectId = string.Empty;

    [ObservableProperty]
    private bool _isFolder;

    [ObservableProperty]
    private string _folderName = string.Empty;

    public ObservableCollection<ArtifactItemViewModel> FolderFiles { get; } = [];

    /// <summary>Full path on disk for preview reading.</summary>
    public string FullDiskPath { get; init; } = string.Empty;

    /// <summary>Path relative to the artifact store root.</summary>
    public string RelativeFromRoot { get; init; } = string.Empty;

    [RelayCommand]
    private void OpenFile()
    {
        if (!string.IsNullOrEmpty(FullDiskPath) && File.Exists(FullDiskPath))
            Process.Start(new ProcessStartInfo { FileName = FullDiskPath, UseShellExecute = true });
    }
}
