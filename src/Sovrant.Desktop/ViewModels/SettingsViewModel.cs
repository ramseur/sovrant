using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private CancellationTokenSource? _autoSaveCts;
    private bool _initialized;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Known base URLs per provider (shared with wizard).
    private static readonly Dictionary<string, string> ProviderBaseUrls = new(StringComparer.Ordinal)
    {
        ["OpenRouter"] = "https://openrouter.ai/api/v1",
        ["DeepSeek"] = "https://api.deepseek.com/v1",
        ["Groq"] = "https://api.groq.com/openai/v1",
        ["Mistral"] = "https://api.mistral.ai/v1",
        ["Together AI"] = "https://api.together.xyz/v1",
        ["Ollama"] = "http://localhost:11434/v1",
        ["LM Studio"] = "http://localhost:1234/v1",
    };

    // Static model lists for providers without a public models API.
    private static readonly Dictionary<string, string[]> StaticProviderModels = new(StringComparer.Ordinal)
    {
        ["OpenAI"] = ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo", "o1", "o1-mini", "o3-mini"],
        ["DeepSeek"] = ["deepseek-chat", "deepseek-reasoner"],
        ["Groq"] = ["llama-3.3-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768", "gemma2-9b-it"],
        ["Mistral"] = ["mistral-large-latest", "mistral-medium-latest", "mistral-small-latest", "open-mixtral-8x22b"],
        ["Together AI"] = ["meta-llama/Llama-3.3-70B-Instruct-Turbo", "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1", "Qwen/Qwen2.5-72B-Instruct-Turbo"],
        ["Google"] = ["gemini-2.0-flash", "gemini-2.0-flash-lite", "gemini-1.5-pro", "gemini-1.5-flash"],
        ["Azure OpenAI"] = ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo"],
    };

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
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoadingModels;

    public ObservableCollection<string> AvailableModels { get; } = [];

    public SettingsViewModel(SovrantConfig config, IPermissionModeAccessor permissionModeAccessor,
        SidebarViewModel sidebar, MutableAuthProvider authProvider)
    {
        _config = config;
        _permissionModeAccessor = permissionModeAccessor;
        _sidebar = sidebar;
        _authProvider = authProvider;

        // Load current values from runtime config.
        _modelName = config.Model;
        _maxOutputTokens = config.MaxTokens;
        _apiKey = config.ApiKey ?? string.Empty;
        _baseUrl = config.BaseUrl?.ToString() ?? string.Empty;
        _permissionMode = permissionModeAccessor.Mode;
        _selectedProvider = InferProvider(config);

        // Load models for the current provider.
        _ = LoadModelsForProviderAsync(_selectedProvider);

        _initialized = true;
    }

    /// <summary>
    /// Debounced auto-save: waits 600ms after the last change, then saves.
    /// Called by OnXxxChanged handlers for all settings properties.
    /// </summary>
    private void ScheduleAutoSave()
    {
        if (!_initialized) return;

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
            catch (OperationCanceledException) { /* debounced — newer change arrived */ }
        }, token);
    }

    [RelayCommand]
    private void ClearModel() => ModelName = string.Empty;

    partial void OnSelectedProviderChanged(string value)
    {
        // Update base URL for known providers.
        if (ProviderBaseUrls.TryGetValue(value, out var url))
            BaseUrl = url;

        // Load models for the new provider.
        _ = LoadModelsForProviderAsync(value);
        ScheduleAutoSave();
    }

    partial void OnApiKeyChanged(string value) => ScheduleAutoSave();
    partial void OnModelNameChanged(string value) => ScheduleAutoSave();
    partial void OnBaseUrlChanged(string value) => ScheduleAutoSave();
    partial void OnMaxOutputTokensChanged(int value) => ScheduleAutoSave();
    partial void OnStreamingChanged(bool value) => ScheduleAutoSave();

    private async Task LoadModelsForProviderAsync(string provider)
    {
        IsLoadingModels = true;
        try
        {
            List<string> models;

            if (StaticProviderModels.TryGetValue(provider, out var staticList))
            {
                models = [.. staticList];
            }
            else if (provider == "OpenRouter")
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
                models = [];
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

    private static async Task<List<string>> FetchLocalModelIdsAsync(string url, string arrayProp, string idProp)
    {
        try
        {
            using var http = new HttpClient();
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

            if (!string.IsNullOrWhiteSpace(ApiKey))
                existing["ApiKey"] = ApiKey;

            if (!string.IsNullOrWhiteSpace(BaseUrl))
                existing["BaseUrl"] = BaseUrl;

            var output = JsonSerializer.Serialize(existing, SerializerOptions);
            await File.WriteAllTextAsync(SettingsPath, output);

            // Hot-swap runtime: update the config singleton, env vars, and auth
            // provider so new chat sessions use the updated settings without restarting.
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
                _config.BaseUrl = new Uri(BaseUrl.Trim());
                Environment.SetEnvironmentVariable("LLM_BASE_URL", BaseUrl.Trim());
            }
            if (!string.IsNullOrWhiteSpace(ModelName))
                Environment.SetEnvironmentVariable("LLM_MODEL", ModelName.Trim());

            // Update sidebar model/provider display immediately.
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
    /// Infers the provider from the saved settings.json "Provider" field,
    /// falling back to BaseUrl heuristics if not present.
    /// </summary>
    private static string InferProvider(SovrantConfig config)
    {
        // Try reading the Provider field from settings.json directly.
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
        catch { /* fall through to heuristic */ }

        // Heuristic from BaseUrl.
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
