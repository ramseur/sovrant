using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Desktop.ViewModels;

public partial class IntegrationsViewModel : ViewModelBase
{
    private readonly IMcpServerStore _serverStore;
    private readonly McpClientRegistry _clientRegistry;
    private readonly McpToolRegistrar _registrar;
    private readonly McpOAuthService _oauthService;
    private readonly IMcpTrustRuleStore _trustRuleStore;
    private readonly IWorkspaceService _workspaceSvc;
    private readonly IPrincipalAccessor _principal;
    private readonly ActiveContextViewModel? _activeContext;
    private string? _workspaceId;

    // ── Tab ──────────────────────────────────────────────────────────────────
    /// <summary>"gallery" | "connected"</summary>
    [ObservableProperty]
    private string _activeTab = "gallery";

    // ── Connected tab ────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private McpServerItem? _selectedServer;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _newServerName = string.Empty;

    [ObservableProperty]
    private string _newServerCommand = string.Empty;

    [ObservableProperty]
    private string _newServerArgs = string.Empty;

    [ObservableProperty]
    private string _newServerUrl = string.Empty;

    [ObservableProperty]
    private string _newServerBearer = string.Empty;

    [ObservableProperty]
    private string _newServerHeadersJson = string.Empty;

    [ObservableProperty]
    private string _newServerJson = string.Empty;

    /// <summary>"json" | "stdio" | "http"</summary>
    [ObservableProperty]
    private string _addMode = "json";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private readonly List<McpServerItem> _allServers = [];
    public ObservableCollection<McpServerItem> FilteredServers { get; } = [];

    // ── Gallery tab ──────────────────────────────────────────────────────────
    [ObservableProperty]
    private CatalogEntryViewModel? _selectedCatalogEntry;

    [ObservableProperty]
    private string _galleryApiKey = string.Empty;

    [ObservableProperty]
    private string _galleryUrl = string.Empty;

    [ObservableProperty]
    private bool _isGalleryConnecting;

    [ObservableProperty]
    private bool _isOAuthWaiting;

    [ObservableProperty]
    private string _galleryClientId = string.Empty;

    // Trust rules form state
    [ObservableProperty]
    private string _newRulePattern = string.Empty;

    [ObservableProperty]
    private string _newRuleAction = "RequireConfirmation";

    [ObservableProperty]
    private string _newRuleReason = string.Empty;

    // All catalog entries (for lookup)
    public ObservableCollection<CatalogEntryViewModel> CatalogEntries { get; } = [];

    // Deduplicated gallery list — one card per group
    public ObservableCollection<CatalogEntryViewModel> GalleryDisplayEntries { get; } = [];

    // Variants for the currently selected group (null = single entry, not grouped)
    [ObservableProperty]
    private List<CatalogEntryViewModel>? _selectedGroupVariants;

    [ObservableProperty]
    private int _selectedGroupTabIndex;

