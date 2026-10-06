using Microsoft.Extensions.DependencyInjection;
using Sovrant.Api.Ui;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Onboarding;

/// <summary>Where a Welcome link leads; each surface maps it to its own page or route.</summary>
public enum WelcomeTarget
{
    Chat,
    Agents,
    Orchestration,
    Workflows,
    Knowledge,
    Integrations,
    TrustBoundary,
    Workspaces,
    Users,
    Projects,
    ProviderSetup,
    Privacy,
}

/// <summary>
/// One info bubble on the Welcome page. <see cref="AdminManaged"/> areas are described to members
/// but carry no link (<see cref="Target"/> is null): the pages are admin-only, and the Welcome page
/// must never be a way into them.
/// </summary>
public sealed record WelcomeArea(string IconName, string Title, string Description, string? ActionLabel, WelcomeTarget? Target, bool AdminManaged = false);

/// <summary>One "Get started" checklist row, ticked from real state.</summary>
public sealed record ChecklistItem(string Key, string Title, string Subtitle, bool Done, string ActionLabel, WelcomeTarget Target);

/// <summary>Everything the Welcome page shows for one user.</summary>
public sealed record WelcomeContent(bool IsAdmin, IReadOnlyList<WelcomeArea> Areas, IReadOnlyList<ChecklistItem> Checklist)
{
    public int DoneCount => Checklist.Count(i => i.Done);
}

/// <summary>
/// Phase 140 — the role-aware "Welcome to Sovrant" page and its checklist, shared by Web and
/// Desktop so both show identical copy. Every tick comes from real state (provider profiles,
/// workspace settings, members, MCP servers, agents, sessions, agent runs); nothing is hard-coded.
/// The "seen" flag lives in <c>user_preferences</c>, never on disk.
/// </summary>
public sealed class OnboardingService(IServiceProvider services)
{
    /// <summary>The line under the Welcome title.</summary>
    public const string Tagline =
        "Your private AI workspace. Bring any model, keep your data, and stay in control of what your agents can do.";

    /// <summary>True once the user has had their first Home visit (greeted with "Welcome to Sovrant").</summary>
    public async Task<bool> HasSeenWelcomeAsync(string userId, CancellationToken ct = default)
    {
        var prefs = services.GetService<IUserPreferenceStore>();
        if (prefs is null || string.IsNullOrEmpty(userId))
            return true; // without a preference store, never force the page
        return await prefs.GetAsync(userId, UserPreferenceKeys.WelcomeSeen, ct).ConfigureAwait(false) == "true";
    }

    /// <summary>Records the user's first Home visit (Phase 141; Phase 140 used it for the Welcome page).</summary>
    public Task MarkWelcomeSeenAsync(string userId, CancellationToken ct = default) =>
        SetFlagAsync(userId, UserPreferenceKeys.WelcomeSeen, ct);

    /// <summary>Records that the user opened a Knowledge page (ticks the member checklist item).</summary>
    public Task MarkKnowledgeVisitedAsync(string userId, CancellationToken ct = default) =>
        SetFlagAsync(userId, UserPreferenceKeys.KnowledgeVisited, ct);

    /// <summary>Phase 141 — true once the user dismissed Home's "All set" line.</summary>
    public async Task<bool> IsGetStartedDismissedAsync(string userId, CancellationToken ct = default)
    {
        var prefs = services.GetService<IUserPreferenceStore>();
        if (prefs is null || string.IsNullOrEmpty(userId))
            return false;
        return await prefs.GetAsync(userId, UserPreferenceKeys.GetStartedDismissed, ct).ConfigureAwait(false) == "true";
    }

    public Task DismissGetStartedAsync(string userId, CancellationToken ct = default) =>
        SetFlagAsync(userId, UserPreferenceKeys.GetStartedDismissed, ct);

    /// <summary>
    /// Home's heading: "Welcome to Sovrant, sam" on the user's first visit, then "Welcome back, sam".
    /// Time-neutral on purpose (Phase 142): a time-of-day greeting said "Good morning" to people
    /// working through the night.
    /// </summary>
    public static string Greeting(string displayName, bool firstVisit) =>
        firstVisit ? $"Welcome to Sovrant, {displayName}" : $"Welcome back, {displayName}";

    /// <summary>The part of an email before '@' (or the id itself), for greetings.</summary>
    public static string DisplayName(string? emailOrId)
    {
        var s = emailOrId ?? string.Empty;
        var at = s.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? s[..at] : s;
    }

    private async Task SetFlagAsync(string userId, string key, CancellationToken ct)
    {
        var prefs = services.GetService<IUserPreferenceStore>();
        if (prefs is null || string.IsNullOrEmpty(userId))
            return;
        if (await prefs.GetAsync(userId, key, ct).ConfigureAwait(false) != "true")
            await prefs.SetAsync(userId, key, "true", ct).ConfigureAwait(false);
    }

