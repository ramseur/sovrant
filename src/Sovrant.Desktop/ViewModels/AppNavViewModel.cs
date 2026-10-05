using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Ui;

namespace Sovrant.Desktop.ViewModels;

/// <summary>
/// Phase 135 — the Desktop app sidebar's nav, mirroring Web's AppNavModel:
/// Dashboard / Chat / Projects are plain links; Knowledge / Agents / Admin are
/// collapsible sections in the expanded rail (one open at a time, the current
/// page's group by default) and flyouts in the collapsed rail.
/// </summary>
public partial class AppNavViewModel : ViewModelBase
{
    private sealed record Item(string Label, string Page, string? Section = null);
    private sealed record Group(string Key, string Label, string IconName, IReadOnlyList<Item> Items, bool AdminOnly = false)
    {
        public bool IsCollapsible => Items.Count > 1;
    }

    private static readonly IReadOnlyList<Group> s_groups =
    [
        new("dashboard", "Dashboard", IconNames.Dashboard, [new("Dashboard", "Dashboard")]),
        new("chat", "Chat", IconNames.Chat, [new("Chat", "Chat")]),
        new("knowledge", "Knowledge", IconNames.Knowledge,
        [
            new("Artifacts", "Artifacts"), new("Code Templates", "Guidelines"), new("Documents", "Documents"),
            new("Memory", "Memory"), new("Skills", "Skills"), new("Tools", "Tools"),
        ]),
        new("agents", "Agents", IconNames.Agents,
            [new("Library", "Agents"), new("Orchestration", "Orchestration"), new("Workflows", "Workflows")]),
        new("workspace", "Projects", IconNames.Projects, [new("Projects", "Projects")]),
        new("admin", "Admin", IconNames.Admin,
        [
            new("Command Center", "CommandCenter", "Overview"),
            new("Users", "Admin", "Access"), new("Workspaces", "AdminWorkspaces"), new("Providers", "AdminProviders"),
            new("Governance", "Governance", "Safety"), new("Trust Boundary", "TrustBoundary"),
            new("Diagnostics", "Diagnostics", "System"), new("Platform Integrations", "AdminPlatformIntegrations"),
            new("System Integrations", "AdminSystemIntegrations"),
        ], AdminOnly: true),
    ];

    private readonly MainViewModel _main;

    // null = auto (the current page's group is open); "" = none; otherwise a group key.
    private string? _openOverride;

