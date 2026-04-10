using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Desktop.ViewModels;

public partial class ArtifactsViewModel : ViewModelBase
{
    private readonly IArtifactStore _store;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ArtifactItemViewModel? _selectedArtifact;

    public ObservableCollection<ArtifactItemViewModel> Artifacts { get; } = [];

    public ArtifactsViewModel(IArtifactStore store)
    {
        _store = store;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        Artifacts.Clear();

        try
        {
            var scope = new ArtifactScope();
            await foreach (var entry in _store.ListAsync(scope))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    Artifacts.Add(new ArtifactItemViewModel
                    {
                        Path = entry.RelativePath,
                        SizeBytes = entry.SizeBytes,
                        SizeDisplay = FormatSize(entry.SizeBytes),
                        ContentType = entry.ContentType ?? "unknown",
                        LastModified = entry.LastModified,
                        RunId = entry.RunId ?? "—",
                    }));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
    };
}

public partial class ArtifactItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _path = string.Empty;

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
}
