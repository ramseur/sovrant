using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Auth;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Desktop.ViewModels;

public enum SupabaseConnectionStatus { NotConfigured, Testing, Connected, Error }
public enum SupabaseSchemaStatus { Unknown, NotInitialized, UpToDate }

public partial class SystemIntegrationsViewModel : ViewModelBase
{
    private readonly ICredentialStore _credentials;

    [ObservableProperty] private string _projectUrl = string.Empty;
    [ObservableProperty] private string _serviceRoleKey = string.Empty;
    [ObservableProperty] private SupabaseConnectionStatus _connectionStatus = SupabaseConnectionStatus.NotConfigured;
    [ObservableProperty] private SupabaseSchemaStatus _schemaStatus = SupabaseSchemaStatus.Unknown;
    [ObservableProperty] private bool _isSupabaseActive;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;

    public bool IsConnected => ConnectionStatus == SupabaseConnectionStatus.Connected;
    public bool IsSchemaReady => SchemaStatus == SupabaseSchemaStatus.UpToDate;
    public bool IsMigrationVisible => IsSchemaReady && !IsSupabaseActive;
    public bool IsSwitchVisible => IsSchemaReady;
    public string ConnectionStatusLabel => ConnectionStatus switch
    {
        SupabaseConnectionStatus.NotConfigured => "Not configured",
        SupabaseConnectionStatus.Testing       => "Testing…",
        SupabaseConnectionStatus.Connected     => "Connected",
        SupabaseConnectionStatus.Error         => "Connection error",
        _                                      => string.Empty,
    };
    public string ActiveBackendLabel => IsSupabaseActive ? "Supabase (PostgreSQL)" : "SQLite (local)";

    public SystemIntegrationsViewModel(ICredentialStore credentials)
    {
        _credentials = credentials;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        ProjectUrl = await _credentials.RetrieveAsync(CredentialKeys.SupabaseProjectUrl).ConfigureAwait(true) ?? string.Empty;
        ServiceRoleKey = await _credentials.RetrieveAsync(CredentialKeys.SupabaseServiceRoleKey).ConfigureAwait(true) ?? string.Empty;
        var backend = await _credentials.RetrieveAsync(CredentialKeys.DatabaseBackend).ConfigureAwait(true) ?? "sqlite";
        IsSupabaseActive = backend == "supabase";

        ConnectionStatus = string.IsNullOrEmpty(ProjectUrl)
            ? SupabaseConnectionStatus.NotConfigured
            : SupabaseConnectionStatus.Connected;

        RefreshDerived();
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectUrl) || string.IsNullOrWhiteSpace(ServiceRoleKey))
        {
            StatusMessage = "Enter a Project URL and Service Role Key first.";
            return;
        }

        ConnectionStatus = SupabaseConnectionStatus.Testing;
        StatusMessage = "Testing connection…";
        RefreshDerived();

        try
        {
            var url = ProjectUrl.TrimEnd('/') + "/rest/v1/";
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("apikey", ServiceRoleKey);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ServiceRoleKey}");

            var response = await client.GetAsync(new Uri(url)).ConfigureAwait(true);

            // Supabase returns 200 (with empty JSON object) when credentials are valid.
            // A 401 means the key is wrong. Any other status still indicates reachability.
            if ((int)response.StatusCode != 401)
            {
                ConnectionStatus = SupabaseConnectionStatus.Connected;
                StatusMessage = "Connection successful.";
                await _credentials.StoreAsync(CredentialKeys.SupabaseProjectUrl, ProjectUrl).ConfigureAwait(true);
                await _credentials.StoreAsync(CredentialKeys.SupabaseServiceRoleKey, ServiceRoleKey).ConfigureAwait(true);
            }
            else
            {
                ConnectionStatus = SupabaseConnectionStatus.Error;
                StatusMessage = "Connection failed: invalid key (HTTP 401).";
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = SupabaseConnectionStatus.Error;
            StatusMessage = $"Connection failed: {ex.Message}";
        }

        RefreshDerived();
    }

    [RelayCommand]
    private async Task InitializeSchemaAsync()
    {
        // Implemented in Step B — PostgreSQL schema initialization.
        StatusMessage = "Schema initialization coming soon (Step B).";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task MigrateDataAsync()
    {
        // Implemented in Step D — SQLite → Postgres migration tooling.
        StatusMessage = "Data migration coming soon (Step D).";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SwitchBackendAsync()
    {
        // Implemented in Step E — boot-time DI switch.
        StatusMessage = "Backend switching coming soon (Step E).";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RevertToSqliteAsync()
    {
        // Implemented in Step E — boot-time DI switch.
        StatusMessage = "Backend switching coming soon (Step E).";
        await Task.CompletedTask;
    }

    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsSchemaReady));
        OnPropertyChanged(nameof(IsMigrationVisible));
        OnPropertyChanged(nameof(IsSwitchVisible));
        OnPropertyChanged(nameof(ConnectionStatusLabel));
        OnPropertyChanged(nameof(ActiveBackendLabel));
    }
}
