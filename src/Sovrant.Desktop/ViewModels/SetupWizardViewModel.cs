using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Config;

namespace Sovrant.Desktop.ViewModels;

public partial class SetupWizardViewModel : ViewModelBase
{
    private readonly SovrantConfig _config;
    private static readonly HttpClient Http = new();

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Known base URLs per provider.
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
    private bool _isVisible;

    [ObservableProperty]
    private string _selectedProvider = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _selectedModel = string.Empty;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isLoadingModels;

    public ObservableCollection<string> AvailableModels { get; } = [];

    public SetupWizardViewModel(SovrantConfig config)
    {
        _config = config;
        _isVisible = string.IsNullOrWhiteSpace(config.ApiKey);

        // Set initial provider (triggers OnSelectedProviderChanged -> loads models + base URL).
        SelectedProvider = "OpenRouter";
    }

    partial void OnSelectedProviderChanged(string value)
    {
        // Update base URL.
        BaseUrl = ProviderBaseUrls.GetValueOrDefault(value, string.Empty);

        // Load models for the new provider.
        SelectedModel = string.Empty;
        _ = LoadModelsForProviderAsync(value);
    }

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
                models = await FetchOpenRouterModelIdsAsync();
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

    /// <summary>
    /// Fetches model IDs from OpenRouter's public API (no key required).
    /// Reuses the same endpoint as <c>LiveModelMetadataFetcher</c> in Sovrant.Api.
    /// </summary>
    internal static async Task<List<string>> FetchOpenRouterModelIdsAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://openrouter.ai/api/v1/models"));
        request.Headers.Add("HTTP-Referer", "https://github.com/ramseur/sovrant-engine");

        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

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

    private static async Task<List<string>> FetchLocalModelIdsAsync(string url, string arrayProp, string idProp)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await Http.GetAsync(new Uri(url), cts.Token);
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

    [RelayCommand]
    private async Task SaveAndStartAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "Please enter an API key.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedModel))
        {
            StatusMessage = "Please select a model.";
            return;
        }

        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);

            Dictionary<string, object?> existing = [];
            if (File.Exists(SettingsPath))
            {
                var json = await File.ReadAllTextAsync(SettingsPath);
                existing = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
            }

            existing["Model"] = SelectedModel.Trim();
            existing["ApiKey"] = ApiKey.Trim();

            if (!string.IsNullOrWhiteSpace(BaseUrl))
                existing["BaseUrl"] = BaseUrl.Trim();

            var output = JsonSerializer.Serialize(existing, SerializerOptions);
            await File.WriteAllTextAsync(SettingsPath, output);

            IsVisible = false;
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

    public IReadOnlyList<string> Providers { get; } =
    [
        "OpenAI", "OpenRouter", "DeepSeek", "Groq", "Mistral", "Together AI",
        "Azure OpenAI", "Google", "Ollama", "LM Studio", "Custom"
    ];
}
