using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Storage.Postgres;
using RuntimeSCE = Sovrant.Runtime.ServiceCollectionExtensions;
using PostgresSCE = Sovrant.Storage.Postgres.ServiceCollectionExtensions;

namespace Sovrant.Desktop.ViewModels;

public enum PostgresConnectionStatus { NotConfigured, Testing, Connected, Error }
public enum SupabaseConnectionStatus { NotConfigured, Testing, Connected, Error }
public enum PostgresSchemaStatus     { Unknown, NotInitialized, UpToDate }
public enum SupabaseSchemaStatus     { Unknown, NotInitialized, UpToDate }

public partial class SystemIntegrationsViewModel : ViewModelBase
{
    private readonly ICredentialStore _bootstrapStore;
    private readonly BootstrapConfig _bootstrap;

    // ── Postgres properties ─────────────────────────────────────────────────
    [ObservableProperty] private string _pgConnectionUrl = string.Empty;
    [ObservableProperty] private PostgresConnectionStatus _pgConnectionStatus = PostgresConnectionStatus.NotConfigured;
    [ObservableProperty] private PostgresSchemaStatus _pgSchemaStatus = PostgresSchemaStatus.Unknown;
    [ObservableProperty] private bool _isPostgresActive;

    // ── Supabase properties ─────────────────────────────────────────────────
    [ObservableProperty] private string _projectUrl = string.Empty;
    [ObservableProperty] private string _serviceRoleKey = string.Empty;
    [ObservableProperty] private SupabaseConnectionStatus _connectionStatus = SupabaseConnectionStatus.NotConfigured;
    [ObservableProperty] private SupabaseSchemaStatus _schemaStatus = SupabaseSchemaStatus.Unknown;
    [ObservableProperty] private bool _isSupabaseActive;

    // ── Shared ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;

    // ── Postgres computed ───────────────────────────────────────────────────
    public bool IsPgConnected        => PgConnectionStatus == PostgresConnectionStatus.Connected;
    public bool IsPgSchemaReady      => PgSchemaStatus == PostgresSchemaStatus.UpToDate;
    public bool IsPgMigrationVisible => IsPgSchemaReady && !IsPostgresActive;
    public bool IsPgSwitchVisible    => IsPgSchemaReady;
    public string PgConnectionStatusLabel => PgConnectionStatus switch
    {
        PostgresConnectionStatus.NotConfigured => "Not configured",
        PostgresConnectionStatus.Testing       => "Testing…",
        PostgresConnectionStatus.Connected     => "Connected",
        PostgresConnectionStatus.Error         => "Connection error",
        _                                      => string.Empty,
    };

    // ── Supabase computed ───────────────────────────────────────────────────
    public bool IsConnected        => ConnectionStatus == SupabaseConnectionStatus.Connected;
    public bool IsSchemaReady      => SchemaStatus == SupabaseSchemaStatus.UpToDate;
    public bool IsMigrationVisible => IsSchemaReady && !IsSupabaseActive;
    public bool IsSwitchVisible    => IsSchemaReady;
    public string ConnectionStatusLabel => ConnectionStatus switch
    {
        SupabaseConnectionStatus.NotConfigured => "Not configured",
        SupabaseConnectionStatus.Testing       => "Testing…",
        SupabaseConnectionStatus.Connected     => "Connected",
        SupabaseConnectionStatus.Error         => "Connection error",
        _                                      => string.Empty,
    };

    // ── Shared computed ─────────────────────────────────────────────────────
    public string ActiveBackendLabel => IsSupabaseActive ? "Supabase (PostgreSQL)"
        : IsPostgresActive ? "PostgreSQL (standalone)"
        : "SQLite (local)";

