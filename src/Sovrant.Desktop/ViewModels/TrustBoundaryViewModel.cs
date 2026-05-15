using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.TrustBoundary;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class TrustBoundaryViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private bool _sanitizerEnabled = true;

    [ObservableProperty]
    private string _sanitizerMode = "redact";

    [ObservableProperty]
    private bool _logRedactions;

    [ObservableProperty]
    private bool _intentEnabled = true;

    [ObservableProperty]
    private bool _clarifyAmbiguous = true;

    [ObservableProperty]
    private bool _blockHarmful = true;

    [ObservableProperty]
    private string _newDomain = string.Empty;

    [ObservableProperty]
    private string _newAllow = string.Empty;

    [ObservableProperty]
    private string _newExempt = string.Empty;

    [ObservableProperty]
    private string _newPatternName = string.Empty;

    [ObservableProperty]
    private string _newPatternRegex = string.Empty;

    [ObservableProperty]
    private string _newPatternAction = "redact";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<string> CorporateDomains { get; } = [];
    public ObservableCollection<string> AllowList { get; } = [];
    public ObservableCollection<string> ExemptProviders { get; } = [];
    public ObservableCollection<CustomPattern> CustomPatterns { get; } = [];

    private readonly SovrantConfig _config;
    private readonly IWorkspaceSettingsStore? _settings;
    private readonly LiveSettingsRegistry? _liveSettings;

    public TrustBoundaryViewModel(
        SovrantConfig config,
        IWorkspaceSettingsStore? settings = null,
        LiveSettingsRegistry? liveSettings = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _settings = settings;
        _liveSettings = liveSettings;
        LoadConfig();
    }

    [RelayCommand]
    private void Refresh() => LoadConfig();

    [RelayCommand]
    private void AddDomain() => AddIfNew(CorporateDomains, NewDomain, v => NewDomain = v);

    [RelayCommand]
    private void RemoveDomain(string value) => CorporateDomains.Remove(value);

    [RelayCommand]
    private void AddAllow() => AddIfNew(AllowList, NewAllow, v => NewAllow = v);

    [RelayCommand]
    private void RemoveAllow(string value) => AllowList.Remove(value);

    [RelayCommand]
    private void AddExempt() => AddIfNew(ExemptProviders, NewExempt, v => NewExempt = v);

    [RelayCommand]
    private void RemoveExempt(string value) => ExemptProviders.Remove(value);

    [RelayCommand]
    private void AddPattern()
    {
        var name = NewPatternName.Trim();
        var regex = NewPatternRegex.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(regex)) return;
        var action = string.IsNullOrWhiteSpace(NewPatternAction) ? "redact" : NewPatternAction;
        CustomPatterns.Add(new CustomPattern(name, regex, action));
        NewPatternName = string.Empty;
        NewPatternRegex = string.Empty;
        NewPatternAction = "redact";
    }

    [RelayCommand]
    private void RemovePattern(CustomPattern pattern) => CustomPatterns.Remove(pattern);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_settings is null)
        {
            StatusMessage = "Settings store unavailable.";
            return;
        }

        try
        {
            var config = new TrustBoundaryConfig
            {
                Enabled = Enabled,
                Sanitizer = new SanitizerConfig
                {
                    Enabled = SanitizerEnabled,
                    Mode = SanitizerMode,
                    CorporateDomains = [.. CorporateDomains],
                    AllowList = [.. AllowList],
                    ExemptProviders = [.. ExemptProviders],
                    CustomPatterns = [.. CustomPatterns],
                    LogRedactions = LogRedactions,
                },
                IntentVerification = new IntentVerificationConfig
                {
                    Enabled = IntentEnabled,
                    ClarifyAmbiguous = ClarifyAmbiguous,
                    BlockHarmfulIntent = BlockHarmful,
                },
            };

            await config.SaveToStoreAsync(_settings).ConfigureAwait(false);
            _liveSettings?.ReloadAll();
            StatusMessage = "Trust boundary saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private static void AddIfNew(ObservableCollection<string> target, string buffer, Action<string> reset)
    {
        var trimmed = buffer.Trim();
        if (!string.IsNullOrEmpty(trimmed) && !target.Contains(trimmed))
            target.Add(trimmed);
        reset(string.Empty);
    }

    private void LoadConfig()
    {
        var resolved = TrustBoundaryConfig.Resolve(_config.TrustBoundary, _settings);

        Enabled = resolved.Enabled;
        SanitizerEnabled = resolved.Sanitizer.Enabled;
        SanitizerMode = resolved.Sanitizer.Mode;
        LogRedactions = resolved.Sanitizer.LogRedactions;

        CorporateDomains.Clear();
        foreach (var d in resolved.Sanitizer.CorporateDomains)
            CorporateDomains.Add(d);

        AllowList.Clear();
        foreach (var a in resolved.Sanitizer.AllowList)
            AllowList.Add(a);

        ExemptProviders.Clear();
        foreach (var p in resolved.Sanitizer.ExemptProviders)
            ExemptProviders.Add(p);

        CustomPatterns.Clear();
        foreach (var p in resolved.Sanitizer.CustomPatterns)
            CustomPatterns.Add(p);

        IntentEnabled = resolved.IntentVerification.Enabled;
        ClarifyAmbiguous = resolved.IntentVerification.ClarifyAmbiguous;
        BlockHarmful = resolved.IntentVerification.BlockHarmfulIntent;
    }
}
