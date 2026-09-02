using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Auth;
using Sovrant.Api.Capabilities;
using Sovrant.Api.Config;
using Sovrant.Api.Routing;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Workspaces;
using RuntimeProfile = Sovrant.Runtime.Providers.ProviderProfile;

namespace Sovrant.Desktop.ViewModels;

#pragma warning disable CA1001 // ViewModel lifecycle is managed by DI
public partial class SettingsViewModel : ViewModelBase
#pragma warning restore CA1001
{
    private readonly SovrantConfig _config;
    private readonly IPermissionModeAccessor _permissionModeAccessor;
    private readonly SidebarViewModel _sidebar;
    private readonly MutableAuthProvider _authProvider;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IUserPreferenceStore _prefs;
    private readonly IProviderProfileStore _profileStore;
    private readonly ICredentialStore _credentials;
    private readonly ISmartRouter? _router;
    private readonly IWorkspaceService? _workspaceService;
    private readonly IWorkspaceSettingsStore? _wsSettings;
    private CancellationTokenSource? _autoSaveCts;
    private bool _initialized;
    private bool _suppressAutoSave;

    // Known base URLs per provider. These match the endpoints each provider
    // expects for OpenAI-compatible chat completions.
    internal static readonly Dictionary<string, string> ProviderBaseUrls = new(StringComparer.Ordinal)
    {
        ["OpenAI"] = "https://api.openai.com/v1",
        ["OpenRouter"] = "https://openrouter.ai/api/v1",
        ["DeepSeek"] = "https://api.deepseek.com/v1",
        ["Groq"] = "https://api.groq.com/openai/v1",
        ["Mistral"] = "https://api.mistral.ai/v1",
        ["Together AI"] = "https://api.together.xyz/v1",
        ["Ollama"] = "http://localhost:11434/v1",
        ["LM Studio"] = "http://localhost:1234/v1",
        ["Google"] = "https://generativelanguage.googleapis.com/v1beta/openai",
        ["Azure OpenAI"] = "", // user must fill in their own endpoint
        ["Custom"] = "",       // user must fill in their own endpoint
    };

    // Static model lists — used as fallback when live /models fetch is unavailable.
    private static readonly Dictionary<string, string[]> StaticProviderModels = new(StringComparer.Ordinal)
    {
        ["OpenAI"] = ["gpt-5", "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano", "gpt-4o", "gpt-4o-mini", "o4-mini", "o3", "o3-mini", "o1", "o1-mini"],
        ["DeepSeek"] = ["deepseek-chat", "deepseek-reasoner"],
        ["Groq"] = ["llama-3.3-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768", "gemma2-9b-it"],
        ["Mistral"] = ["mistral-large-latest", "mistral-medium-latest", "mistral-small-latest", "open-mixtral-8x22b"],
        ["Together AI"] = ["meta-llama/Llama-3.3-70B-Instruct-Turbo", "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1", "Qwen/Qwen2.5-72B-Instruct-Turbo"],
        ["Google"] = ["gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.0-flash", "gemini-2.0-flash-lite"],
        ["Azure OpenAI"] = ["gpt-4o", "gpt-4o-mini", "gpt-4.1"],
    };

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private bool _isDarkMode = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiKeyLabel))]
    [NotifyPropertyChangedFor(nameof(ApiKeyWatermark))]
    private string _selectedProvider = "OpenAI";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _modelName = "gpt-4o";

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private PermissionMode _permissionMode;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoadingModels;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private ProviderProfile? _selectedProfile;

    public ObservableCollection<string> AvailableModels { get; } = [];
    public ObservableCollection<ProviderProfile> SavedProfiles { get; } = [];
    public ObservableCollection<WorkspaceSelectItem> WorkspaceItems { get; } = [];

    public SettingsViewModel(SovrantConfig config, IPermissionModeAccessor permissionModeAccessor,
        SidebarViewModel sidebar, MutableAuthProvider authProvider, IHttpClientFactory httpFactory,
        IUserPreferenceStore prefs,
        IProviderProfileStore profileStore,
        ICredentialStore credentials,
        ISmartRouter? router = null,
        WebSearchOptions? webSearchOptions = null,
        IModelCapabilityRegistry? capabilities = null,
        IWorkspaceService? workspaceService = null,
        IWorkspaceSettingsStore? wsSettings = null)
    {
        _config = config;
        _permissionModeAccessor = permissionModeAccessor;
        _sidebar = sidebar;
        _authProvider = authProvider;
        _httpFactory = httpFactory;
        _prefs = prefs;
        _profileStore = profileStore;
        _credentials = credentials;
        _router = router;
        _workspaceService = workspaceService;
        _wsSettings = wsSettings;

        // Load current values from runtime config (already populated by
        // ApplyUserPreferencesAsync at boot).
        _modelName = config.Model;
        _apiKey = string.Empty; // Never pre-fill; user must enter or load a saved profile explicitly.
        _baseUrl = config.BaseUrl?.ToString() ?? string.Empty;
        _permissionMode = permissionModeAccessor.Mode;

        // Provider name + saved profile list need DB reads — fire and forget;
        // the UI will populate as the await chain completes.
        _selectedProvider = InferProviderFromBaseUrl(config.BaseUrl);
        _ = HydrateFromStoresAsync();

        _initialized = true;
    }

    internal async Task HydrateFromStoresAsync()
    {
        // Provider name is stored in user preferences when explicitly set.
        var savedProvider = await _prefs.GetAsync(App.SovrantUserId, UserPreferenceKeys.Provider);
        if (!string.IsNullOrEmpty(savedProvider))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _suppressAutoSave = true;
                SelectedProvider = savedProvider;
                _suppressAutoSave = false;
            });
        }

        // Load connection mode from credential store.
        var runtimeMode = await _credentials.RetrieveAsync(CredentialKeys.RuntimeMode);
        var remoteUrl = await _credentials.RetrieveAsync(CredentialKeys.RemoteServerUrl);
        var isRemote = string.Equals(runtimeMode, "remote", StringComparison.OrdinalIgnoreCase);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsRemoteMode = isRemote;
            ConnectionServerUrl = remoteUrl ?? string.Empty;
        });

        await LoadProfilesAsync();

        // Re-apply any navigation-time provider override AFTER hydration so it isn't
        // overwritten by the saved-preference path above.
        if (_pendingProviderOverride is not null)
        {
            var pending = _pendingProviderOverride;
            _pendingProviderOverride = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _suppressAutoSave = true;
                SelectedProvider = pending;
                _suppressAutoSave = false;
            });
        }

        await Dispatcher.UIThread.InvokeAsync(() => _ = LoadModelsForProviderAsync(SelectedProvider));

        if (_workspaceService is not null)
        {
            try
            {
                var workspaces = await _workspaceService.ListAllAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    WorkspaceItems.Clear();
                    foreach (var ws in workspaces)
                    {
                        // Personal workspace is pre-selected; all others are opt-in.
                        var isPersonal = ws.Type == Runtime.Workspaces.WorkspaceType.Personal;
                        WorkspaceItems.Add(new WorkspaceSelectItem { WorkspaceId = ws.WorkspaceId, Name = ws.Name, IsSelected = isPersonal });
                    }
                });
            }
            catch { /* workspaces unavailable — omit the picker */ }
        }
    }

    // ── Connection tab ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isRemoteMode;
    [ObservableProperty] private string _connectionServerUrl = string.Empty;
    [ObservableProperty] private string _connectionStatusMessage = string.Empty;

    /// <summary>Raised when the user switches mode and the app must restart.</summary>
    public event Action? RestartRequired;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsProvidersTab => SelectedTab == 1;
    public bool IsConnectionTab => SelectedTab == 2;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsGeneralTab));
        OnPropertyChanged(nameof(IsProvidersTab));
        OnPropertyChanged(nameof(IsConnectionTab));
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = int.Parse(tab, CultureInfo.InvariantCulture);

    /// <summary>
    /// Debounced auto-save: waits 600ms after the last change, then saves.
    /// </summary>
    private void ScheduleAutoSave()
    {
        if (!_initialized || _suppressAutoSave) return;

        _autoSaveCts?.Cancel();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600, token);
                await Dispatcher.UIThread.InvokeAsync(() => SaveCommand.Execute(null));
            }
            catch (OperationCanceledException) { /* debounced */ }
        }, token);
    }

    [RelayCommand]
    private void ClearModel() => ModelName = string.Empty;

    [RelayCommand]
    private async Task RefreshModelsAsync() => await LoadModelsForProviderAsync(SelectedProvider);

    partial void OnSelectedProviderChanged(string value)
    {
        if (_suppressAutoSave) return;

        // Changing provider starts a fresh add-profile flow — clear the key field.
        ApiKey = string.Empty;

        // Update base URL for known providers.
        if (ProviderBaseUrls.TryGetValue(value, out var url))
            BaseUrl = url;

        _ = LoadModelsForProviderAsync(value);
        ScheduleAutoSave();
    }

    // Note: ApiKey and BaseUrl do NOT auto-save on every keystroke to avoid
    // saving partial values. They are saved when the provider dropdown changes
    // (which sets these programmatically) or when the user clicks Save Profile.
    // The debounced save from OnSelectedProviderChanged covers the switch case.
    partial void OnModelNameChanged(string value)
    {
        // Auto-detect correct provider from model name patterns so the user
        // doesn't accidentally send an OpenRouter model to the OpenAI endpoint.
        if (!_suppressAutoSave && _initialized)
        {
            var inferred = InferProviderFromModelName(value);
            if (inferred is not null && !string.Equals(inferred, SelectedProvider, StringComparison.Ordinal))
            {
                _suppressAutoSave = true;
                SelectedProvider = inferred;
                if (ProviderBaseUrls.TryGetValue(inferred, out var url))
                    BaseUrl = url;
                _suppressAutoSave = false;
            }
        }

        ScheduleAutoSave();
    }

    private async Task LoadModelsForProviderAsync(string provider)
    {
        IsLoadingModels = true;
        try
        {
            List<string> models;

            // The form ApiKey is cleared on provider switch before this fires.
            // Fall back to the saved credential for any existing profile that
            // matches the selected provider so the model list still loads.
            var effectiveKey = !string.IsNullOrWhiteSpace(ApiKey)
                ? ApiKey
                : SavedProfiles.FirstOrDefault(p =>
                    p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))?.ApiKey
                  ?? string.Empty;

            if (provider == "OpenRouter")
            {
                models = await FetchAuthenticatedModelIdsAsync("https://openrouter.ai/api/v1", effectiveKey);
            }
            else if (provider == "Ollama")
            {
                models = await FetchLocalModelIdsAsync("http://localhost:11434/api/tags", "models", "name");
            }
            else if (provider == "LM Studio")
            {
                models = await FetchLocalModelIdsAsync("http://localhost:1234/v1/models", "data", "id");
            }
            else
            {
                // Try fetching from the provider's /models endpoint (OpenAI, DeepSeek, Groq, etc.)
                var baseUrl = ProviderBaseUrls.GetValueOrDefault(provider, string.Empty);
                models = !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrWhiteSpace(effectiveKey)
                    ? await FetchAuthenticatedModelIdsAsync(baseUrl, effectiveKey)
                    : [];

                // Fall back to static list if API fetch returned nothing.
                if (models.Count == 0 && StaticProviderModels.TryGetValue(provider, out var staticList))
                    models = [.. staticList];
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableModels.Clear();
                foreach (var m in models)
                    AvailableModels.Add(m);
            });
        }
        catch
        {
            // Best-effort — user can type manually.
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    /// <summary>
    /// Fetches model IDs from any OpenAI-compatible /models endpoint using an API key.
    /// Works with OpenAI, DeepSeek, Groq, Mistral, Together AI, Google, etc.
    /// </summary>
    private async Task<List<string>> FetchAuthenticatedModelIdsAsync(string baseUrl, string apiKey)
    {
        try
        {
            using var http = _httpFactory.CreateClient("ProviderProbe");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

            var modelsUrl = baseUrl.TrimEnd('/') + "/models";
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(modelsUrl));
            var safeKey = new string(apiKey.Where(c => c < 128).ToArray()).Trim();
            if (!string.IsNullOrEmpty(safeKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", safeKey);

            var response = await http.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id) && id.GetString() is { } modelId)
                        models.Add(modelId);
                }
            }

            models.Sort(StringComparer.OrdinalIgnoreCase);
            return models;
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<string>> FetchLocalModelIdsAsync(string url, string arrayProp, string idProp)
    {
        try
        {
            using var http = _httpFactory.CreateClient("ProviderProbe");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await http.GetAsync(new Uri(url), cts.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var models = new List<string>();
            if (doc.RootElement.TryGetProperty(arrayProp, out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.TryGetProperty(idProp, out var id) && id.GetString() is { } modelId)
                        models.Add(modelId);
                }
            }
            return models;
        }
        catch
        {
            return [];
        }
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant =
                value ? ThemeVariant.Dark : ThemeVariant.Light;
        }
        ScheduleAutoSave();
    }

    partial void OnPermissionModeChanged(PermissionMode value)
    {
        _permissionModeAccessor.Mode = value;
        ScheduleAutoSave();
    }

    // ─── Provider Profiles ─────────────────────────────

    private static bool IsLocalProvider(string provider) =>
        provider is "Ollama" or "LM Studio";

    public string ApiKeyLabel =>
        IsLocalProvider(SelectedProvider) ? "API Key (optional)" : "API Key";

    public string ApiKeyWatermark =>
        IsLocalProvider(SelectedProvider) ? "Not required for local providers" : "sk-...";

    [RelayCommand]
    private async Task AddProviderAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) && !IsLocalProvider(SelectedProvider))
        {
            StatusMessage = "Please enter an API key.";
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(NewProfileName)
            ? SelectedProvider
            : NewProfileName.Trim();

        var profileId = MakeProfileId(SelectedProvider, displayName);
        var credentialId = $"provider.{profileId}.api_key";

        // Persist secret to credential store first so the row's reference is valid.
        await _credentials.StoreAsync(credentialId, new string(ApiKey.Where(c => c < 128).ToArray()).Trim());

        var now = DateTimeOffset.UtcNow;
        var runtimeProfile = new RuntimeProfile(
            ProfileId: profileId,
            UserId: App.SovrantUserId,
            Name: displayName,
            ProviderKind: SelectedProvider,
            BaseUrl: BaseUrl ?? string.Empty,
            // Always null at create time. The form's ModelName field is shared
            // with the current-session model selection; using it here would
            // carry over the user's previous pick and auto-stamp the new
            // provider with a model they never chose for it.
            DefaultModel: null,
            MaxTokens: _config.MaxTokens,
            CredentialId: credentialId,
            CreatedAt: now,
            UpdatedAt: now);

        var existing = await _profileStore.GetAsync(profileId);
        if (existing is null)
            await _profileStore.CreateAsync(runtimeProfile);
        else
            await _profileStore.UpdateAsync(runtimeProfile with { CreatedAt = existing.CreatedAt });

        // Clear the in-form model so it isn't echoed back via LoadProfileAsync/SaveAsync.
        // Suppress auto-save so the OnModelNameChanged debounce doesn't fire a
        // delayed SaveAsync that overwrites the sidebar's "Select a model" display.
        _suppressAutoSave = true;
        try { ModelName = string.Empty; }
        finally { _suppressAutoSave = false; }

        // Clear the user's previously-saved Model pref — the new provider
        // hasn't had a model chosen yet, and the old pref likely points to a
        // model the new provider does not offer.
        await _prefs.DeleteAsync(App.SovrantUserId, UserPreferenceKeys.Model);

        // Refresh the UI list so the new profile appears immediately.
        await LoadProfilesAsync();

        // Auto-switch to the newly added profile.
        var added = SavedProfiles.FirstOrDefault(p =>
            string.Equals(p.ProfileId, profileId, StringComparison.Ordinal));
        if (added is not null)
            await LoadProfileAsync(added);

        // Clear the form so the next add starts from a clean slate. Suppress
        // auto-save so OnApiKeyChanged/NewProfileNameChanged don't trigger a
        // delayed SaveAsync.
        _suppressAutoSave = true;
        try
        {
            NewProfileName = string.Empty;
            ApiKey = string.Empty;
        }
        finally { _suppressAutoSave = false; }

        // Update enabled_profile_ids for each selected workspace
        if (_wsSettings is not null)
        {
            foreach (var item in WorkspaceItems.Where(w => w.IsSelected))
            {
                var existingRaw = await _wsSettings.GetAsync(item.WorkspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds);
                var ids = new HashSet<string>(string.IsNullOrEmpty(existingRaw)
                    ? []
                    : existingRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                ids.Add(profileId);
                await _wsSettings.SetAsync(item.WorkspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds, string.Join(',', ids));
            }
            foreach (var item in WorkspaceItems) item.IsSelected = false;
        }

        StatusMessage = $"Provider '{SelectedProvider}' added.";
    }

    [RelayCommand]
    private async Task LoadProfileAsync(ProviderProfile profile)
    {
        _suppressAutoSave = true;
        try
        {
            // Set everything from the saved profile exactly as stored. If the
            // profile has no DefaultModel, clear ModelName explicitly — leaving
            // the previous form value in place would cause SaveAsync to write
            // it to the user's Model preference, making the sidebar show a
            // model the user never picked for this provider.
            SelectedProvider = profile.Provider;
            ApiKey = profile.ApiKey;
            BaseUrl = profile.BaseUrl;
            ModelName = profile.Model ?? string.Empty;
            SelectedProfile = profile;
        }
        finally
        {
            _suppressAutoSave = false;
        }

        // Pin this profile id as the active one — boot path will pick it up next time.
        await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.ActiveProviderProfileId, profile.ProfileId);

        // Single save applies everything to runtime config + env vars.
        await SaveAsync();
        // Refresh sidebar dropdown to reflect changes.
        await _sidebar.LoadProviderProfilesAsync();
        StatusMessage = $"Switched to '{profile.Name}'.";
    }

    [RelayCommand]
    private async Task DeleteProfileAsync(ProviderProfile profile)
    {
        await _profileStore.DeleteAsync(profile.ProfileId);
        await _credentials.DeleteAsync(profile.CredentialId);
        SavedProfiles.Remove(profile);
        if (SelectedProfile == profile) SelectedProfile = null;

        // Clear the active-profile + model prefs if we just removed the active
        // one — otherwise the next boot would resurrect a model selection that
        // belongs to a profile that no longer exists.
        var activeProfileId = await _prefs.GetAsync(App.SovrantUserId, UserPreferenceKeys.ActiveProviderProfileId);
        if (string.Equals(activeProfileId, profile.ProfileId, StringComparison.Ordinal))
        {
            await _prefs.DeleteAsync(App.SovrantUserId, UserPreferenceKeys.ActiveProviderProfileId);
            await _prefs.DeleteAsync(App.SovrantUserId, UserPreferenceKeys.Model);
        }

        // Refresh the sidebar so the dropdown reflects the new state.
        await _sidebar.LoadProviderProfilesAsync();
        StatusMessage = $"Profile '{profile.Name}' deleted.";
    }

    private async Task LoadProfilesAsync()
    {
        var rows = await _profileStore.ListAsync(App.SovrantUserId);
        var hydrated = new List<ProviderProfile>(rows.Count);
        foreach (var row in rows)
        {
            var apiKey = await _credentials.RetrieveAsync(row.CredentialId) ?? string.Empty;
            hydrated.Add(new ProviderProfile
            {
                ProfileId = row.ProfileId,
                CredentialId = row.CredentialId,
                Name = row.Name,
                Provider = row.ProviderKind,
                Model = row.DefaultModel ?? string.Empty,
                ApiKey = apiKey,
                BaseUrl = row.BaseUrl,
                MaxTokens = row.MaxTokens ?? 32000,
            });
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SavedProfiles.Clear();
            foreach (var p in hydrated)
                SavedProfiles.Add(p);
        });
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
        Justification = "Profile ids are URL-style slugs and conventionally lowercase; never compared as security tokens.")]
    private static string MakeProfileId(string provider, string name)
    {
        var slug = $"{provider}-{name}".ToLowerInvariant();
        var sanitized = new string(slug.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        return sanitized.Trim('-');
    }

    // ─── Save ──────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            // Persist scalar prefs to the per-user store.
            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.Provider, SelectedProvider);
            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.Model, ModelName.Trim());
            await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.PermissionMode, PermissionMode.ToString());

            if (!string.IsNullOrWhiteSpace(BaseUrl))
                await _prefs.SetAsync(App.SovrantUserId, UserPreferenceKeys.BaseUrl, BaseUrl.Trim());
            else
                await _prefs.DeleteAsync(App.SovrantUserId, UserPreferenceKeys.BaseUrl);

            // Secret never goes to the preference store. The MutableApiKeyAuthProvider
            // (server) and ApplyUserPreferencesAsync (boot path) both look up
            // CredentialKeys.LlmApiKey, so writing here keeps the running process
            // and the next boot in agreement.
            if (!string.IsNullOrWhiteSpace(ApiKey))
                await _credentials.StoreAsync(CredentialKeys.LlmApiKey, new string(ApiKey.Where(c => c < 128).ToArray()).Trim());

            // Hot-swap runtime config, env vars, and auth provider.
            _config.Model = ModelName.Trim();
            _config.PermissionMode = PermissionMode;

            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                _config.ApiKey = ApiKey.Trim();
                _authProvider.ApiKey = ApiKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(BaseUrl))
            {
                var parsedUrl = new Uri(BaseUrl.Trim());
                _config.BaseUrl = parsedUrl;
                _authProvider.BaseUrl = parsedUrl;
            }
            else
            {
                _config.BaseUrl = null;
                _authProvider.BaseUrl = null;
            }

            // Pin the router to the provider matching the configured base URL so that
            // OllamaProvider (cost=0) doesn't silently win over a cloud provider.
            if (_router is not null)
            {
                bool isLocal = _config.BaseUrl is not null &&
                               (_config.BaseUrl.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                                _config.BaseUrl.Host == "127.0.0.1");
                await _router.PinProviderAsync(isLocal ? "ollama" : "openai-compat").ConfigureAwait(false);
            }

            // Update sidebar display immediately.
            _sidebar.CurrentModel = SidebarViewModel.ShortenModelName(ModelName);
            _sidebar.CurrentProvider = SelectedProvider;
            _sidebar.IsConnected = !string.IsNullOrWhiteSpace(ApiKey);
            _sidebar.ConnectionStatus = !string.IsNullOrWhiteSpace(ApiKey) ? "Connected" : "No API key";

            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Infers the correct provider from a model name so the base URL auto-switches.
    /// Returns null if the model doesn't clearly belong to a specific provider.
    /// </summary>
    private static string? InferProviderFromModelName(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        var m = model.Trim();

        // OpenRouter models use org/model format (e.g. "google/gemma-4-31b-it:free")
        if (m.Contains('/', StringComparison.Ordinal))
            return "OpenRouter";

        // OpenAI models
        if (m.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("chatgpt", StringComparison.OrdinalIgnoreCase))
            return "OpenAI";

        // DeepSeek models
        if (m.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase))
            return "DeepSeek";

        // Google Gemini models
        if (m.StartsWith("gemini", StringComparison.OrdinalIgnoreCase))
            return "Google";

        // Mistral models (without org/ prefix — those go to OpenRouter)
        if (m.StartsWith("mistral", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("open-mixtral", StringComparison.OrdinalIgnoreCase))
            return "Mistral";

        return null;
    }

    private static string InferProviderFromBaseUrl(Uri? baseUrl)
    {
        var url = baseUrl?.ToString() ?? string.Empty;
        if (url.Contains("openrouter", StringComparison.OrdinalIgnoreCase)) return "OpenRouter";
        if (url.Contains("deepseek", StringComparison.OrdinalIgnoreCase)) return "DeepSeek";
        if (url.Contains("groq", StringComparison.OrdinalIgnoreCase)) return "Groq";
        if (url.Contains("mistral", StringComparison.OrdinalIgnoreCase)) return "Mistral";
        if (url.Contains("together", StringComparison.OrdinalIgnoreCase)) return "Together AI";
        if (url.Contains("localhost:11434", StringComparison.Ordinal)) return "Ollama";
        if (url.Contains("localhost:1234", StringComparison.Ordinal)) return "LM Studio";
        return "OpenAI";
    }

    public IReadOnlyList<string> Providers { get; } =
    [
        "OpenAI", "DeepSeek", "Groq", "Mistral", "Together AI",
        "Azure OpenAI", "Google", "OpenRouter", "Ollama", "LM Studio", "Custom"
    ];

    private string? _pendingProviderOverride;

    /// <summary>Called from navigation to pre-select a provider by name (case-insensitive).
    /// Stores a pending override so hydration cannot overwrite it.</summary>
    public void SelectProvider(string name)
    {
        var match = Providers.FirstOrDefault(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            _pendingProviderOverride = match;
            SelectedProvider = match;
        }
    }

    public IReadOnlyList<PermissionMode> PermissionModes { get; } =
    [
        PermissionMode.Default,
        PermissionMode.AcceptEdits,
        PermissionMode.BypassPermissions,
    ];

    /// <summary>Read-only view of the admin-managed max tokens setting.</summary>
    public int MaxOutputTokens => _config.MaxTokens;

    /// <summary>Read-only view of the admin-managed intent routing setting.</summary>
    public bool IntentRoutingEnabled => _router?.IntentRoutingEnabled ?? false;

    [RelayCommand]
    private async Task SaveConnectionAsync()
    {
        if (IsRemoteMode && string.IsNullOrWhiteSpace(ConnectionServerUrl))
        {
            ConnectionStatusMessage = "Please enter a server URL.";
            return;
        }

        if (IsRemoteMode && !Uri.TryCreate(ConnectionServerUrl.Trim(), UriKind.Absolute, out _))
        {
            ConnectionStatusMessage = "Please enter a valid URL (e.g. http://localhost:5200).";
            return;
        }

        try
        {
            if (IsRemoteMode)
            {
                await _credentials.StoreAsync(CredentialKeys.RuntimeMode, "remote");
                await _credentials.StoreAsync(CredentialKeys.RemoteServerUrl, ConnectionServerUrl.Trim());
            }
            else
            {
                await _credentials.StoreAsync(CredentialKeys.RuntimeMode, "local");
                await _credentials.DeleteAsync(CredentialKeys.RemoteServerUrl);
                await _credentials.DeleteAsync(CredentialKeys.RemoteApiToken);
            }

            ConnectionStatusMessage = "Saved. Restart the app to apply changes.";
            RestartRequired?.Invoke();
        }
        catch (Exception ex)
        {
            ConnectionStatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
    private async Task LogoutAsync()
    {
        await App.LogoutAsync().ConfigureAwait(true);
    }
}

public partial class ProviderProfile : ViewModelBase
{
    /// <summary>Stable id used as the row key in <c>provider_profiles</c>.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Lookup key used by the credential store for this profile's API key.</summary>
    public string CredentialId { get; set; } = string.Empty;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _provider = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _baseUrl = string.Empty;
    [ObservableProperty] private int _maxTokens = 32000;
}

public partial class WorkspaceSelectItem : ViewModelBase
{
    public string WorkspaceId { get; set; } = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isSelected;
}
