using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Storage.Postgres;
using RuntimeSCE = Sovrant.Runtime.ServiceCollectionExtensions;
using PostgresSCE = Sovrant.Storage.Postgres.ServiceCollectionExtensions;

namespace Sovrant.Desktop.ViewModels;

public enum SupabaseConnectionStatus { NotConfigured, Testing, Connected, Error }
public enum SupabaseSchemaStatus { Unknown, NotInitialized, UpToDate }

public partial class SystemIntegrationsViewModel : ViewModelBase
{
    private readonly ICredentialStore _bootstrapStore;
    private readonly BootstrapConfig _bootstrap;

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

    public SystemIntegrationsViewModel(BootstrapConfig bootstrap)
    {
        _bootstrap      = bootstrap;
        _bootstrapStore = RuntimeSCE.CreateBootstrapCredentialStore(bootstrap);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        ProjectUrl     = await _bootstrapStore.RetrieveAsync(CredentialKeys.SupabaseProjectUrl).ConfigureAwait(true) ?? string.Empty;
        ServiceRoleKey = await _bootstrapStore.RetrieveAsync(CredentialKeys.SupabaseServiceRoleKey).ConfigureAwait(true) ?? string.Empty;
        var backend    = await _bootstrapStore.RetrieveAsync(CredentialKeys.DatabaseBackend).ConfigureAwait(true) ?? "sqlite";
        IsSupabaseActive = backend == "supabase";

        if (!string.IsNullOrEmpty(ProjectUrl) && !string.IsNullOrEmpty(ServiceRoleKey))
        {
            ConnectionStatus = SupabaseConnectionStatus.Connected;
            await CheckSchemaVersionAsync().ConfigureAwait(true);
        }
        else
        {
            ConnectionStatus = SupabaseConnectionStatus.NotConfigured;
        }

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

            if ((int)response.StatusCode != 401)
            {
                ConnectionStatus = SupabaseConnectionStatus.Connected;
                await _bootstrapStore.StoreAsync(CredentialKeys.SupabaseProjectUrl, ProjectUrl).ConfigureAwait(true);
                await _bootstrapStore.StoreAsync(CredentialKeys.SupabaseServiceRoleKey, ServiceRoleKey).ConfigureAwait(true);
                await CheckSchemaVersionAsync().ConfigureAwait(true);
                StatusMessage = "Connection successful.";
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
        if (!IsConnected) return;
        IsLoading = true;
        StatusMessage = "Initializing schema…";
        try
        {
            var connStr     = PostgresSCE.BuildSupabaseConnectionString(ProjectUrl, ServiceRoleKey);
            var initializer = PostgresSCE.CreateSchemaInitializer(connStr);
            await initializer.InitializeAsync().ConfigureAwait(true);
            await CheckSchemaVersionAsync().ConfigureAwait(true);
            StatusMessage = "Schema initialized successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Schema initialization failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            RefreshDerived();
        }
    }

    [RelayCommand]
    private async Task MigrateDataAsync()
    {
        if (!IsSchemaReady) return;
        IsLoading = true;
        StatusMessage = "Starting migration…";
        try
        {
            var home       = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var sqlitePath = _bootstrap.DbPath ?? Path.Combine(home, ".sovrant", "data", "sovrant.db");

            if (!File.Exists(sqlitePath))
            {
                StatusMessage = $"SQLite database not found at {sqlitePath}.";
                return;
            }

            var connStr  = PostgresSCE.BuildSupabaseConnectionString(ProjectUrl, ServiceRoleKey);
            var factory  = PostgresSCE.CreateConnectionFactory(connStr);
            var migrator = new SqliteToPostgresMigrator(factory, sqlitePath);

            var prog = new Progress<MigrationProgress>(p =>
                StatusMessage = string.IsNullOrEmpty(p.Message)
                    ? $"{p.Stage}: {p.Done}/{p.Total}"
                    : p.Message);

            var result = await migrator.MigrateAsync(prog).ConfigureAwait(true);
            StatusMessage = $"Migration complete — {result.SessionsCopied} sessions, " +
                            $"{result.EntriesCopied} entries, {result.CredentialsCopied} credentials copied.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Migration failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SwitchBackendAsync()
    {
        if (!IsSchemaReady) return;
        await _bootstrapStore.StoreAsync(CredentialKeys.DatabaseBackend, "supabase").ConfigureAwait(true);
        IsSupabaseActive = true;
        StatusMessage = "Switched to Supabase. Restart Sovrant for the change to take effect.";
        RefreshDerived();
    }

    [RelayCommand]
    private async Task RevertToSqliteAsync()
    {
        await _bootstrapStore.StoreAsync(CredentialKeys.DatabaseBackend, "sqlite").ConfigureAwait(true);
        IsSupabaseActive = false;
        StatusMessage = "Reverted to SQLite. Restart Sovrant for the change to take effect.";
        RefreshDerived();
    }

    private async Task CheckSchemaVersionAsync()
    {
        try
        {
            var connStr     = PostgresSCE.BuildSupabaseConnectionString(ProjectUrl, ServiceRoleKey);
            var initializer = PostgresSCE.CreateSchemaInitializer(connStr);
            var version     = await initializer.GetSchemaVersionAsync().ConfigureAwait(true);
            SchemaStatus    = version.HasValue ? SupabaseSchemaStatus.UpToDate : SupabaseSchemaStatus.NotInitialized;
        }
        catch
        {
            SchemaStatus = SupabaseSchemaStatus.NotInitialized;
        }
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
