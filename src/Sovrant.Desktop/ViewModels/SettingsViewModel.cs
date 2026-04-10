using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sovrant.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
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

    partial void OnIsDarkModeChanged(bool value)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant =
                value ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    public IReadOnlyList<string> Providers { get; } =
    [
        "OpenAI", "DeepSeek", "Groq", "Mistral", "Together AI",
        "Azure OpenAI", "Google", "OpenRouter", "Ollama", "LM Studio", "Custom"
    ];
}
