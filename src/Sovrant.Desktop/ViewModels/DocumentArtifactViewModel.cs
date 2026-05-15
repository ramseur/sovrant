using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Phase 66 — single document artifact rendered inline under a tool-use
/// row in the chat view. Backed by the JSON returned from
/// <c>DocumentGenerate</c> / <c>DocumentFromTemplate</c> / <c>DocumentPackage</c>.
/// </summary>
public partial class DocumentArtifactViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _format = string.Empty;

    [ObservableProperty]
    private string _sizeText = string.Empty;

    [ObservableProperty]
    private string _icon = "\uD83D\uDCCE";

    [ObservableProperty]
    private string? _accessUrl;

    [ObservableProperty]
    private string? _localPath;

    [ObservableProperty]
    private string? _templateId;

    [RelayCommand]
    private void Open()
    {
        var target = LocalPath ?? AccessUrl;
        if (string.IsNullOrWhiteSpace(target)) return;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort; swallow so the chat view stays usable.
        }
    }

    [RelayCommand]
    private void RevealInFolder()
    {
        if (string.IsNullOrWhiteSpace(LocalPath)) return;
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start("explorer.exe", $"/select,\"{LocalPath}\"");
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", $"-R \"{LocalPath}\"");
            else
                Process.Start(new ProcessStartInfo(System.IO.Path.GetDirectoryName(LocalPath) ?? ".")
                {
                    UseShellExecute = true,
                });
        }
        catch
        {
            // Best-effort.
        }
    }
}
