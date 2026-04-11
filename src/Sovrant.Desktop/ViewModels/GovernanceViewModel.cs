using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Governance;

namespace Sovrant.Desktop.ViewModels;

public partial class GovernanceViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _governanceLevel = string.Empty;

    [ObservableProperty]
    private bool _auditLogEnabled;

    public ObservableCollection<string> BlockedCommands { get; } = [];
    public ObservableCollection<string> ProtectedFiles { get; } = [];
    public ObservableCollection<string> SecretPatterns { get; } = [];

    public GovernanceViewModel()
    {
        LoadConfig();
    }

    [RelayCommand]
    private void Refresh() => LoadConfig();

    private void LoadConfig()
    {
        var config = GovernanceConfig.Load();

        GovernanceLevel = config.Level.ToString();
        AuditLogEnabled = config.AuditLog;

        BlockedCommands.Clear();
        foreach (var cmd in config.BlockedCommands)
            BlockedCommands.Add(cmd);

        ProtectedFiles.Clear();
        foreach (var f in config.ProtectedFiles)
            ProtectedFiles.Add(f);

        SecretPatterns.Clear();
        foreach (var p in config.SecretPatterns)
            SecretPatterns.Add(p);
    }
}
