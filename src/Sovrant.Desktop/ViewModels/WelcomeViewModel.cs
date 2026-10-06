using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Onboarding;

namespace Sovrant.Desktop.ViewModels;

/// <summary>One Welcome info bubble. <see cref="Page"/> is null for admin-managed areas shown to members.</summary>
public sealed record WelcomeAreaItem(string IconName, string Title, string Description, string? ActionLabel, string? Page)
{
    public bool HasLink => Page is not null;
    public bool IsManaged => Page is null;
    public string LinkText => $"{ActionLabel} →";
}

/// <summary>One "Get started" row, ticked from real state.</summary>
public sealed record WelcomeChecklistItem(string Title, string Subtitle, bool Done, string ActionLabel, string? Page)
{
    public bool ShowAction => !Done && Page is not null;
    public string LinkText => $"{ActionLabel} →";
}

/// <summary>
/// Phase 140 — the full-window "Welcome to Sovrant" overlay: shown once per user after first
/// sign-in (and after setup on first run), reopened from Dashboard → Show welcome. Content comes
/// from the shared <see cref="OnboardingService"/>, so it matches Web exactly.
/// </summary>
public partial class WelcomeViewModel(OnboardingService onboarding) : ViewModelBase
{
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private string _title = "Welcome to Sovrant";
    [ObservableProperty] private string _checklistTitle = "Get started";
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private double _progressPercent;

    public ObservableCollection<WelcomeAreaItem> Areas { get; } = [];
    public ObservableCollection<WelcomeChecklistItem> Checklist { get; } = [];

    /// <summary>Raised with a page name ("Chat", "Agents", "Dashboard", …) when the user leaves via a link.</summary>
    public event Action<string>? NavigateRequested;

    /// <summary>Shows the page the first time this user signs in.</summary>
    public async Task ShowIfFirstTimeAsync(string userId, string displayName, bool isAdmin, string? workspaceId)
    {
        if (!await onboarding.HasSeenWelcomeAsync(userId).ConfigureAwait(true))
            await ShowAsync(userId, displayName, isAdmin, workspaceId).ConfigureAwait(true);
    }

    /// <summary>Loads fresh content (checklist ticks reflect the current state) and shows the page.</summary>
    public async Task ShowAsync(string userId, string displayName, bool isAdmin, string? workspaceId)
    {
        var content = await onboarding.GetWelcomeAsync(userId, isAdmin, workspaceId).ConfigureAwait(true);
        Title = $"Welcome to Sovrant, {displayName}";
        ChecklistTitle = content.IsAdmin ? "Get started: set up your server" : "Get started";
        ProgressText = $"{content.DoneCount} of {content.Checklist.Count} done";
        ProgressPercent = content.Checklist.Count == 0 ? 0 : 100.0 * content.DoneCount / content.Checklist.Count;

        Areas.Clear();
        foreach (var a in content.Areas)
            Areas.Add(new WelcomeAreaItem(a.IconName, a.Title, a.Description, a.ActionLabel, PageFor(a.Target, isAdmin)));
        Checklist.Clear();
        foreach (var c in content.Checklist)
            Checklist.Add(new WelcomeChecklistItem(c.Title, c.Subtitle, c.Done, c.ActionLabel, PageFor(c.Target, isAdmin)));

        IsVisible = true;
        await onboarding.MarkWelcomeSeenAsync(userId).ConfigureAwait(true);
    }

    [RelayCommand]
    private void Open(string? page)
    {
        if (string.IsNullOrEmpty(page)) return;
        IsVisible = false;
        NavigateRequested?.Invoke(page);
    }

    [RelayCommand]
    private void StartChatting() => Open("Chat");

    [RelayCommand]
    private void Skip() => Open("Dashboard");

    /// <summary>Desktop page for a Welcome link; null for admin-only pages when the user isn't an admin.</summary>
    public static string? PageFor(WelcomeTarget? target, bool isAdmin)
    {
        if (target is not { } t || (OnboardingService.IsAdminOnly(t) && !isAdmin))
            return null;
        return t switch
        {
            WelcomeTarget.Chat or WelcomeTarget.Privacy => "Chat",
            WelcomeTarget.Agents => "Agents",
            WelcomeTarget.Orchestration => "Orchestration",
            WelcomeTarget.Workflows => "Workflows",
            WelcomeTarget.Knowledge => "Skills",
            WelcomeTarget.Projects => "Projects",
            WelcomeTarget.Integrations => "AdminPlatformIntegrations",
            WelcomeTarget.TrustBoundary => "TrustBoundary",
            WelcomeTarget.Workspaces => "AdminWorkspaces",
            WelcomeTarget.Users => "Admin",
            WelcomeTarget.ProviderSetup => "AdminProviders",
            _ => null,
        };
    }
}
