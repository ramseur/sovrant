using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Onboarding;

namespace Sovrant.Desktop.ViewModels;

/// <summary>One "What Sovrant can do" card. <see cref="Page"/> is null for admin-managed areas shown to members.</summary>
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
/// Phase 141 — Home's greeting and guide (replaces Phase 140's full-window Welcome): the
/// time-of-day greeting ("Welcome to Sovrant, …" on a user's first visit), the Get started pill,
/// the role-aware checklist (collapsing to "All set" + Dismiss), and the What Sovrant can do cards.
/// Content comes from the shared <see cref="OnboardingService"/>, so it matches Web.
/// </summary>
public partial class HomeGuideViewModel(OnboardingService onboarding) : ViewModelBase
{
    private string? _firstVisitUser;
    private bool _firstVisit;

    [ObservableProperty] private string _greeting = "Home";
    [ObservableProperty] private string _checklistTitle = "Get started";
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private string _pillText = string.Empty;
    [ObservableProperty] private double _progressPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChecklist), nameof(ShowAllSet), nameof(ShowPill))]
    private bool _isComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAllSet))]
    private bool _isDismissed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChecklist), nameof(ShowAllSet), nameof(ShowPill))]
    private bool _isLoaded;

    private string? _userId;

    public ObservableCollection<WelcomeAreaItem> Areas { get; } = [];
    public ObservableCollection<WelcomeChecklistItem> Checklist { get; } = [];

    public bool ShowPill => IsLoaded && !IsComplete;
    public bool ShowChecklist => IsLoaded && !IsComplete;
    public bool ShowAllSet => IsLoaded && IsComplete && !IsDismissed;

    /// <summary>Raised with a page name ("Chat", "Agents", …) when the user follows a link.</summary>
    public event Action<string>? NavigateRequested;

    /// <summary>
    /// Loads the guide for the signed-in user. The first load for a user who has never seen Home
    /// greets them with "Welcome to Sovrant" for the rest of the session and records the visit.
    /// </summary>
    public async Task LoadAsync(string userId, string displayName, bool isAdmin, string? workspaceId, int localHour)
    {
        if (!string.Equals(_firstVisitUser, userId, StringComparison.Ordinal))
        {
            _firstVisitUser = userId;
            _firstVisit = !await onboarding.HasSeenWelcomeAsync(userId).ConfigureAwait(true);
            if (_firstVisit)
                await onboarding.MarkWelcomeSeenAsync(userId).ConfigureAwait(true);
        }

        _userId = userId;
        var content = await onboarding.GetWelcomeAsync(userId, isAdmin, workspaceId).ConfigureAwait(true);
        var dismissed = await onboarding.IsGetStartedDismissedAsync(userId).ConfigureAwait(true);

        Greeting = OnboardingService.Greeting(displayName, _firstVisit, localHour);
        ChecklistTitle = content.IsAdmin ? "Get started: set up your server" : "Get started";
        ProgressText = $"{content.DoneCount} of {content.Checklist.Count} done";
        PillText = $"Get started · {content.DoneCount} of {content.Checklist.Count}";
        ProgressPercent = content.Checklist.Count == 0 ? 0 : 100.0 * content.DoneCount / content.Checklist.Count;
        IsComplete = content.DoneCount == content.Checklist.Count;
        IsDismissed = dismissed;

        Areas.Clear();
        foreach (var a in content.Areas)
            Areas.Add(new WelcomeAreaItem(a.IconName, a.Title, a.Description, a.ActionLabel, PageFor(a.Target, isAdmin)));
        Checklist.Clear();
        foreach (var c in content.Checklist)
            Checklist.Add(new WelcomeChecklistItem(c.Title, c.Subtitle, c.Done, c.ActionLabel, PageFor(c.Target, isAdmin)));
        IsLoaded = true;
    }

    /// <summary>Forget the per-user first-visit state (sign-out / user switch).</summary>
    public void Reset()
    {
        _firstVisitUser = null;
        _userId = null;
        IsLoaded = false;
        Greeting = "Home";
        Areas.Clear();
        Checklist.Clear();
    }

    [RelayCommand]
    private void Open(string? page)
    {
        if (!string.IsNullOrEmpty(page))
            NavigateRequested?.Invoke(page);
    }

    [RelayCommand]
    private async Task DismissAsync()
    {
        if (_userId is { Length: > 0 } userId)
            await onboarding.DismissGetStartedAsync(userId).ConfigureAwait(true);
        IsDismissed = true;
    }

    /// <summary>Desktop page for a guide link; null for admin-only pages when the user isn't an admin.</summary>
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
