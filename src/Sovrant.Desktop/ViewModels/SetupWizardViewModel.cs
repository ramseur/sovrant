using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Auth;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using RuntimeProfile = Sovrant.Runtime.Providers.ProviderProfile;

namespace Sovrant.Desktop.ViewModels;

public partial class SetupWizardViewModel : ViewModelBase
{
    private readonly SovrantConfig _config;
    private readonly IUserPreferenceStore _prefs;
    private readonly IProviderProfileStore _profileStore;
    private readonly ICredentialStore _credentials;
    private readonly MutableAuthProvider? _authProvider;

    // Known base URLs per provider — shared with SettingsViewModel.
    private static readonly Dictionary<string, string> ProviderBaseUrls =
        SettingsViewModel.ProviderBaseUrls;

    // Default model per provider — used as initial model when none is configured yet.
    private static readonly Dictionary<string, string> DefaultModels = new(StringComparer.Ordinal)
    {
        ["OpenAI"] = "gpt-4o",
        ["OpenRouter"] = "google/gemma-4-27b-it:free",
        ["DeepSeek"] = "deepseek-chat",
        ["Groq"] = "llama-3.3-70b-versatile",
        ["Mistral"] = "mistral-large-latest",
        ["Together AI"] = "meta-llama/Llama-3.3-70B-Instruct-Turbo",
        ["Google"] = "gemini-2.5-flash",
        ["Azure OpenAI"] = "gpt-4o",
    };

    // ── Wizard step state ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isModeStep = true;
    [ObservableProperty] private bool _isProviderStep;
    [ObservableProperty] private bool _isRemoteStep;

    // ── Remote step ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string _remoteServerUrl = string.Empty;
    [ObservableProperty] private string _remoteStatusMessage = string.Empty;

    // ── Provider step ────────────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _selectedProvider = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    /// <summary>Raised after settings are saved successfully.</summary>
    public event Action? SetupCompleted;

    /// <summary>Raised when remote mode is saved and a restart is required.</summary>
    public event Action? RestartRequired;

    public SetupWizardViewModel(
        SovrantConfig config,
        IUserPreferenceStore prefs,
        IProviderProfileStore profileStore,
        ICredentialStore credentials,
        MutableAuthProvider? authProvider = null)
    {
        _config = config;
        _prefs = prefs;
        _profileStore = profileStore;
        _credentials = credentials;
        _authProvider = authProvider;
        _isVisible = string.IsNullOrWhiteSpace(config.ApiKey);

        // Set initial provider.
        SelectedProvider = "OpenRouter";
    }

    partial void OnSelectedProviderChanged(string value)
    {
        BaseUrl = ProviderBaseUrls.GetValueOrDefault(value, string.Empty);
    }

    [RelayCommand]
    private void ChooseLocal()
    {
        IsModeStep = false;
        IsProviderStep = true;
        IsRemoteStep = false;
    }

    [RelayCommand]
    private void ChooseRemote()
    {
        IsModeStep = false;
        IsProviderStep = false;
        IsRemoteStep = true;
    }

    [RelayCommand]
    private void BackToMode()
    {
        IsModeStep = true;
        IsProviderStep = false;
        IsRemoteStep = false;
    }

