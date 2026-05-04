using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Desktop.ViewModels;

public partial class IntegrationsViewModel : ViewModelBase
{
    private readonly IMcpServerStore _serverStore;
    private readonly McpClientRegistry _clientRegistry;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private McpServerItem? _selectedServer;

    [ObservableProperty]
    private string _detailMarkdown = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    // Add-server form fields
    [ObservableProperty]
    private string _newServerName = string.Empty;

    [ObservableProperty]
    private string _newServerCommand = string.Empty;

    [ObservableProperty]
    private string _newServerArgs = string.Empty;

    // HTTP transport form fields
    [ObservableProperty]
    private string _newServerUrl = string.Empty;

    [ObservableProperty]
    private string _newServerBearer = string.Empty;

    [ObservableProperty]
    private string _newServerHeadersJson = string.Empty;

    // JSON-paste mode field
    [ObservableProperty]
    private string _newServerJson = string.Empty;

    /// <summary>"json" | "stdio" | "http"</summary>
    [ObservableProperty]
    private string _addMode = "json";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private readonly List<McpServerItem> _allServers = [];

    public ObservableCollection<McpServerItem> FilteredServers { get; } = [];

    public IntegrationsViewModel(IMcpServerStore serverStore, McpClientRegistry clientRegistry)
    {
        _serverStore = serverStore;
        _clientRegistry = clientRegistry;
        _ = LoadServersAsync();
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

            await _serverStore.UpsertAsync(name, new McpServerConfig
            {
                Command = command,
                Args = args,
                Env = new Dictionary<string, string>(StringComparer.Ordinal),
            }).ConfigureAwait(true);

            StatusMessage = $"Server '{name}' added. Restart to connect.";
            ResetAddForm();

            _allServers.Add(new McpServerItem
            {
                Name = name,
                Command = command,
                ArgsSummary = args.Length > 0 ? string.Join(" ", args) : "",
                IsConnected = false,
                ToolCount = 0,
            });
            TotalCount = _allServers.Count;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add server: {ex.Message}";
        }
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
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var url)
            || (url.Scheme != "http" && url.Scheme != "https"))
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

            await _serverStore.UpsertAsync(name, new McpServerConfig { Url = url, Headers = headers })
                .ConfigureAwait(true);

            StatusMessage = $"Server '{name}' added. Restart to connect.";
            ResetAddForm();
            await LoadServersAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add server: {ex.Message}";
        }
    }

    private async Task AddFromJsonAsync()
    {
        if (string.IsNullOrWhiteSpace(NewServerJson))
        {
            StatusMessage = "Paste a JSON config first.";
            return;
        }
        try
        {
            var entries = McpJsonParser.Parse(NewServerJson);
            if (entries.Count == 0)
            {
                StatusMessage = "No valid mcpServers entries found.";
                return;
            }
            foreach (var (name, config) in entries)
                await _serverStore.UpsertAsync(name, config).ConfigureAwait(true);

            StatusMessage = $"Added {entries.Count} server(s): {string.Join(", ", entries.Keys)}. Restart to connect.";
            ResetAddForm();
            await LoadServersAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add server: {ex.Message}";
        }
    }

    private void ResetAddForm()
    {
        NewServerName = string.Empty;
        NewServerCommand = string.Empty;
        NewServerArgs = string.Empty;
        NewServerUrl = string.Empty;
        NewServerBearer = string.Empty;
        NewServerHeadersJson = string.Empty;
        NewServerJson = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveServerAsync(McpServerItem server)
    {
        try
        {
            await _serverStore.DeleteAsync(server.Name).ConfigureAwait(true);

            _allServers.RemoveAll(s => s.Name == server.Name);
            TotalCount = _allServers.Count;

            if (SelectedServer == server)
                SelectedServer = null;

            ApplyFilter();
            StatusMessage = $"Server '{server.Name}' removed. Restart to disconnect.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to remove server: {ex.Message}";
        }
    }

    private async Task LoadServersAsync()
    {
        _allServers.Clear();

        var servers = await _serverStore.GetAllAsync().ConfigureAwait(true);
        foreach (var (name, server) in servers.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            var isConnected = _clientRegistry.Clients.ContainsKey(name);

            if (isConnected && _clientRegistry.Clients.TryGetValue(name, out var client))
            {
                // List tools asynchronously
                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        var tools = await client.ListToolsAsync();
                        var item = _allServers.FirstOrDefault(s => s.Name == name);
                        if (item is not null)
                        {
                            item.ToolCount = tools.Count;
                            item.ToolNames.Clear();
                            foreach (var t in tools.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                                item.ToolNames.Add(t);
                            item.Markdown = BuildServerMarkdown(item);
                            // Refresh detail if this server is selected
                            if (SelectedServer == item)
                                DetailMarkdown = BuildServerMarkdown(item);
                        }
                    }
                    catch
                    {
                        // Tool listing failed — leave count at 0
                    }
                });
            }

            var serverItem = new McpServerItem
            {
                Name = name,
                Command = server.Command,
                ArgsSummary = server.Args.Count > 0 ? string.Join(" ", server.Args) : "",
                Url = server.Url?.ToString() ?? string.Empty,
                Transport = server.Url is not null ? "http" : "stdio",
                HasOAuth = server.OAuthConfig is not null,
                IsConnected = isConnected,
                ToolCount = 0,
                EnvVars = server.Env.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
                Headers = server.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
            };
            serverItem.Markdown = BuildServerMarkdown(serverItem);
            _allServers.Add(serverItem);
        }

        TotalCount = _allServers.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedServerChanged(McpServerItem? value)
    {
        DetailMarkdown = value is null ? string.Empty : BuildServerMarkdown(value);
    }

    private void ApplyFilter()
    {
        FilteredServers.Clear();
        var query = SearchText.Trim();

        foreach (var server in _allServers)
        {
            if (query.Length > 0
                && !server.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !server.Command.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredServers.Add(server);
        }
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

        if (server.HasOAuth)
        {
            sb.AppendLine("**Authentication:** OAuth 2.0 configured");
            sb.AppendLine();
        }

        if (server.Headers.Count > 0)
        {
            sb.AppendLine("## Headers");
            sb.AppendLine();
            sb.AppendLine("| Header | Value |");
            sb.AppendLine("|--------|-------|");
            foreach (var (key, value) in server.Headers.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                var display = IsSensitiveKey(key) ? Mask(value) : value;
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {key} | {display} |");
            }
            sb.AppendLine();
        }

        if (server.EnvVars.Count > 0)
        {
            sb.AppendLine("## Environment Variables");
            sb.AppendLine();
            sb.AppendLine("| Variable | Value |");
            sb.AppendLine("|----------|-------|");
            foreach (var (key, value) in server.EnvVars.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                var display = IsSensitiveKey(key) ? "--------" : value;
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {key} | {display} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();

        if (server.ToolNames.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Tools ({server.ToolCount}):** {string.Join(", ", server.ToolNames)}");
        }
        else if (server.IsConnected)
        {
            sb.AppendLine("**Tools:** Loading...");
        }
        else
        {
            sb.AppendLine("**Tools:** Server not connected");
        }

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

public partial class McpServerItem : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _command = string.Empty;

    [ObservableProperty]
    private string _argsSummary = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _transport = "stdio";

    [ObservableProperty]
    private bool _hasOAuth;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _toolCount;

    public ObservableCollection<string> ToolNames { get; } = [];
    public Dictionary<string, string> EnvVars { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.Ordinal);
    public string Markdown { get; set; } = string.Empty;
}
