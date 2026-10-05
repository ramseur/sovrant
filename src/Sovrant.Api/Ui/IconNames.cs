namespace Sovrant.Api.Ui;

/// <summary>
/// Phase 136 — the shared icon vocabulary. Every icon in Web and Desktop is one of
/// these names; each surface maps a name to a Lucide glyph (<c>SovrantIcons</c> on Web,
/// <c>SovrantIconMap</c> on Desktop). Call sites name the meaning, never the glyph, so
/// swapping a glyph is a one-line change in each map. Mirrors the
/// <c>VOCAB</c> list in <c>docs/design/web.html</c> / <c>desktop.html</c>.
/// </summary>
public static class IconNames
{
    // Navigation
    public const string Dashboard = "dashboard";
    public const string Chat = "chat";
    public const string Knowledge = "knowledge";
    public const string Agents = "agents";
    public const string Projects = "projects";
    public const string Admin = "admin";
    public const string Workflow = "workflow";
    public const string Orchestration = "orchestration";
    public const string Swarm = "swarm";
    public const string Team = "team";
    public const string Sequential = "sequential";
    public const string Parallel = "parallel";
    public const string Integrations = "integrations";
    public const string Workspace = "workspace";
    public const string Model = "model";
    public const string Trust = "trust";
    public const string Diagnostics = "diagnostics";
    public const string RailCollapse = "rail-collapse";
    public const string RailExpand = "rail-expand";

    // Actions
    public const string Search = "search";
    public const string Add = "add";
    public const string Close = "close";
    public const string More = "more";
    public const string Rename = "rename";
    public const string Delete = "delete";
    public const string Send = "send";
    public const string Attach = "attach";
    public const string Allow = "allow";
    public const string AllowAll = "allow-all";
    public const string Deny = "deny";
    public const string Expand = "expand";
    public const string Refresh = "refresh";
    public const string Stop = "stop";
    public const string Dropdown = "dropdown";
    public const string Back = "back";
    public const string SortAscending = "sort-asc";
    public const string SortDescending = "sort-desc";

    // Objects
    public const string Folder = "folder";
    public const string NewFolder = "folder-new";
    public const string Tool = "tool";
    public const string Package = "package";
    public const string Image = "image";
    public const string Automation = "automation";
    public const string Brand = "brand";
    public const string File = "file";
    public const string FilePdf = "file-pdf";
    public const string FileWord = "file-word";
    public const string FileSheet = "file-sheet";
    public const string FileSlides = "file-slides";
    public const string FileMarkdown = "file-markdown";

    // Status
    public const string Private = "private";
    public const string Public = "public";
    public const string Done = "done";
    public const string Failed = "failed";
    public const string Warning = "warning";

    // Brand fallbacks — Lucide ships no brand logos, so providers and integrations show
    // the icon for their kind until official, licensed logos are added.
    public const string ProviderCloud = "provider-cloud";
    public const string ProviderLocal = "provider-local";
    public const string IntegrationAutomation = "integration-automation";
    public const string IntegrationPlatform = "integration-platform";
    public const string IntegrationDatabase = "integration-database";
    public const string IntegrationSearch = "integration-search";
    public const string IntegrationDxp = "integration-dxp";

    /// <summary>Every name, for the tests that prove both surfaces resolve all of them.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Dashboard, Chat, Knowledge, Agents, Projects, Admin, Workflow, Orchestration, Swarm, Team,
        Sequential, Parallel, Integrations, Workspace, Model, Trust, Diagnostics, RailCollapse, RailExpand,
        Search, Add, Close, More, Rename, Delete, Send, Attach, Allow, AllowAll, Deny, Expand, Refresh, Stop, Dropdown, Back, SortAscending, SortDescending,
        Folder, NewFolder, Tool, Package, Image, Automation, Brand, File, FilePdf, FileWord, FileSheet,
        FileSlides, FileMarkdown,
        Private, Public, Done, Failed, Warning,
        ProviderCloud, ProviderLocal, IntegrationAutomation, IntegrationPlatform, IntegrationDatabase,
        IntegrationSearch, IntegrationDxp,
    ];

    /// <summary>The category icon for a model provider (hosted vs local runtime).</summary>
    public static string ForProvider(string provider) =>
        provider is "Ollama" or "LM Studio" ? ProviderLocal : ProviderCloud;
}
