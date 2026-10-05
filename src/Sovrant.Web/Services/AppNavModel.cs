using Sovrant.Api.Ui;

namespace Sovrant.Web.Services;

/// <summary>One page reachable from a nav group. <paramref name="Section"/> starts a small label row (Admin only).</summary>
internal sealed record AppNavItem(string Label, string Href, string? Section = null);

/// <summary>
/// A top-level nav group. Groups with more than one item are collapsible
/// sections in the expanded rail and flyouts in the collapsed rail; the rest
/// are plain links to <see cref="Href"/>.
/// </summary>
internal sealed record AppNavGroup(string Key, string Label, string IconName, string Href, IReadOnlyList<AppNavItem> Items, bool AdminOnly = false)
{
    public bool IsCollapsible => Items.Count > 1;
}

/// <summary>
/// Phase 135 — the app sidebar's groups and pages, shared by the expanded nav
/// (<c>AppNav</c>) and the collapsed rail's flyouts (<c>RailNav</c>), plus the
/// page → group lookup that opens the current page's group automatically.
/// </summary>
internal static class AppNavModel
{
    public static readonly IReadOnlyList<AppNavGroup> Groups =
    [
        new("dashboard", "Dashboard",
            IconNames.Dashboard,
            "/dashboard", [new("Dashboard", "/dashboard")]),
        new("chat", "Chat",
            IconNames.Chat,
            "/", [new("Chat", "/")]),
        new("knowledge", "Knowledge",
            IconNames.Knowledge,
            "/artifacts",
            [
                new("Artifacts", "/artifacts"),
                new("Code Templates", "/guidelines"),
                new("Documents", "/documents"),
                new("Memory", "/memory"),
                new("Skills", "/skills"),
                new("Tools", "/tools"),
            ]),
        new("agents", "Agents",
            IconNames.Agents,
            "/agents",
            [
                new("Library", "/agents"),
                new("Orchestration", "/orchestration"),
                new("Workflows", "/workflows"),
            ]),
        new("workspace", "Projects",
            IconNames.Projects,
            "/projects", [new("Projects", "/projects")]),
        new("admin", "Admin",
            IconNames.Admin,
            "/command",
            [
                new("Command Center", "/command", "Overview"),
                new("Users", "/admin", "Access"),
                new("Workspaces", "/admin/workspaces"),
                new("Providers", "/admin/providers"),
                new("Governance", "/governance", "Safety"),
                new("Trust Boundary", "/trust-boundary"),
                new("Diagnostics", "/diagnostics", "System"),
                new("Platform Integrations", "/admin/integrations"),
                new("System Integrations", "/admin/system-integrations"),
            ],
            AdminOnly: true),
    ];

    /// <summary>The group whose pages include <paramref name="path"/> ("/skills", "/admin/providers", …), or null.</summary>
    public static AppNavGroup? GroupForPath(string path)
    {
        var p = Normalize(path);
        if (IsChatPath(p))
            return Groups.First(g => g.Key == "chat");
        return Groups.FirstOrDefault(g => g.Items.Any(i => string.Equals(Normalize(i.Href), p, StringComparison.OrdinalIgnoreCase)))
            // sub-routes such as /documents/templates belong to their parent page's group
            ?? Groups.FirstOrDefault(g => g.Items.Any(i => i.Href != "/" && p.StartsWith(Normalize(i.Href) + "/", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>True when <paramref name="href"/> is the page at <paramref name="path"/>.</summary>
    public static bool IsActive(string href, string path)
    {
        var p = Normalize(path);
        return href == "/" ? IsChatPath(p) : string.Equals(Normalize(href), p, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChatPath(string p) =>
        p == "/" || string.Equals(p, "/chat", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path)
    {
        var p = path.Split('?', '#')[0].TrimEnd('/');
        return p.Length == 0 ? "/" : (p.StartsWith('/') ? p : "/" + p);
    }
}
