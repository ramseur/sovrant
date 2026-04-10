using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Session;

namespace Sovrant.Desktop.ViewModels;

public partial class SidebarViewModel : ViewModelBase
{
    private readonly ISessionStore _sessionStore;

    [ObservableProperty]
    private string _selectedNavItem = "Chat";

    [ObservableProperty]
    private string _connectionStatus = "Connecting...";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _currentModel = "GPT-4o";

    [ObservableProperty]
    private string _currentProvider = "OpenAI";

    [ObservableProperty]
    private bool _isCodebaseMode;

    public ObservableCollection<SessionListItem> RecentSessions { get; } = [];

    public event EventHandler<string>? NavigationRequested;
    public event EventHandler<string>? SessionResumeRequested;

    public SidebarViewModel(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
        _ = LoadSessionsAsync();
    }

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

    partial void OnIsCodebaseModeChanged(bool value)
    {
        // TODO: Wire up codebase context toggle (e.g. enable/disable file indexing).
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