    /// <summary>Builds the Welcome page for <paramref name="userId"/> in <paramref name="workspaceId"/>.</summary>
    public async Task<WelcomeContent> GetWelcomeAsync(string userId, bool isAdmin, string? workspaceId, CancellationToken ct = default)
    {
        var trustBoundaryOn = await IsTrustBoundaryOnAsync(ct).ConfigureAwait(false);
        var checklist = isAdmin
            ? await AdminChecklistAsync(userId, ct).ConfigureAwait(false)
            : await MemberChecklistAsync(userId, workspaceId, ct).ConfigureAwait(false);
        return new WelcomeContent(isAdmin, Areas(isAdmin, trustBoundaryOn), checklist);
    }

    /// <summary>
    /// The info bubbles. Integrations, Trust Boundary / Governance and Workspaces are admin-only
    /// pages today, so members see them described as managed by their admin, with no link.
    /// </summary>
    public static IReadOnlyList<WelcomeArea> Areas(bool isAdmin, bool trustBoundaryOn) => isAdmin
        ?
        [
            .. Shared,
            new(IconNames.Integrations, "Integrations", "Connect MCP servers and platforms so agents can work in your tools.", "Open Integrations", WelcomeTarget.Integrations),
            new(IconNames.Trust, "Trust Boundary & Governance", "Redact sensitive data before it leaves, and decide which tools agents may use.", "Open Trust Boundary", WelcomeTarget.TrustBoundary),
            new(IconNames.Workspace, "Workspaces & admin", "Choose which providers, integrations and people each workspace gets.", "Open Workspaces", WelcomeTarget.Workspaces),
        ]
        :
        [
            .. Shared,
            new(IconNames.Projects, "Projects", "Keep the files and conversations for a piece of work together.", "Open Projects", WelcomeTarget.Projects),
            new(IconNames.Integrations, "Integrations", "Your admin connects MCP servers and platforms so agents can work in your tools.", null, null, AdminManaged: true),
            // Only claim redaction when the Trust Boundary is on (it's off by default). New conversations,
            // agent runs and workflows are created private (is_private = 1); making one Public shows it to
            // teammates in the same workspace (Home → Activity) and to admins.
            new(IconNames.Private, "Privacy & governance", trustBoundaryOn
                ? "Your conversations are private by default. Make one Public to share it with your workspace. Sensitive data is redacted before it reaches a model, and your admin decides which tools agents may use."
                : "Your conversations are private by default. Make one Public to share it with your workspace. Your admin decides which tools agents may use.", null, null, AdminManaged: true),
        ];

    private static readonly WelcomeArea[] Shared =
    [
        new(IconNames.Chat, "Chat with any model", "Use OpenAI, Anthropic, OpenRouter or a local model, and switch any time. Keys stay encrypted on your server.", "Start a chat", WelcomeTarget.Chat),
        new(IconNames.Agents, "Agents", "Build assistants with their own instructions, tools and knowledge.", "Open Agents", WelcomeTarget.Agents),
        new(IconNames.Team, "Teams & Swarms", "Put agents to work together: in sequence, in parallel, or as a swarm.", "Open Orchestration", WelcomeTarget.Orchestration),
        new(IconNames.Workflow, "Workflows", "Plan multi-step work, review the plan, then let it run on a schedule.", "Open Workflows", WelcomeTarget.Workflows),
        new(IconNames.Knowledge, "Knowledge", "Skills, code templates, memory and documents your agents draw on.", "Open Knowledge", WelcomeTarget.Knowledge),
    ];

    /// <summary>Targets that are admin-only pages today; never offered to members.</summary>
    public static bool IsAdminOnly(WelcomeTarget target) =>
        target is WelcomeTarget.Integrations or WelcomeTarget.TrustBoundary or WelcomeTarget.Workspaces
            or WelcomeTarget.Users or WelcomeTarget.ProviderSetup;

    private async Task<IReadOnlyList<ChecklistItem>> AdminChecklistAsync(string userId, CancellationToken ct)
    {
        var workspaces = await WorkspacesForAsync(userId, ct).ConfigureAwait(false);
        var profiles = await ProfilesAsync(userId, workspaces, ct).ConfigureAwait(false);

        var wsSettings = services.GetService<IWorkspaceSettingsStore>();
        var anyEnabled = false;
        if (wsSettings is not null)
        {
            foreach (var ws in workspaces)
            {
                var raw = await wsSettings.GetAsync(ws.WorkspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(raw)) { anyEnabled = true; break; }
            }
        }

        var invited = false;
        var wsService = services.GetService<IWorkspaceService>();
        if (wsService is not null)
        {
            foreach (var ws in workspaces)
            {
                if ((await wsService.ListMembersAsync(ws.WorkspaceId, ct).ConfigureAwait(false)).Count > 1) { invited = true; break; }
            }
        }

        var mcp = services.GetService<IMcpServerStore>();
        var integrations = mcp is null ? 0 : (await mcp.GetAllAsync(ct).ConfigureAwait(false)).Count;
        var ownAgents = (await AgentsAsync(workspaceId: null, ct).ConfigureAwait(false)).Count(a => a.Tier == "User");

        var provider = profiles.Count > 0 ? profiles[0] : null;
        return
        [
            new("provider", "Connect a model provider",
                provider is null ? "OpenAI, OpenRouter, Anthropic, or a local model" : $"{provider.ProviderKind} connected",
                provider is not null, "Set up a provider", WelcomeTarget.ProviderSetup),
            new("workspace-providers", "Enable providers for a workspace", "Admins choose what each workspace can use",
                anyEnabled, "Open Workspaces", WelcomeTarget.Workspaces),
            new("invite", "Invite your team", "Add people and set their roles", invited, "Open Users", WelcomeTarget.Users),
            new("integration", "Connect an integration",
                integrations > 0 ? $"{integrations} connected" : "GitHub, Slack, databases and more",
                integrations > 0, "Open Integrations", WelcomeTarget.Integrations),
            new("agent", "Create your first agent", "Give it instructions, tools and knowledge", ownAgents > 0, "Create agent", WelcomeTarget.Agents),
        ];
    }

