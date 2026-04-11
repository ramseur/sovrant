using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Session;

namespace Sovrant.Desktop.ViewModels;

public partial class SidebarViewModel : ViewModelBase
{
    private readonly ISessionStore _sessionStore;

    public ActiveContextViewModel ActiveContext { get; }

    [ObservableProperty]
    private string _selectedNavItem = "Chat";

    [ObservableProperty]
    private bool _isCollapsed;

    [ObservableProperty]
    private string _connectionStatus = "Connected";

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private string _currentModel = string.Empty;

    [ObservableProperty]
    private string _currentProvider = string.Empty;

    [ObservableProperty]
    private string _currentUserName = Environment.UserName;

    [ObservableProperty]
    private string _userInitial = Environment.UserName.Length > 0
        ? Environment.UserName[..1].ToUpperInvariant()
        : "U";

    public ObservableCollection<SessionListItem> RecentSessions { get; } = [];

    public event EventHandler<string>? NavigationRequested;
    public event EventHandler<string>? SessionResumeRequested;

    public SidebarViewModel(ISessionStore sessionStore, SovrantConfig config, ActiveContextViewModel activeContext)
    {
        _sessionStore = sessionStore;
        ActiveContext = activeContext;
        LoadFromConfig(config);
        _ = LoadSessionsAsync();
    }

    /// <summary>Reads model and provider info from the current config.</summary>
    public void LoadFromConfig(SovrantConfig config)
    {
        CurrentModel = ShortenModelName(config.Model);

        // Infer provider from base URL.
        var url = config.BaseUrl?.ToString() ?? string.Empty;
        CurrentProvider = url switch
        {
            _ when url.Contains("openrouter", StringComparison.OrdinalIgnoreCase) => "OpenRouter",
            _ when url.Contains("deepseek", StringComparison.OrdinalIgnoreCase) => "DeepSeek",
            _ when url.Contains("groq", StringComparison.OrdinalIgnoreCase) => "Groq",
            _ when url.Contains("mistral", StringComparison.OrdinalIgnoreCase) => "Mistral",
            _ when url.Contains("together", StringComparison.OrdinalIgnoreCase) => "Together AI",
            _ when url.Contains("localhost:11434", StringComparison.Ordinal) => "Ollama",
            _ when url.Contains("localhost:1234", StringComparison.Ordinal) => "LM Studio",
            _ when string.IsNullOrEmpty(url) => "OpenAI",
            _ => "Custom",
        };

        // Read saved provider name from settings.json if available.
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".sovrant", "settings.json");
            if (File.Exists(path))
            {
                var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                if (json.RootElement.TryGetProperty("Provider", out var prop) &&
                    prop.GetString() is { Length: > 0 } saved)
                    CurrentProvider = saved;
            }
        }
        catch { /* use inferred provider */ }

        IsConnected = !string.IsNullOrWhiteSpace(config.ApiKey);
        ConnectionStatus = IsConnected ? "Connected" : "No API key";
    }

    /// <summary>
    /// Shortens a model ID like "google/gemma-4-26b-a4b-it:free" to "gemma-4-26b-a4b-it".
    /// Strips the provider prefix and common suffixes like ":free".
    /// </summary>
    internal static string ShortenModelName(string model)
    {
        if (string.IsNullOrEmpty(model)) return model;

        // Strip provider prefix (e.g. "google/", "meta-llama/")
        var slashIdx = model.IndexOf('/', StringComparison.Ordinal);
        if (slashIdx >= 0 && slashIdx < model.Length - 1)
            model = model[(slashIdx + 1)..];

        // Strip common suffixes
        foreach (var suffix in new[] { ":free", ":extended" })
        {
            if (model.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                model = model[..^suffix.Length];
                break;
            }
        }

        return model;
    }

    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    private void Navigate(string pageName)
    {
        SelectedNavItem = pageName;
        NavigationRequested?.Invoke(this, pageName);
    }

    [RelayCommand]
    private void ResumeSession(string sessionId)
    {
        SelectedNavItem = "Chat";
        SessionResumeRequested?.Invoke(this, sessionId);
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(string sessionId)
    {
        await _sessionStore.DeleteAsync(sessionId);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var item = RecentSessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (item is not null)
                RecentSessions.Remove(item);
        });
    }

    [RelayCommand]
    private async Task RefreshSessionsAsync()
    {
        await LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        var ids = await _sessionStore.ListAsync();
        var items = new List<SessionListItem>();

        foreach (var id in ids.Take(20))
        {
            var entries = await _sessionStore.LoadAsync(id);
            var firstUser = entries.FirstOrDefault(e => e.Role == "user");
            var label = firstUser?.Content ?? id;
            if (label.Length > 40)
                label = string.Concat(label.AsSpan(0, 37), "...");

            items.Add(new SessionListItem
            {
                SessionId = id,
                Label = label,
                Timestamp = entries.Count > 0 ? entries[^1].Timestamp : DateTimeOffset.MinValue,
                MessageCount = entries.Count(e => e.Role is "user" or "assistant"),
            });
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RecentSessions.Clear();
            foreach (var item in items.OrderByDescending(s => s.Timestamp))
                RecentSessions.Add(item);
        });
    }

}

public partial class SessionListItem : ViewModelBase
{
    [ObservableProperty]
    private string _sessionId = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _timestamp;

    [ObservableProperty]
    private int _messageCount;
}