    public SystemIntegrationsViewModel(BootstrapConfig bootstrap)
    {
        _bootstrap      = bootstrap;
        _bootstrapStore = RuntimeSCE.CreateBootstrapCredentialStore(bootstrap);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        PgConnectionUrl = await _bootstrapStore.RetrieveAsync(CredentialKeys.PostgresConnectionUrl).ConfigureAwait(true) ?? string.Empty;
        ProjectUrl      = await _bootstrapStore.RetrieveAsync(CredentialKeys.SupabaseProjectUrl).ConfigureAwait(true) ?? string.Empty;
        ServiceRoleKey  = await _bootstrapStore.RetrieveAsync(CredentialKeys.SupabaseServiceRoleKey).ConfigureAwait(true) ?? string.Empty;
        var backend     = await _bootstrapStore.RetrieveAsync(CredentialKeys.DatabaseBackend).ConfigureAwait(true) ?? "sqlite";
        IsSupabaseActive = backend == "supabase";
        IsPostgresActive = backend == "postgres";

        if (!string.IsNullOrEmpty(PgConnectionUrl))
        {
            PgConnectionStatus = PostgresConnectionStatus.Connected;
            await CheckPgSchemaVersionAsync().ConfigureAwait(true);
        }
        else
        {
            PgConnectionStatus = PostgresConnectionStatus.NotConfigured;
        }

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

    // ── PostgreSQL commands ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task TestPgConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(PgConnectionUrl))
        {
            StatusMessage = "Enter a connection string first.";
            return;
        }

        PgConnectionStatus = PostgresConnectionStatus.Testing;
        StatusMessage = "Testing connection…";
        RefreshDerived();

        try
        {
            var ok = await PostgresSCE.CanConnectAsync(PgConnectionUrl).ConfigureAwait(true);
            if (ok)
            {
                PgConnectionStatus = PostgresConnectionStatus.Connected;
                await _bootstrapStore.StoreAsync(CredentialKeys.PostgresConnectionUrl, PgConnectionUrl).ConfigureAwait(true);
                await CheckPgSchemaVersionAsync().ConfigureAwait(true);
                StatusMessage = "Connection successful.";
            }
            else
            {
                PgConnectionStatus = PostgresConnectionStatus.Error;
                StatusMessage = "Connection failed. Check the connection string and ensure PostgreSQL is running.";
            }
        }
        catch (Exception ex)
        {
            PgConnectionStatus = PostgresConnectionStatus.Error;
            StatusMessage = $"Connection failed: {ex.Message}";
        }

        RefreshDerived();
    }

    [RelayCommand]
    private async Task InitializePgSchemaAsync()
    {
        if (!IsPgConnected) return;
        IsLoading = true;
        StatusMessage = "Initializing schema…";
        try
        {
            await PostgresSCE.InitializeBaseSchemaAsync(PgConnectionUrl).ConfigureAwait(true);
            await CheckPgSchemaVersionAsync().ConfigureAwait(true);
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
    private async Task MigratePgDataAsync()
    {
        if (!IsPgSchemaReady) return;
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

            var factory  = PostgresSCE.CreateConnectionFactory(PgConnectionUrl);
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
    private async Task SwitchToPostgresAsync()
    {
        if (!IsPgSchemaReady) return;
        await _bootstrapStore.StoreAsync(CredentialKeys.DatabaseBackend, "postgres").ConfigureAwait(true);
        IsPostgresActive = true;
        IsSupabaseActive = false;
        StatusMessage = "Switched to PostgreSQL. Restart Sovrant for the change to take effect.";
        RefreshDerived();
    }

    // ── Supabase commands ───────────────────────────────────────────────────

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
        IsPostgresActive = false;
        StatusMessage = "Switched to Supabase. Restart Sovrant for the change to take effect.";
        RefreshDerived();
    }

    // ── Shared commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RevertToSqliteAsync()
    {
        await _bootstrapStore.StoreAsync(CredentialKeys.DatabaseBackend, "sqlite").ConfigureAwait(true);
        IsSupabaseActive = false;
        IsPostgresActive = false;
        StatusMessage = "Reverted to SQLite. Restart Sovrant for the change to take effect.";
        RefreshDerived();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task CheckPgSchemaVersionAsync()
    {
        try
        {
            var initializer = PostgresSCE.CreateSchemaInitializer(PgConnectionUrl);
            var version     = await initializer.GetSchemaVersionAsync().ConfigureAwait(true);
            PgSchemaStatus  = version.HasValue ? PostgresSchemaStatus.UpToDate : PostgresSchemaStatus.NotInitialized;
        }
        catch
        {
            PgSchemaStatus = PostgresSchemaStatus.NotInitialized;
        }
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
        OnPropertyChanged(nameof(IsPgConnected));
        OnPropertyChanged(nameof(IsPgSchemaReady));
        OnPropertyChanged(nameof(IsPgMigrationVisible));
        OnPropertyChanged(nameof(IsPgSwitchVisible));
        OnPropertyChanged(nameof(PgConnectionStatusLabel));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsSchemaReady));
        OnPropertyChanged(nameof(IsMigrationVisible));
        OnPropertyChanged(nameof(IsSwitchVisible));
        OnPropertyChanged(nameof(ConnectionStatusLabel));
        OnPropertyChanged(nameof(ActiveBackendLabel));
    }
}
