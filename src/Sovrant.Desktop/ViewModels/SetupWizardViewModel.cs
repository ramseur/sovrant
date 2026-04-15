using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Config;

namespace Sovrant.Desktop.ViewModels;

public partial class SetupWizardViewModel : ViewModelBase
{
    private readonly SovrantConfig _config;

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

    public SetupWizardViewModel(SovrantConfig config)
    {
        _config = config;
        _isVisible = string.IsNullOrWhiteSpace(config.ApiKey);

        // Set initial provider.
        SelectedProvider = "OpenRouter";
    }

    partial void OnSelectedProviderChanged(string value)
    {
        BaseUrl = ProviderBaseUrls.GetValueOrDefault(value, string.Empty);
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
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);

            var defaultModel = DefaultModels.GetValueOrDefault(SelectedProvider, "gpt-4o");

            // Save to settings.json
            Dictionary<string, object?> existing = [];
            if (File.Exists(SettingsPath))
            {
                var json = await File.ReadAllTextAsync(SettingsPath);
                existing = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
            }

            existing["Provider"] = SelectedProvider;
            existing["Model"] = defaultModel;
            existing["ApiKey"] = ApiKey.Trim();
            if (!string.IsNullOrWhiteSpace(BaseUrl))
                existing["BaseUrl"] = BaseUrl.Trim();

            await File.WriteAllTextAsync(SettingsPath, JsonSerializer.Serialize(existing, SerializerOptions));

            // Also save as a provider profile in providers.json
            var newProfile = new Dictionary<string, object>
            {
                ["Name"] = SelectedProvider,
                ["Provider"] = SelectedProvider,
                ["ApiKey"] = ApiKey.Trim(),
                ["BaseUrl"] = (BaseUrl ?? string.Empty).Trim(),
                ["MaxTokens"] = 32000,
            };
            var profiles = new List<Dictionary<string, object>>();
            if (File.Exists(ProfilesPath))
            {
                try
                {
                    var existingProfiles = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                        await File.ReadAllTextAsync(ProfilesPath)) ?? [];
                    // Keep existing profiles for other providers.
                    foreach (var p in existingProfiles)
                    {
                        if (p.TryGetValue("Provider", out var prov) && prov?.ToString() != SelectedProvider)
                            profiles.Add(p);
                    }
                }
                catch { /* ignore parse errors */ }
            }
            profiles.Insert(0, newProfile);
            await File.WriteAllTextAsync(ProfilesPath, JsonSerializer.Serialize(profiles, SerializerOptions));

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

    public IReadOnlyList<string> Providers { get; } =
    [
        "OpenAI", "OpenRouter", "DeepSeek", "Groq", "Mistral", "Together AI",
        "Azure OpenAI", "Google", "Ollama", "LM Studio", "Custom"
    ];
}