    [RelayCommand]
    private async Task SaveRemoteAndStartAsync()
    {
        if (string.IsNullOrWhiteSpace(RemoteServerUrl))
        {
            RemoteStatusMessage = "Please enter a server URL.";
            return;
        }

        if (!Uri.TryCreate(RemoteServerUrl.Trim(), UriKind.Absolute, out _))
        {
            RemoteStatusMessage = "Please enter a valid URL (e.g. http://localhost:5200).";
            return;
        }

        IsSaving = true;
        RemoteStatusMessage = string.Empty;

        try
        {
            await _credentials.StoreAsync(CredentialKeys.RuntimeMode, "remote");
            await _credentials.StoreAsync(CredentialKeys.RemoteServerUrl, RemoteServerUrl.Trim());
            await _credentials.StoreAsync(CredentialKeys.RemoteApiToken, string.Empty);

            IsVisible = false;
            RestartRequired?.Invoke();
        }
        catch (Exception ex)
        {
            RemoteStatusMessage = $"Failed to save: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task SaveAndStartAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "Please enter an API key.";
            return;
        }

        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            var trimmedKey = ApiKey.Trim();
            var trimmedBase = (BaseUrl ?? string.Empty).Trim();
            var defaultModel = DefaultModels.GetValueOrDefault(SelectedProvider, "gpt-4o");

            var profileId = MakeProfileId(SelectedProvider, SelectedProvider);
            var credentialId = $"provider.{profileId}.api_key";

            // Encrypted credentials first so the profile row's reference is valid the moment it's inserted.
            await _credentials.StoreAsync(credentialId, trimmedKey);
            // Mirror to the global key so MutableApiKeyAuthProvider's lookup
            // (CredentialKeys.LlmApiKey) resolves on the very next request.
            await _credentials.StoreAsync(CredentialKeys.LlmApiKey, trimmedKey);

            var now = DateTimeOffset.UtcNow;
            var runtimeProfile = new RuntimeProfile(
                ProfileId: profileId,
                UserId: App.SovrantUserId,
                Name: SelectedProvider,
                ProviderKind: SelectedProvider,
                BaseUrl: trimmedBase,
                DefaultModel: defaultModel,
                MaxTokens: 32000,
                CredentialId: credentialId,
                CreatedAt: now,
                UpdatedAt: now);

            var existing = await _profileStore.GetAsync(profileId);
            if (existing is null)
                await _profileStore.CreateAsync(runtimeProfile);
            else
                await _profileStore.UpdateAsync(runtimeProfile with { CreatedAt = existing.CreatedAt });

            // Pin the new profile as active and save scalar prefs so they survive the next boot.
            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.ActiveProviderProfileId, profileId);
            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.Provider, SelectedProvider);
            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.Model, defaultModel);
            if (!string.IsNullOrWhiteSpace(trimmedBase))
                await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.BaseUrl, trimmedBase);
            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.MaxTokens,
                "32000");

            // Hot-swap the running config so the chat surface works without a restart.
            _config.ApiKey = trimmedKey;
            _config.Model = defaultModel;
            _config.MaxTokens = 32000;
            if (!string.IsNullOrWhiteSpace(trimmedBase) && Uri.TryCreate(trimmedBase, UriKind.Absolute, out var parsed))
                _config.BaseUrl = parsed;

            if (_authProvider is not null)
            {
                _authProvider.ApiKey = trimmedKey;
                if (_config.BaseUrl is not null)
                    _authProvider.BaseUrl = _config.BaseUrl;
            }

            // Env-var bridge for any provider clients still reading from the
            // environment (LiveModelMetadataFetcher, OpenRouter API key probe).
            Environment.SetEnvironmentVariable("LLM_API_KEY", trimmedKey);
            Environment.SetEnvironmentVariable("LLM_MODEL", defaultModel);
            if (!string.IsNullOrWhiteSpace(trimmedBase))
                Environment.SetEnvironmentVariable("LLM_BASE_URL", trimmedBase);
            if (trimmedBase.Contains("openrouter", StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", trimmedKey);

            IsVisible = false;
            SetupCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
        Justification = "Profile ids are URL-style slugs and conventionally lowercase; never compared as security tokens.")]
    private static string MakeProfileId(string provider, string name)
    {
        var slug = $"{provider}-{name}".ToLowerInvariant();
        var sanitized = new string(slug.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        return sanitized.Trim('-');
    }

    public IReadOnlyList<string> Providers { get; } =
    [
        "OpenAI", "OpenRouter", "DeepSeek", "Groq", "Mistral", "Together AI",
        "Azure OpenAI", "Google", "Ollama", "LM Studio", "Custom"
    ];
}
