using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Routing;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;

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
    private readonly ISmartRouter? _router;
    private CancellationTokenSource? _autoSaveCts;
    private bool _initialized;
    private bool _suppressAutoSave;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "settings.json");

    private static readonly string ProfilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "providers.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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
    private string _selectedProvider = "OpenAI";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _modelName = "gpt-4o";

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private int _maxOutputTokens = 32000;

    [ObservableProperty]
    private bool _streaming = true;

    [ObservableProperty]
    private PermissionMode _permissionMode;

    [ObservableProperty]
    private bool _intentRoutingEnabled;

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

    public SettingsViewModel(SovrantConfig config, IPermissionModeAccessor permissionModeAccessor,
        SidebarViewModel sidebar, MutableAuthProvider authProvider, IHttpClientFactory httpFactory,
        ISmartRouter? router = null)
    {
        _config = config;
        _permissionModeAccessor = permissionModeAccessor;
        _sidebar = sidebar;
        _authProvider = authProvider;
        _httpFactory = httpFactory;
        _router = router;

        // Load current values from runtime config.
        _modelName = config.Model;
        _maxOutputTokens = config.MaxTokens;
        _apiKey = config.ApiKey ?? string.Empty;
        _baseUrl = config.BaseUrl?.ToString() ?? string.Empty;
        _permissionMode = permissionModeAccessor.Mode;
        _intentRoutingEnabled = router?.IntentRoutingEnabled ?? false;
        _selectedProvider = InferProvider(config);

        LoadProfiles();
        _ = LoadModelsForProviderAsync(_selectedProvider);

        _initialized = true;
    }

    public bool IsProvidersTab => SelectedTab == 0;
    public bool IsGeneralTab => SelectedTab == 1;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsProvidersTab));
        OnPropertyChanged(nameof(IsGeneralTab));
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = int.Parse(tab, System.Globalization.CultureInfo.InvariantCulture);

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
    partial void OnMaxOutputTokensChanged(int value) => ScheduleAutoSave();
    partial void OnStreamingChanged(bool value) => ScheduleAutoSave();

    private async Task LoadModelsForProviderAsync(string provider)
    {
        IsLoadingModels = true;
        try
        {
            List<string> models;

            if (provider == "OpenRouter")
            {
                models = await SetupWizardViewModel.FetchOpenRouterModelIdsAsync();
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
                models = !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrWhiteSpace(ApiKey)
                    ? await FetchAuthenticatedModelIdsAsync(baseUrl, ApiKey)
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
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());

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

    partial void OnIntentRoutingEnabledChanged(bool value)
    {
        if (_router is not null)
            _router.IntentRoutingEnabled = value;
        ScheduleAutoSave();
    }

    // ─── Provider Profiles ─────────────────────────────

    [RelayCommand]
    private async Task AddProviderAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "Please enter an API key.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ModelName))
        {
            StatusMessage = "Please select a model.";
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewProfileName)
            ? $"{SelectedProvider} - {SidebarViewModel.ShortenModelName(ModelName)}"
            : NewProfileName.Trim();

        // Update existing or add new.
        var existing = SavedProfiles.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Provider = SelectedProvider;
            existing.Model = ModelName;
            existing.ApiKey = ApiKey;
            existing.BaseUrl = BaseUrl;
            existing.MaxTokens = MaxOutputTokens;
        }
        else
        {
            SavedProfiles.Add(new ProviderProfile
            {
                Name = name,
                Provider = SelectedProvider,
                Model = ModelName,
                ApiKey = ApiKey,
                BaseUrl = BaseUrl,
                MaxTokens = MaxOutputTokens,
            });
        }

        PersistProfiles();

        // Auto-switch to the newly added provider.
        await LoadProfileAsync(SavedProfiles.Last(p => p.Name == name));

        NewProfileName = string.Empty;
        StatusMessage = $"Provider '{name}' added and activated.";
    }

    [RelayCommand]
    private async Task LoadProfileAsync(ProviderProfile profile)
    {
        _suppressAutoSave = true;
        try
        {
            // Set everything from the saved profile exactly as stored.
            // Order matters: provider and base URL first, then model list, then model name.
            SelectedProvider = profile.Provider;
            ApiKey = profile.ApiKey;
            BaseUrl = profile.BaseUrl;
            MaxOutputTokens = profile.MaxTokens;
            SelectedProfile = profile;

            // Load model list for the provider so the dropdown is populated,
            // then set the saved model name.
            await LoadModelsForProviderAsync(profile.Provider);
            ModelName = profile.Model;
        }
        finally
        {
            _suppressAutoSave = false;
        }

        // Single save applies everything to runtime config + env vars.
        await SaveAsync();
        StatusMessage = $"Switched to '{profile.Name}'.";
    }

    [RelayCommand]
    private void DeleteProfile(ProviderProfile profile)
    {
        SavedProfiles.Remove(profile);
        PersistProfiles();
        if (SelectedProfile == profile) SelectedProfile = null;
        StatusMessage = $"Profile '{profile.Name}' deleted.";
    }

    private void LoadProfiles()
    {
        SavedProfiles.Clear();
        if (!File.Exists(ProfilesPath)) return;

        try
        {
            var json = File.ReadAllText(ProfilesPath);
            var profiles = JsonSerializer.Deserialize<List<ProviderProfile>>(json, SerializerOptions);
            if (profiles is not null)
            {
                foreach (var p in profiles)
                    SavedProfiles.Add(p);
            }
        }
        catch { /* ignore corrupt file */ }
    }

    private void PersistProfiles()
    {
        try
        {
            var dir = Path.GetDirectoryName(ProfilesPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(SavedProfiles.ToList(), SerializerOptions);
            File.WriteAllText(ProfilesPath, json);
        }
        catch { /* best effort */ }
    }

    // ─── Save ──────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);

            // Read existing file to preserve fields we don't manage.
            Dictionary<string, object?> existing = [];
            if (File.Exists(SettingsPath))
            {
                var json = await File.ReadAllTextAsync(SettingsPath);
                existing = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
            }

            existing["Provider"] = SelectedProvider;
            existing["Model"] = ModelName;
            existing["MaxTokens"] = MaxOutputTokens;
            existing["PermissionMode"] = PermissionMode.ToString();
            existing["IntentRouting"] = IntentRoutingEnabled;

            if (!string.IsNullOrWhiteSpace(ApiKey))
                existing["ApiKey"] = ApiKey;

            if (!string.IsNullOrWhiteSpace(BaseUrl))
                existing["BaseUrl"] = BaseUrl;
            else
                existing.Remove("BaseUrl");

            var output = JsonSerializer.Serialize(existing, SerializerOptions);
            await File.WriteAllTextAsync(SettingsPath, output);

            // Hot-swap runtime config, env vars, and auth provider.
            _config.Model = ModelName.Trim();
            _config.MaxTokens = MaxOutputTokens;
            _config.PermissionMode = PermissionMode;

            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                _config.ApiKey = ApiKey.Trim();
                Environment.SetEnvironmentVariable("LLM_API_KEY", ApiKey.Trim());
                _authProvider.ApiKey = ApiKey.Trim();
                if (BaseUrl.Contains("openrouter", StringComparison.OrdinalIgnoreCase))
                    Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", ApiKey.Trim());
            }

            if (!string.IsNullOrWhiteSpace(BaseUrl))
            {
                var parsedUrl = new Uri(BaseUrl.Trim());
                _config.BaseUrl = parsedUrl;
                _authProvider.BaseUrl = parsedUrl;
                Environment.SetEnvironmentVariable("LLM_BASE_URL", BaseUrl.Trim());
            }
            else
            {
                _config.BaseUrl = null;
                _authProvider.BaseUrl = null;
                Environment.SetEnvironmentVariable("LLM_BASE_URL", null);
            }

            if (!string.IsNullOrWhiteSpace(ModelName))
                Environment.SetEnvironmentVariable("LLM_MODEL", ModelName.Trim());

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

    private static string InferProvider(SovrantConfig config)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".sovrant", "settings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Provider", out var prop) &&
                    prop.GetString() is { Length: > 0 } saved)
                    return saved;
            }
        }
        catch { /* fall through */ }

        var url = config.BaseUrl?.ToString() ?? string.Empty;
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

    public IReadOnlyList<PermissionMode> PermissionModes { get; } =
    [
        PermissionMode.Default,
        PermissionMode.AcceptEdits,
        PermissionMode.BypassPermissions,
    ];
}

public partial class ProviderProfile : ViewModelBase
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _provider = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _baseUrl = string.Empty;
    [ObservableProperty] private int _maxTokens = 32000;
}