    private async Task<IReadOnlyList<ChecklistItem>> MemberChecklistAsync(string userId, string? workspaceId, CancellationToken ct)
    {
        var prefs = services.GetService<IUserPreferenceStore>();
        string? model = null, provider = null, profileId = null, knowledgeVisited = null;
        if (prefs is not null)
        {
            model = await prefs.GetAsync(userId, UserPreferenceKeys.Model, ct).ConfigureAwait(false);
            provider = await prefs.GetAsync(userId, UserPreferenceKeys.Provider, ct).ConfigureAwait(false);
            profileId = await prefs.GetAsync(userId, UserPreferenceKeys.ActiveProviderProfileId, ct).ConfigureAwait(false);
            knowledgeVisited = await prefs.GetAsync(userId, UserPreferenceKeys.KnowledgeVisited, ct).ConfigureAwait(false);
        }
        var hasModel = !string.IsNullOrWhiteSpace(model) && !string.IsNullOrWhiteSpace(profileId);

        var sessions = services.GetService<ISessionStore>();
        var conversations = sessions is null ? 0 : (await sessions.ListAsync(userId, ct).ConfigureAwait(false)).Count;

        var runs = services.GetService<IAgentRunStore>();
        var triedAgent = runs is not null
            && (await runs.ListAsync(new AgentRunFilter(UserId: userId), limit: 1, ct).ConfigureAwait(false)).Count > 0;
        var agentCount = (await AgentsAsync(workspaceId, ct).ConfigureAwait(false)).Count;

        return
        [
            new("model", "Pick a model",
                hasModel ? (string.IsNullOrWhiteSpace(provider) ? model! : $"{model} via {provider}") : "Choose one from the model menu",
                hasModel, "Pick a model", WelcomeTarget.Chat),
            new("conversation", "Start your first conversation", "Ask anything, or try a suggestion", conversations > 0, "Start chatting", WelcomeTarget.Chat),
            new("agent", "Try an agent",
                agentCount > 0 ? $"Your workspace has {agentCount} agent{(agentCount == 1 ? "" : "s")} ready" : "Agents can research, write and build for you",
                triedAgent, "Browse agents", WelcomeTarget.Agents),
            new("knowledge", "Explore Knowledge", "Skills and templates your agents use", knowledgeVisited == "true", "Open Knowledge", WelcomeTarget.Knowledge),
        ];
    }

    private async Task<IReadOnlyList<Workspace>> WorkspacesForAsync(string userId, CancellationToken ct)
    {
        var ws = services.GetService<IWorkspaceService>();
        return ws is null ? [] : await ws.ListForUserAsync(userId, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ProviderProfile>> ProfilesAsync(string userId, IReadOnlyList<Workspace> workspaces, CancellationToken ct)
    {
        var store = services.GetService<IProviderProfileStore>();
        if (store is null)
            return [];
        var all = new List<ProviderProfile>(await store.ListAsync(userId, ct).ConfigureAwait(false));
        foreach (var ws in workspaces)
            all.AddRange(await store.ListByWorkspaceAsync(ws.WorkspaceId, ct).ConfigureAwait(false));
        return all;
    }

    private async Task<IReadOnlyList<KnowledgePage>> AgentsAsync(string? workspaceId, CancellationToken ct)
    {
        var knowledge = services.GetService<IKnowledgeStore>();
        return knowledge is null ? [] : await knowledge.GetAllAsync("agents", workspaceId ?? string.Empty, ct).ConfigureAwait(false);
    }

    private async Task<bool> IsTrustBoundaryOnAsync(CancellationToken ct)
    {
        var config = services.GetService<SovrantConfig>();
        var wsSettings = services.GetService<IWorkspaceSettingsStore>();
        var global = wsSettings is null ? null : await wsSettings.GetGlobalAsync(WorkspaceSettingsKeys.TrustBoundaryEnabled, ct).ConfigureAwait(false);
        return bool.TryParse(global, out var on) ? on : config?.TrustBoundary.Enabled ?? false;
    }
}
