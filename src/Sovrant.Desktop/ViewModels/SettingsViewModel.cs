using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SovrantConfig _config;
    private readonly IPermissionModeAccessor _permissionModeAccessor;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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

    public SettingsViewModel(SovrantConfig config, IPermissionModeAccessor permissionModeAccessor)
    {
        _config = config;
        _permissionModeAccessor = permissionModeAccessor;

        // Load current values from runtime config.
        _modelName = config.Model;
        _maxOutputTokens = config.MaxTokens;
        _apiKey = config.ApiKey ?? string.Empty;
        _baseUrl = config.BaseUrl?.ToString() ?? string.Empty;
        _permissionMode = permissionModeAccessor.Mode;
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant =
                value ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    partial void OnPermissionModeChanged(PermissionMode value)
    {
        _permissionModeAccessor.Mode = value;
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

            existing["Model"] = ModelName;
            existing["MaxTokens"] = MaxOutputTokens;
            existing["PermissionMode"] = PermissionMode.ToString();

            if (!string.IsNullOrWhiteSpace(ApiKey))
                existing["ApiKey"] = ApiKey;

            if (!string.IsNullOrWhiteSpace(BaseUrl))
                existing["BaseUrl"] = BaseUrl;

            var output = JsonSerializer.Serialize(existing, SerializerOptions);
            await File.WriteAllTextAsync(SettingsPath, output);

            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
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
