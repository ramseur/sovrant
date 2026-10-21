using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class GovernanceViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _governanceLevel = string.Empty;

    [ObservableProperty]
    private bool _auditLogEnabled;

    /// <summary>Phase 145 stopgap: members may use file and shell tools on a shared Web server.</summary>
    [ObservableProperty]
    private bool _memberFileToolsEnabled;

    /// <summary>Phase 145 Part D: file and shell tools may run on a hosted server (Web, Server) at all.</summary>
    [ObservableProperty]
    private bool _hostFileToolsEnabled;

    // Phase 148: controls the server's environment sets while SOVRANT_ENV_OVERRIDE is on.
    public Sovrant.Runtime.Config.EnvLock AuditLogLock { get; } = Sovrant.Runtime.Config.EnvLock.ForSetting(Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceAuditLog);
    public Sovrant.Runtime.Config.EnvLock HostToolsLock { get; } = Sovrant.Runtime.Config.EnvLock.ForSetting(Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceHostFileTools);
    public Sovrant.Runtime.Config.EnvLock MemberToolsLock { get; } = Sovrant.Runtime.Config.EnvLock.ForSetting(Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceMemberFileTools);
    public Sovrant.Runtime.Config.EnvLock BlockedLock { get; } = Sovrant.Runtime.Config.EnvLock.ForSetting(Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceBlockedCommands);
    public Sovrant.Runtime.Config.EnvLock ProtectedLock { get; } = Sovrant.Runtime.Config.EnvLock.ForSetting(Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceProtectedFiles);
    public Sovrant.Runtime.Config.EnvLock SecretsLock { get; } = Sovrant.Runtime.Config.EnvLock.ForSetting(Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceSecretPatterns);

    [ObservableProperty]
    private string _newBlockedCommand = string.Empty;

    [ObservableProperty]
    private string _newProtectedFile = string.Empty;

    [ObservableProperty]
    private string _newSecretPattern = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<string> BlockedCommands { get; } = [];
    public ObservableCollection<string> ProtectedFiles { get; } = [];
    public ObservableCollection<string> SecretPatterns { get; } = [];

    private readonly IWorkspaceSettingsStore? _settings;
    private readonly LiveSettingsRegistry? _liveSettings;

    public GovernanceViewModel(IWorkspaceSettingsStore? settings = null, LiveSettingsRegistry? liveSettings = null)
    {
        _settings = settings;
        _liveSettings = liveSettings;
        LoadConfig();
    }

    [RelayCommand]
    private void Refresh() => LoadConfig();

    [RelayCommand]
    private void AddBlockedCommand()
    {
        var cmd = NewBlockedCommand.Trim();
        if (string.IsNullOrEmpty(cmd) || BlockedCommands.Contains(cmd)) return;
        BlockedCommands.Add(cmd);
        NewBlockedCommand = string.Empty;
        SaveConfig();
    }

    [RelayCommand]
    private void RemoveBlockedCommand(string command)
    {
        BlockedCommands.Remove(command);
        SaveConfig();
    }

    [RelayCommand]
    private void AddProtectedFile()
    {
        var file = NewProtectedFile.Trim();
        if (string.IsNullOrEmpty(file) || ProtectedFiles.Contains(file)) return;
        ProtectedFiles.Add(file);
        NewProtectedFile = string.Empty;
        SaveConfig();
    }

    [RelayCommand]
    private void RemoveProtectedFile(string file)
    {
        ProtectedFiles.Remove(file);
        SaveConfig();
    }

    [RelayCommand]
    private void AddSecretPattern()
    {
        var pattern = NewSecretPattern.Trim();
        if (string.IsNullOrEmpty(pattern) || SecretPatterns.Contains(pattern)) return;
        SecretPatterns.Add(pattern);
        NewSecretPattern = string.Empty;
        SaveConfig();
    }

    [RelayCommand]
    private void RemoveSecretPattern(string pattern)
    {
        SecretPatterns.Remove(pattern);
        SaveConfig();
    }

    [RelayCommand]
    private void ToggleAuditLog()
    {
        AuditLogEnabled = !AuditLogEnabled;
        SaveConfig();
    }

    [RelayCommand]
    private void ToggleHostFileTools()
    {
        HostFileToolsEnabled = !HostFileToolsEnabled;
        SaveConfig();
    }

    [RelayCommand]
    private void ToggleMemberFileTools()
    {
        MemberFileToolsEnabled = !MemberFileToolsEnabled;
        SaveConfig();
    }

    private void LoadConfig()
    {
        var config = GovernanceConfig.Load(_settings);

        GovernanceLevel = config.Level.ToString();
        AuditLogEnabled = config.AuditLog;
        MemberFileToolsEnabled = config.MemberFileTools;
        HostFileToolsEnabled = config.HostFileTools;

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

    private async void SaveConfig()
    {
        if (_settings is null)
        {
            StatusMessage = "Settings store unavailable.";
            return;
        }

        try
        {
            var config = new GovernanceConfig
            {
                GovernanceLevelName = GovernanceLevel.Equals("STANDARD", StringComparison.OrdinalIgnoreCase) ? "standard"
                    : GovernanceLevel.Equals("STRICT", StringComparison.OrdinalIgnoreCase) ? "strict"
                    : GovernanceLevel.Equals("PERMISSIVE", StringComparison.OrdinalIgnoreCase) ? "permissive"
                    : "standard",
                AuditLog = AuditLogEnabled,
                MemberFileTools = MemberFileToolsEnabled,
                HostFileTools = HostFileToolsEnabled,
            };

            foreach (var cmd in BlockedCommands)
                config.BlockedCommands.Add(cmd);
            foreach (var f in ProtectedFiles)
                config.ProtectedFiles.Add(f);
            foreach (var p in SecretPatterns)
                config.SecretPatterns.Add(p);

            await config.SaveToStoreAsync(_settings).ConfigureAwait(false);
            _liveSettings?.ReloadAll();
            StatusMessage = "Saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }
}