    public bool HasGroupTabs => SelectedGroupVariants?.Count > 1;
    public string GroupTab0Label => SelectedGroupVariants?.Count > 0 ? (SelectedGroupVariants[0].TabLabel ?? SelectedGroupVariants[0].Name) : string.Empty;
    public string GroupTab1Label => SelectedGroupVariants?.Count > 1 ? (SelectedGroupVariants[1].TabLabel ?? SelectedGroupVariants[1].Name) : string.Empty;
    public bool IsGroupTab0Active => SelectedGroupTabIndex == 0;
    public bool IsGroupTab1Active => SelectedGroupTabIndex == 1;
    public string ActiveGroupTab => SelectedGroupTabIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public IntegrationsViewModel(
        IMcpServerStore serverStore,
        McpClientRegistry clientRegistry,
        McpToolRegistrar registrar,
        McpOAuthService oauthService,
        IMcpTrustRuleStore trustRuleStore,
        IWorkspaceService workspaceSvc,
        IPrincipalAccessor principal,
        ActiveContextViewModel? activeContext = null)
    {
        _serverStore = serverStore;
        _clientRegistry = clientRegistry;
        _registrar = registrar;
        _oauthService = oauthService;
        _trustRuleStore = trustRuleStore;
        _workspaceSvc = workspaceSvc;
        _principal = principal;
        _activeContext = activeContext;

        var seenGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in IntegrationCatalog.All)
        {
            var vm = new CatalogEntryViewModel(entry);
            CatalogEntries.Add(vm);
            var groupKey = entry.GroupName ?? entry.Id;
            if (seenGroups.Add(groupKey))
                GalleryDisplayEntries.Add(vm);
        }

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var uid = _principal.UserId;
        if (uid is not null)
        {
            var ws = await _workspaceSvc.GetPersonalAsync(uid).ConfigureAwait(true);
            _workspaceId = ws?.WorkspaceId;
        }
        await LoadServersAsync().ConfigureAwait(true);
    }

    // ── Tab commands ─────────────────────────────────────────────────────────
    [RelayCommand]
    private void SwitchTab(string tab)
    {
        ActiveTab = tab;
        SelectedCatalogEntry = null;
        SelectedGroupVariants = null;
        SelectedGroupTabIndex = 0;
        SelectedServer = null;
        StatusMessage = string.Empty;
    }

    partial void OnSelectedGroupTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGroupTab0Active));
        OnPropertyChanged(nameof(IsGroupTab1Active));
        OnPropertyChanged(nameof(ActiveGroupTab));
    }

    partial void OnSelectedGroupVariantsChanged(List<CatalogEntryViewModel>? value)
    {
        OnPropertyChanged(nameof(HasGroupTabs));
        OnPropertyChanged(nameof(GroupTab0Label));
        OnPropertyChanged(nameof(GroupTab1Label));
        OnPropertyChanged(nameof(IsGroupTab0Active));
        OnPropertyChanged(nameof(IsGroupTab1Active));
    }

    // ── Gallery commands ─────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectCatalogEntry(CatalogEntryViewModel entry)
    {
        var groupName = entry.GroupName;
        if (groupName is not null)
        {
            SelectedGroupVariants = CatalogEntries.Where(e => e.GroupName == groupName).ToList();
            SelectedGroupTabIndex = 0;
            SelectedCatalogEntry = SelectedGroupVariants[0];
        }
        else
        {
            SelectedGroupVariants = null;
            SelectedGroupTabIndex = 0;
            SelectedCatalogEntry = entry;
        }
        GalleryApiKey = string.Empty;
        GalleryUrl = string.Empty;
        GalleryClientId = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void SelectGroupTab(string tabIndexStr)
    {
        if (!int.TryParse(tabIndexStr, out var idx) || SelectedGroupVariants is null || idx >= SelectedGroupVariants.Count) return;
        SelectedGroupTabIndex = idx;
        SelectedCatalogEntry = SelectedGroupVariants[idx];
        GalleryApiKey = string.Empty;
        GalleryUrl = string.Empty;
        GalleryClientId = string.Empty;
    }

    [RelayCommand]
    private async Task ConnectFromGalleryAsync()
    {
        var entry = SelectedCatalogEntry;
        if (entry is null || entry.Kind == IntegrationKind.LlmProvider) return;

        if (entry.NeedsApiKey && string.IsNullOrWhiteSpace(GalleryApiKey))
        {
            StatusMessage = $"{entry.ApiKeyLabel} is required.";
            return;
        }
        if (entry.NeedsEndpoint && string.IsNullOrWhiteSpace(GalleryUrl))
        {
            StatusMessage = $"{entry.EndpointLabel} is required.";
            return;
        }

        IsGalleryConnecting = true;
        StatusMessage = string.Empty;
        try
        {
            var config = BuildConfig(entry, GalleryApiKey.Trim(), GalleryUrl.Trim());
            await _serverStore.UpsertAsync(entry.Id, config).ConfigureAwait(true);
            await ConnectAndRefreshAsync(entry.Id, config).ConfigureAwait(true);
            ActiveTab = "connected";
            SelectedServer = FilteredServers.FirstOrDefault(s => s.Name == entry.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsGalleryConnecting = false;
        }
    }

    [RelayCommand]
    private async Task ConnectWithOAuthAsync()
    {
        var entry = SelectedCatalogEntry;
        if (entry is null || entry.Kind == IntegrationKind.LlmProvider) return;

        if (string.IsNullOrWhiteSpace(GalleryClientId))
        {
            StatusMessage = "OAuth Client ID is required.";
            return;
        }

        IsGalleryConnecting = true;
        IsOAuthWaiting = false;
        StatusMessage = string.Empty;

        try
        {
            var config = BuildOAuthConfig(entry, GalleryClientId.Trim(), GalleryUrl.Trim());
            await _serverStore.UpsertAsync(entry.Id, config).ConfigureAwait(true);

            await using var callbackListener = McpOAuthCallbackListener.Start();

            var authUrl = await _oauthService.GenerateAuthorizationUrlAsync(
                entry.Id, callbackListener.RedirectUri).ConfigureAwait(true);

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            IsOAuthWaiting = true;
            StatusMessage = "Browser opened. Waiting for authorization...";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var (code, state) = await callbackListener.WaitForCallbackAsync(cts.Token).ConfigureAwait(true);

            await _oauthService.ExchangeCodeAsync(state, code).ConfigureAwait(true);

            ActiveTab = "connected";
            await LoadServersAsync().ConfigureAwait(true);
            SelectedServer = FilteredServers.FirstOrDefault(s => s.Name == entry.Id);
            StatusMessage = $"Connected to '{entry.Name}' via OAuth.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Authorization timed out. Please try again.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"OAuth failed: {ex.Message}";
        }
        finally
        {
            IsGalleryConnecting = false;
            IsOAuthWaiting = false;
        }
    }

    private static McpServerConfig BuildOAuthConfig(CatalogEntryViewModel entry, string clientId, string endpointOverride)
    {
        var urlStr = !string.IsNullOrEmpty(entry.EndpointTemplate) ? entry.EndpointTemplate : endpointOverride;
        Uri.TryCreate(urlStr, UriKind.Absolute, out var url);

        var oauthConfig = new McpOAuthConfig
        {
            ClientId = clientId,
            AuthorizationUrl = entry.OAuthAuthorizationUrl,
            TokenUrl = entry.OAuthTokenUrl,
            Scopes = (IReadOnlyList<string>?)entry.OAuthScopes ?? [],
        };

        return new McpServerConfig { Url = url, OAuthConfig = oauthConfig };
    }

    private static McpServerConfig BuildConfig(CatalogEntryViewModel entry, string apiKey, string endpoint)
    {
        if (entry.Kind == IntegrationKind.McpStdio)
        {
            var env = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(entry.ApiKeyEnvVar) && !string.IsNullOrEmpty(apiKey))
                env[entry.ApiKeyEnvVar] = apiKey;

            var args = (entry.DefaultArgs ?? (IReadOnlyList<string>)[])
                .Select(a => a
                    .Replace("{ENDPOINT}", endpoint, StringComparison.Ordinal)
                    .Replace("{API_KEY}", apiKey, StringComparison.Ordinal))
                .ToArray();

            return new McpServerConfig { Command = entry.DefaultCommand ?? string.Empty, Args = args, Env = env };
        }
        else
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(entry.ApiKeyHeader) && !string.IsNullOrEmpty(apiKey))
                headers[entry.ApiKeyHeader] = entry.ApiKeyHeader == "Authorization"
                    ? $"Bearer {apiKey}"
                    : apiKey;

            var urlStr = !string.IsNullOrEmpty(entry.EndpointTemplate)
                ? entry.EndpointTemplate.Replace("{API_KEY}", apiKey, StringComparison.Ordinal)
                : endpoint;

            Uri.TryCreate(urlStr, UriKind.Absolute, out var url);
            return new McpServerConfig { Url = url, Headers = headers };
        }
    }

    // ── Connected tab commands ────────────────────────────────────────────────
    private async Task ConnectAndRefreshAsync(string name, McpServerConfig config)
    {
        StatusMessage = $"Connecting to '{name}'…";
        try
        {
            await _registrar.ReconnectServerAsync(name, config).ConfigureAwait(true);
            var toolCount = _clientRegistry.ToolToServer.Count(kvp => kvp.Value == name);
            StatusMessage = $"Connected to '{name}' ({toolCount} tool{(toolCount == 1 ? "" : "s")}).";
            await LoadServersAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to connect to '{name}': {ex.Message}";
            await LoadServersAsync().ConfigureAwait(true);
        }
        if (_activeContext is not null)
            await _activeContext.RefreshMcpServersAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private Task Refresh() => LoadServersAsync();

    [RelayCommand]
    private void SelectServer(McpServerItem server) => SelectedServer = server;

    [RelayCommand]
    private Task AddServer() => AddMode switch
    {
        "http" => AddHttpServerAsync(),
        "json" => AddFromJsonAsync(),
        _ => AddStdioServerAsync(),
    };

    private async Task AddStdioServerAsync()
    {
        var name = NewServerName.Trim();
        var command = NewServerCommand.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(command))
        {
            StatusMessage = "Name and command are required.";
            return;
        }
        if (await _serverStore.GetAsync(name).ConfigureAwait(true) is not null)
        {
            StatusMessage = $"Server '{name}' already exists.";
            return;
        }
        try
        {
            var args = string.IsNullOrWhiteSpace(NewServerArgs)
                ? []
                : NewServerArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var config = new McpServerConfig { Command = command, Args = args, Env = new Dictionary<string, string>(StringComparer.Ordinal) };
            await _serverStore.UpsertAsync(name, config).ConfigureAwait(true);
            ResetAddForm();
            await ConnectAndRefreshAsync(name, config).ConfigureAwait(true);
        }
        catch (Exception ex) { StatusMessage = $"Failed to add server: {ex.Message}"; }
    }

    private async Task AddHttpServerAsync()
    {
        var name = NewServerName.Trim();
        var urlText = NewServerUrl.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(urlText))
        {
            StatusMessage = "Name and URL are required.";
            return;
        }
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var url) || (url.Scheme != "http" && url.Scheme != "https"))
        {
            StatusMessage = "URL must be an absolute http(s) URL.";
            return;
        }
        if (await _serverStore.GetAsync(name).ConfigureAwait(true) is not null)
        {
            StatusMessage = $"Server '{name}' already exists.";
            return;
        }
        try
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(NewServerHeadersJson))
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(NewServerHeadersJson);
                if (parsed is not null)
                    foreach (var kv in parsed) headers[kv.Key] = kv.Value;
            }
            if (!string.IsNullOrWhiteSpace(NewServerBearer))
                headers["Authorization"] = $"Bearer {NewServerBearer.Trim()}";

            var config = new McpServerConfig { Url = url, Headers = headers };
            await _serverStore.UpsertAsync(name, config).ConfigureAwait(true);
            ResetAddForm();
            await ConnectAndRefreshAsync(name, config).ConfigureAwait(true);
        }
        catch (Exception ex) { StatusMessage = $"Failed to add server: {ex.Message}"; }
    }

    private async Task AddFromJsonAsync()
    {
        if (string.IsNullOrWhiteSpace(NewServerJson)) { StatusMessage = "Paste a JSON config first."; return; }
        try
        {
            var entries = McpJsonParser.Parse(NewServerJson);
            if (entries.Count == 0) { StatusMessage = "No valid mcpServers entries found."; return; }
            foreach (var (name, config) in entries)
                await _serverStore.UpsertAsync(name, config).ConfigureAwait(true);
            ResetAddForm();
            foreach (var (name, config) in entries)
                await ConnectAndRefreshAsync(name, config).ConfigureAwait(true);
        }
        catch (Exception ex) { StatusMessage = $"Failed to add server: {ex.Message}"; }
    }

    private void ResetAddForm()
    {
        NewServerName = NewServerCommand = NewServerArgs = NewServerUrl = NewServerBearer = NewServerHeadersJson = NewServerJson = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveServerAsync(McpServerItem server)
    {
        try
        {
            await _serverStore.DeleteAsync(server.Name).ConfigureAwait(true);
            _allServers.RemoveAll(s => s.Name == server.Name);
            TotalCount = _allServers.Count;
            if (SelectedServer == server) SelectedServer = null;
            ApplyFilter();
            StatusMessage = $"Server '{server.Name}' removed. Restart to disconnect.";
        }
        catch (Exception ex) { StatusMessage = $"Failed to remove server: {ex.Message}"; }
        if (_activeContext is not null)
            await _activeContext.RefreshMcpServersAsync().ConfigureAwait(true);
    }

    private async Task LoadServersAsync()
    {
        _allServers.Clear();
        var servers = await _serverStore.GetAllAsync().ConfigureAwait(true);

        var toolsByServer = _clientRegistry.ToolToServer
            .GroupBy(kv => kv.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);

        foreach (var (name, server) in servers.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            var isConnected = _clientRegistry.Clients.ContainsKey(name);
            var tools = toolsByServer.TryGetValue(name, out var t) ? t : [];
            var item = new McpServerItem
            {
                Name = name,
                Command = server.Command,
                ArgsSummary = server.Args.Count > 0 ? string.Join(" ", server.Args) : "",
                Url = server.Url?.ToString() ?? string.Empty,
                Transport = server.Url is not null ? "http" : "stdio",
                HasOAuth = server.OAuthConfig is not null,
                IsConnected = isConnected,
                ToolCount = tools.Count,
                EnvVars = server.Env.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
                Headers = server.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
            };
            foreach (var toolName in tools) item.ToolNames.Add(toolName);
            item.Markdown = BuildServerMarkdown(item);
            _allServers.Add(item);
        }

        TotalCount = _allServers.Count;
        ApplyFilter();

        // Refresh "already connected" state on all entries
        foreach (var entry in CatalogEntries)
            entry.IsAlreadyConnected = _clientRegistry.Clients.ContainsKey(entry.Id);

        // For grouped display entries, show connected if any variant is connected
        foreach (var displayEntry in GalleryDisplayEntries)
        {
            if (displayEntry.GroupName is not null)
            {
                displayEntry.IsAlreadyConnected = CatalogEntries
                    .Where(e => e.GroupName == displayEntry.GroupName)
                    .Any(e => _clientRegistry.Clients.ContainsKey(e.Id));
            }
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredServers.Clear();
        var query = SearchText.Trim();
        foreach (var server in _allServers)
        {
            if (query.Length > 0
                && !server.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !server.Command.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
            FilteredServers.Add(server);
        }
    }

    partial void OnSelectedServerChanged(McpServerItem? value)
    {
        if (value is not null)
            _ = LoadTrustRulesAsync(value);
    }

    private async Task LoadTrustRulesAsync(McpServerItem server)
    {
        if (_workspaceId is null) return;
        var rules = await _trustRuleStore.GetRulesAsync(_workspaceId, server.Name).ConfigureAwait(true);
        server.TrustRules.Clear();
        foreach (var r in rules)
        {
            var ruleId = r.RuleId;
            server.TrustRules.Add(new TrustRuleViewModel
            {
                RuleId = ruleId,
                ToolPattern = r.ToolPattern,
                Action = r.Action.ToString(),
                Reason = r.Reason,
                RemoveCommand = new RelayCommand(() => _ = RemoveTrustRuleByIdAsync(ruleId, server)),
            });
        }
    }

    private async Task RemoveTrustRuleByIdAsync(string ruleId, McpServerItem server)
    {
        if (_workspaceId is null) return;
        await _trustRuleStore.DeleteAsync(ruleId, _workspaceId).ConfigureAwait(true);
        StatusMessage = "Rule removed.";
        await LoadTrustRulesAsync(server).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddTrustRule()
    {
        var server = SelectedServer;
        if (server is null || string.IsNullOrWhiteSpace(NewRulePattern) || _workspaceId is null)
        {
            StatusMessage = "Tool pattern is required.";
            return;
        }
        if (!Enum.TryParse<McpTrustAction>(NewRuleAction, out var action))
        {
            StatusMessage = "Invalid action.";
            return;
        }
        var now = DateTimeOffset.UtcNow;
        var rule = new McpTrustRule(
            RuleId: Guid.NewGuid().ToString("N"),
            WorkspaceId: _workspaceId,
            ServerName: server.Name,
            ToolPattern: NewRulePattern.Trim(),
            Action: action,
            Reason: string.IsNullOrWhiteSpace(NewRuleReason) ? null : NewRuleReason.Trim(),
            CreatedAt: now,
            UpdatedAt: now);
        await _trustRuleStore.UpsertAsync(rule).ConfigureAwait(true);
        NewRulePattern = string.Empty;
        NewRuleReason = string.Empty;
        StatusMessage = "Rule added.";
        await LoadTrustRulesAsync(server).ConfigureAwait(true);
    }

    private static string BuildServerMarkdown(McpServerItem server)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {server.Name}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Status:** {(server.IsConnected ? "Connected" : "Not Connected")}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Transport:** {server.Transport}");
        sb.AppendLine();

        if (server.Transport == "http")
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**URL:** {server.Url}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Command:** {server.Command}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(server.ArgsSummary))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"**Args:** {server.ArgsSummary}");
                sb.AppendLine();
            }
        }

        if (server.HasOAuth) { sb.AppendLine("**Authentication:** OAuth 2.0 configured"); sb.AppendLine(); }

        if (server.Headers.Count > 0)
        {
            sb.AppendLine("## Headers"); sb.AppendLine();
            sb.AppendLine("| Header | Value |"); sb.AppendLine("|--------|-------|");
            foreach (var (key, value) in server.Headers.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {key} | {(IsSensitiveKey(key) ? Mask(value) : value)} |");
            sb.AppendLine();
        }

        if (server.EnvVars.Count > 0)
        {
            sb.AppendLine("## Environment Variables"); sb.AppendLine();
            sb.AppendLine("| Variable | Value |"); sb.AppendLine("|----------|-------|");
            foreach (var (key, value) in server.EnvVars.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {key} | {(IsSensitiveKey(key) ? "--------" : value)} |");
            sb.AppendLine();
        }

        sb.AppendLine("---"); sb.AppendLine();
        if (server.ToolNames.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Tools ({server.ToolCount}):** {string.Join(", ", server.ToolNames)}");
        else if (server.IsConnected)
            sb.AppendLine("**Tools:** Connected — server reported no tools.");
        else
            sb.AppendLine("**Tools:** Server not connected");

        return sb.ToString();
    }

    private static bool IsSensitiveKey(string key) =>
        key.Contains("KEY", StringComparison.OrdinalIgnoreCase)
        || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
        || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
        || key.Contains("AUTH", StringComparison.OrdinalIgnoreCase);

    private static string Mask(string value) =>
        value.Length > 6 ? string.Concat(value.AsSpan(0, 4), "****") : "****";
}

public partial class CatalogEntryViewModel : ViewModelBase
{
    private readonly CatalogEntry _entry;

    [ObservableProperty]
    private bool _isAlreadyConnected;

    public CatalogEntryViewModel(CatalogEntry entry) => _entry = entry;

    public string Id => _entry.Id;
    public string Name => _entry.Name;
    public string Category => _entry.Category;
    public string Icon => _entry.Icon;
    public string Description => _entry.Description;
    public IntegrationKind Kind => _entry.Kind;
    public IntegrationTier Tier => _entry.Tier;
    public bool NeedsApiKey => _entry.NeedsApiKey;
    public bool NeedsEndpoint => _entry.NeedsEndpoint;
    public bool RequiresOAuth => _entry.RequiresOAuth;
    public string? ApiKeyLabel => _entry.ApiKeyLabel;
    public string? ApiKeyEnvVar => _entry.ApiKeyEnvVar;
    public string? ApiKeyHeader => _entry.ApiKeyHeader;
    public string? EndpointLabel => _entry.EndpointLabel;
    public string? DefaultCommand => _entry.DefaultCommand;
    public IReadOnlyList<string>? DefaultArgs => _entry.DefaultArgs;
    public string? EndpointTemplate => _entry.EndpointTemplate;
    public string? SettingsSection => _entry.SettingsSection;
    public string? GroupName => _entry.GroupName;
    public string? TabLabel => _entry.TabLabel;
    public bool IsLlmProvider => _entry.Kind == IntegrationKind.LlmProvider;
    public Uri? OAuthAuthorizationUrl => _entry.OAuthAuthorizationUrl;
    public Uri? OAuthTokenUrl => _entry.OAuthTokenUrl;
    public IReadOnlyList<string>? OAuthScopes => _entry.OAuthScopes;
    public bool HasOAuthMetadata => _entry.HasOAuthMetadata;
}

public partial class McpServerItem : ViewModelBase
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _command = string.Empty;
    [ObservableProperty] private string _argsSummary = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _transport = "stdio";
    [ObservableProperty] private bool _hasOAuth;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private int _toolCount;

    public ObservableCollection<string> ToolNames { get; } = [];
    public ObservableCollection<TrustRuleViewModel> TrustRules { get; } = [];
    public Dictionary<string, string> EnvVars { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.Ordinal);
    public string Markdown { get; set; } = string.Empty;
}

public partial class TrustRuleViewModel : ViewModelBase
{
    public string RuleId { get; init; } = string.Empty;
    public string ToolPattern { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public IRelayCommand? RemoveCommand { get; init; }

    public string ActionBadgeColor => Action switch
    {
        "Block" => "#cc3333",
        "Allow" => "#16a34a",
        _ => "#d97706",
    };
}
