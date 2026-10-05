namespace Sovrant.Web.Services;

/// <summary>One page reachable from a nav group. <paramref name="Section"/> starts a small label row (Admin only).</summary>
internal sealed record AppNavItem(string Label, string Href, string? Section = null);

/// <summary>
/// A top-level nav group. Groups with more than one item are collapsible
/// sections in the expanded rail and flyouts in the collapsed rail; the rest
/// are plain links to <see cref="Href"/>.
/// </summary>
internal sealed record AppNavGroup(string Key, string Label, string IconSvg, string Href, IReadOnlyList<AppNavItem> Items, bool AdminOnly = false)
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
    private const string Svg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" stroke-linejoin=\"round\">";

    public static readonly IReadOnlyList<AppNavGroup> Groups =
    [
        new("dashboard", "Dashboard",
            Svg + "<rect x=\"3\" y=\"3\" width=\"7\" height=\"9\" rx=\"1.5\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"5\" rx=\"1.5\"/><rect x=\"14\" y=\"12\" width=\"7\" height=\"9\" rx=\"1.5\"/><rect x=\"3\" y=\"16\" width=\"7\" height=\"5\" rx=\"1.5\"/></svg>",
            "/dashboard", [new("Dashboard", "/dashboard")]),
        new("chat", "Chat",
            Svg + "<path d=\"M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z\"/></svg>",
            "/", [new("Chat", "/")]),
        new("knowledge", "Knowledge",
            Svg + "<path d=\"M4 19.5A2.5 2.5 0 0 1 6.5 17H20\"/><path d=\"M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z\"/></svg>",
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
            Svg + "<rect x=\"4\" y=\"7\" width=\"16\" height=\"13\" rx=\"2.5\"/><path d=\"M9 7V5a3 3 0 0 1 6 0v2\"/><line x1=\"9\" y1=\"13\" x2=\"9\" y2=\"13.01\"/><line x1=\"15\" y1=\"13\" x2=\"15\" y2=\"13.01\"/><path d=\"M9 17h6\"/></svg>",
            "/agents",
            [
                new("Library", "/agents"),
                new("Orchestration", "/orchestration"),
                new("Workflows", "/workflows"),
            ]),
        new("workspace", "Projects",
            Svg + "<path d=\"M3 7.5A1.5 1.5 0 0 1 4.5 6H9l2 2.5h8.5A1.5 1.5 0 0 1 21 10v8a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18z\"/></svg>",
            "/projects", [new("Projects", "/projects")]),
        new("admin", "Admin",
            Svg + "<path d=\"M12 2l8 3.5v6c0 5-3.4 8.9-8 10.5-4.6-1.6-8-5.5-8-10.5v-6z\"/></svg>",
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