    public AppNavViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SelectedGroup) or nameof(MainViewModel.IsAdmin))
                Rebuild();
        };
        _main.Sidebar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SidebarViewModel.SelectedNavItem))
            {
                _openOverride = null; // navigating opens the new page's group
                Rebuild();
            }
        };
        Rebuild();
    }

    /// <summary>Expanded rail: group headers, Admin's section labels, and the open group's pages, flattened.</summary>
    public ObservableCollection<AppNavRowViewModel> Rows { get; } = [];

    /// <summary>Collapsed rail: one entry per group, each with its flyout items.</summary>
    public ObservableCollection<AppNavRowViewModel> Groups { get; } = [];

    private string? CurrentGroupKey
    {
        get
        {
            var page = _main.Sidebar.SelectedNavItem;
            var byPage = s_groups.FirstOrDefault(g => g.Items.Any(i => i.Page == page))?.Key;
            return _main.SelectedGroup is "dashboard" or "chat" ? _main.SelectedGroup : byPage ?? _main.SelectedGroup;
        }
    }

    private string? OpenKey => _openOverride is null ? CurrentGroupKey : (_openOverride.Length == 0 ? null : _openOverride);

    private void Rebuild()
    {
        var current = CurrentGroupKey;
        var open = OpenKey;
        var page = _main.Sidebar.SelectedNavItem;
        var visible = s_groups.Where(g => !g.AdminOnly || _main.IsAdmin).ToList();

        Rows.Clear();
        foreach (var g in visible)
        {
            var isOpen = g.IsCollapsible && g.Key == open;
            Rows.Add(AppNavRowViewModel.ForGroup(g.Key, g.Label, g.IconName, g.IsCollapsible, isOpen,
                isActive: g.Key == current && !isOpen));
            if (!isOpen)
                continue;
            foreach (var item in g.Items)
            {
                if (item.Section is not null)
                    Rows.Add(AppNavRowViewModel.ForSection(item.Section));
                Rows.Add(AppNavRowViewModel.ForItem(g.Key, item.Label, item.Page, isActive: item.Page == page));
            }
        }

        Groups.Clear();
        foreach (var g in visible)
        {
            var row = AppNavRowViewModel.ForGroup(g.Key, g.Label, g.IconName, g.IsCollapsible, false, g.Key == current);
            if (g.IsCollapsible)
            {
                foreach (var item in g.Items)
                {
                    if (item.Section is not null)
                        row.FlyoutItems.Add(AppNavRowViewModel.ForSection(item.Section));
                    row.FlyoutItems.Add(AppNavRowViewModel.ForItem(g.Key, item.Label, item.Page, item.Page == page));
                }
            }
            Groups.Add(row);
        }
    }

    /// <summary>Activates a row: toggles a section, follows a plain group link, or opens a page.</summary>
    [RelayCommand]
    private void Activate(AppNavRowViewModel row)
    {
        switch (row.Kind)
        {
            case AppNavRowKind.Group when row.IsCollapsible:
                _openOverride = OpenKey == row.GroupKey ? string.Empty : row.GroupKey;
                Rebuild();
                break;
            case AppNavRowKind.Group:
                _main.SelectGroupCommand.Execute(row.GroupKey);
                break;
            case AppNavRowKind.Item:
                _main.SelectedGroup = row.GroupKey;
                _main.Sidebar.NavigateCommand.Execute(row.Page);
                break;
        }
    }
}

public enum AppNavRowKind
{
    Group,
    Section,
    Item,
}

/// <summary>One row of the Desktop app nav (group header, section label, or page).</summary>
public partial class AppNavRowViewModel : ViewModelBase
{
    public AppNavRowKind Kind { get; private init; }
    public string GroupKey { get; private init; } = string.Empty;
    public string Label { get; private init; } = string.Empty;
    public string? Page { get; private init; }
    /// <summary>An <see cref="IconNames"/> value (group rows only).</summary>
    public string? IconName { get; private init; }
    public bool IsCollapsible { get; private init; }
    public bool IsOpen { get; private init; }
    public bool IsActive { get; private init; }

    public bool IsGroup => Kind == AppNavRowKind.Group;
    public bool IsSection => Kind == AppNavRowKind.Section;
    public bool IsItem => Kind == AppNavRowKind.Item;
    public bool IsChatGroup => IsGroup && GroupKey == "chat";

    /// <summary>Collapsed rail: whether clicking the icon opens a flyout (multi-page groups and Chat).</summary>
    public bool HasFlyout => IsCollapsible || IsChatGroup;
    public double ChevronAngle => IsOpen ? 90 : 0;

    /// <summary>Collapsed rail only: the group's pages (and Admin's section labels).</summary>
    public ObservableCollection<AppNavRowViewModel> FlyoutItems { get; } = [];
    public bool HasFlyoutItems => IsCollapsible;

    public static AppNavRowViewModel ForGroup(string key, string label, string iconName, bool collapsible, bool open, bool isActive) =>
        new() { Kind = AppNavRowKind.Group, GroupKey = key, Label = label, IconName = iconName, IsCollapsible = collapsible, IsOpen = open, IsActive = isActive };

    public static AppNavRowViewModel ForSection(string label) =>
        new() { Kind = AppNavRowKind.Section, Label = label.ToUpperInvariant() };

    public static AppNavRowViewModel ForItem(string groupKey, string label, string page, bool isActive) =>
        new() { Kind = AppNavRowKind.Item, GroupKey = groupKey, Label = label, Page = page, IsActive = isActive };
}
