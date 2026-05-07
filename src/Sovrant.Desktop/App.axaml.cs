using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Agents;
using Sovrant.Api.Auth;
using Sovrant.Client.Remote;
using Sovrant.Commands;
using Sovrant.Desktop.Adapters;
using Sovrant.Desktop.Auth;
using Sovrant.Desktop.ViewModels;
using Sovrant.Desktop.Views;
using Sovrant.Runtime;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Logging;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Storage;
using Sovrant.Tools;
using Sovrant.Tools.Extended;

namespace Sovrant.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// The authenticated user's ID after login.
    /// Falls back to the OS identity for embedded/dev runs without a login flow.
    /// </summary>
    internal static string SovrantUserId { get; private set; } =
        Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName;

    public static IServiceProvider Services { get; private set; } = null!;
    public static Window? MainWindow { get; private set; }

    /// <summary>Signals when runtime initialization (DB, model metadata) is complete.</summary>
    public static TaskCompletionSource RuntimeReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        // Desktop uses Fixed routing — the configured provider is used directly.
        Environment.SetEnvironmentVariable("ROUTER_MODE", "Fixed");

        // Build DI first; ApplyUserPreferencesAsync (Phase 88-C) runs inside
        // InitializeRuntimeAsync and hydrates SovrantConfig from the DB +
        // credential store, so we can't decide whether to show the setup
        // wizard until that step has run. ConfigLoader.Load() now only reads
        // sovrant.config / env vars — never per-user settings — so its
        // ApiKey is empty for everyone except the env-var bootstrap path.
        var config = ConfigLoader.Load();

        // Bridge env-var bootstrap values (rare; mostly CI / dev) so the API
        // layer sees them immediately. Real per-user values come from
        // InitializeRuntimeAsync below.
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            Environment.SetEnvironmentVariable("LLM_API_KEY", config.ApiKey);
            if (config.BaseUrl?.ToString().Contains("openrouter", StringComparison.OrdinalIgnoreCase) == true)
                Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", config.ApiKey);
        }
        if (config.BaseUrl is not null)
            Environment.SetEnvironmentVariable("LLM_BASE_URL", config.BaseUrl.ToString());

        try
        {
            await BuildAppAsync(config, desktop).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"BuildApp FATAL: {ex}");
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task BuildAppAsync(SovrantConfig config, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var bootstrap = BootstrapConfigLoader.Load();

        // ── Phase 1: detect runtime mode ──────────────────────────────────────
        // Build a minimal storage container to read the stored mode before committing
        // to either the embedded runtime or the remote client stack.
        var isRemote = false;
        string? remoteServerUrl = Environment.GetEnvironmentVariable("SOVRANT_SERVER_URL");
        string? remoteApiToken = null;

        if (string.IsNullOrEmpty(remoteServerUrl))
        {
            var minSvc = new ServiceCollection();
            minSvc.AddSovrantStorage(bootstrap);
            await using var minSp = minSvc.BuildServiceProvider();
            var minStorage = minSp.GetRequiredService<IStorageProvider>();
            await minStorage.InitializeAsync().ConfigureAwait(true);
            var minStore = minSp.GetRequiredService<ICredentialStore>();
            var mode = await minStore.RetrieveAsync(CredentialKeys.RuntimeMode).ConfigureAwait(true);
            if (string.Equals(mode, "remote", StringComparison.OrdinalIgnoreCase))
            {
                remoteServerUrl = await minStore.RetrieveAsync(CredentialKeys.RemoteServerUrl).ConfigureAwait(true);
                remoteApiToken = await minStore.RetrieveAsync(CredentialKeys.RemoteApiToken).ConfigureAwait(true);
            }
        }
        else
        {
            remoteApiToken = Environment.GetEnvironmentVariable("SOVRANT_API_TOKEN");
        }

        isRemote = !string.IsNullOrEmpty(remoteServerUrl);

        // ── Phase 2: build full DI container ─────────────────────────────────
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddSovrantLogging(
            consoleMinOverride: LogLevel.Warning,
            logFileOverride: bootstrap.LogFile));

        // IPrincipalAccessor — populated after login.
        var principalAccessor = new DesktopPrincipalAccessor();
        services.AddSingleton<IPrincipalAccessor>(principalAccessor);
        services.AddSingleton(principalAccessor);

        // Desktop-specific overrides.
        var mutableAuth = new MutableAuthProvider(config.ApiKey ?? string.Empty);
        var permissionPolicy = new MutableCliPermissionPolicy(config.PermissionMode);
        services.AddSingleton<IPermissionPolicy>(permissionPolicy);
        services.AddSingleton<IPermissionModeAccessor>(permissionPolicy);
        services.AddSingleton(config);
        var confirmationHandler = new DesktopConfirmationHandler();
        services.AddSingleton<IToolConfirmationHandler>(confirmationHandler);
        services.AddSingleton(confirmationHandler);
        services.AddSingleton<IUserInputProvider, DesktopUserInputProvider>();
        services.AddSingleton<IAuthProvider>(mutableAuth);
        services.AddSingleton(mutableAuth);

        if (isRemote)
        {
            var remoteOptions = new SovrantRemoteOptions { Url = remoteServerUrl, ApiToken = remoteApiToken };
            services.AddSovrantClient(remoteOptions);
            // Also register the credential store (via storage) for token persistence across restarts.
            services.AddSovrantStorage(bootstrap);
        }
        else
        {
            services.AddSovrantRuntime(config, bootstrap);
            services.AddSovrantTools();
            services.AddOrchestrationSystem();
            services.AddSovrantCommands();
            services.AddHttpClient("ProviderProbe", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });
        }

        // ViewModels.
        services.AddSingleton<ActiveContextViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SidebarViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<IntegrationsViewModel>();
        services.AddTransient<ArtifactsViewModel>();
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<ToolsViewModel>();
        services.AddTransient<SkillsViewModel>();
        services.AddTransient<MemoryViewModel>();
        services.AddTransient<GovernanceViewModel>();
        services.AddTransient<TrustBoundaryViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<WorkspacesViewModel>();
        services.AddTransient<AgentsViewModel>();
        services.AddTransient<AutomationsViewModel>();
        services.AddTransient<OrchestrationViewModel>();
        // Singleton: MainViewModel subscribes to RowSelected once at startup.
        services.AddSingleton<CommandCenterViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<AdminViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // ── Phase 3: initialize runtime ───────────────────────────────────────
        if (!isRemote)
        {
            _serviceProvider.GetRequiredService<ToolRegistrar>().RegisterAll();

            // Run DB migrations + the legacy migrator + ApplyUserPreferencesAsync
            // synchronously so SovrantConfig.ApiKey is hydrated from the DB before
            // we decide whether to show the setup wizard.
            await _serviceProvider.InitializeRuntimeAsync().ConfigureAwait(true);
        }
        else
        {
            // Remote mode: initialize local credential store (for token storage),
            // then connect SignalR and refresh the remote tool registry.
            var remoteStorage = _serviceProvider.GetRequiredService<IStorageProvider>();
            await remoteStorage.InitializeAsync().ConfigureAwait(true);

            _ = Task.Run(async () =>
            {
                try
                {
                    var signalR = _serviceProvider.GetRequiredService<SignalRStreamingClient>();
                    await signalR.EnsureConnectedAsync().ConfigureAwait(false);
                    var toolRegistry = _serviceProvider.GetRequiredService<RemoteToolRegistry>();
                    await toolRegistry.RefreshAsync().ConfigureAwait(false);
                }
                finally
                {
                    RuntimeReady.TrySetResult();
                }
            });
        }

        // ── Login / token validation ─────────────────────────────────────
        string? authenticatedUserId;
        if (isRemote)
        {
            // Remote mode: check credential store for stored token, validate against server.
            authenticatedUserId = await TryRestoreRemoteSessionAsync(_serviceProvider, principalAccessor).ConfigureAwait(true);
        }
        else
        {
            authenticatedUserId = await TryRestoreSessionAsync(_serviceProvider, principalAccessor).ConfigureAwait(true);
        }

        if (authenticatedUserId is null)
        {
            authenticatedUserId = await RunLoginWindowAsync(desktop, _serviceProvider, principalAccessor).ConfigureAwait(true);
        }
        SovrantUserId = authenticatedUserId;

        // ── API key / setup wizard ────────────────────────────────────────────
        // Skip setup wizard in remote mode — the server handles LLM credentials.
        if (!isRemote && string.IsNullOrWhiteSpace(config.ApiKey))
        {
            await RunSetupWizardAsync(desktop, _serviceProvider).ConfigureAwait(true);
        }

        // Refresh the auth provider's API key (local mode only).
        if (!isRemote && !string.IsNullOrWhiteSpace(config.ApiKey))
            mutableAuth.ApiKey = config.ApiKey!;

        // ── 401 monitoring in remote mode ─────────────────────────────────────
        if (isRemote)
        {
            var connectionState = _serviceProvider.GetRequiredService<RemoteConnectionState>();
            connectionState.StatusChanged += async (_, status) =>
            {
                if (status != ConnectionStatus.Disconnected) return;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var principal = _serviceProvider.GetRequiredService<DesktopPrincipalAccessor>();
                    MainWindow?.Hide();
                    var userId = await RunLoginWindowAsync(desktop, _serviceProvider, principal).ConfigureAwait(true);
                    SovrantUserId = userId;
                    // Persist and hot-swap the new token.
                    var store = _serviceProvider.GetRequiredService<ICredentialStore>();
                    var remoteOpts = _serviceProvider.GetRequiredService<SovrantRemoteOptions>();
                    var newToken = await store.RetrieveAsync(StoredTokenKey).ConfigureAwait(true);
                    if (!string.IsNullOrEmpty(newToken))
                        remoteOpts.ApiToken = newToken;
                    MainWindow?.Show();
                });
            };
        }

        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = mainVm };
        desktop.MainWindow = window;
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.ShutdownRequested += (_, _) => Environment.Exit(0);
        MainWindow = window;
        window.Show();

        // Background user/workspace seeding (local mode only — server handles this in remote mode).
        if (!isRemote)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var userService = _serviceProvider.GetRequiredService<Sovrant.Runtime.Users.IUserService>();
                    var user = await userService.GetAsync(SovrantUserId).ConfigureAwait(false);
                    if (user is null)
                        await userService.CreateAsync(SovrantUserId, userId: SovrantUserId).ConfigureAwait(false);

                    var workspaceService = _serviceProvider.GetRequiredService<Sovrant.Runtime.Workspaces.IWorkspaceService>();
                    var personal = await workspaceService.GetPersonalAsync(SovrantUserId).ConfigureAwait(false);
                    if (personal is null)
                        await workspaceService.CreatePersonalWorkspaceAsync(SovrantUserId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
                    logger.LogError(ex, "User/workspace seeding failed");
                }
                finally
                {
                    RuntimeReady.TrySetResult();
                }
            });
        }
    }

    private const string StoredTokenKey = "sovrant.desktop.auth_token";

    /// <summary>
    /// Clears the stored session, hides the main window, re-runs the login flow,
    /// and shows the main window again. Called from Settings → Log out.
    /// </summary>
    internal static async Task LogoutAsync()
    {
        if (Services is null) return;

        var store = Services.GetRequiredService<ICredentialStore>();
        await store.DeleteAsync(StoredTokenKey).ConfigureAwait(true);

        var principal = Services.GetRequiredService<DesktopPrincipalAccessor>();
        principal.UserId = null;
        principal.Role = null;

        var desktop = (IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!;

        MainWindow?.Hide();

        var authenticatedUserId = await RunLoginWindowAsync(desktop, Services, principal).ConfigureAwait(true);
        SovrantUserId = authenticatedUserId;

        MainWindow?.Show();
    }

    /// <summary>
    /// In remote mode: checks the credential store for a stored bearer token,
    /// validates it against the server's /v1/auth/me endpoint.
    /// Returns the user ID on success, null if login is needed.
    /// </summary>
    private static async Task<string?> TryRestoreRemoteSessionAsync(
        IServiceProvider services, DesktopPrincipalAccessor principal)
    {
        var store = services.GetRequiredService<ICredentialStore>();
        var token = await store.RetrieveAsync(StoredTokenKey).ConfigureAwait(true)
            ?? services.GetRequiredService<SovrantRemoteOptions>().ApiToken;
        if (string.IsNullOrEmpty(token)) return null;

        var remoteOpts = services.GetRequiredService<SovrantRemoteOptions>();
        using var http = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri(remoteOpts.Url!.TrimEnd('/'))
        };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            var resp = await http.GetAsync(new Uri("/v1/auth/me", UriKind.Relative)).ConfigureAwait(true);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(true);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var userId = doc.RootElement.TryGetProperty("user_id", out var u) ? u.GetString() : null;
            var role = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() : "user";
            if (userId is null) return null;

            // Hot-swap the stored token into the running options.
            remoteOpts.ApiToken = token;
            principal.UserId = userId;
            principal.Role = role;
            return userId;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks the credential store for a valid session token.
    /// Returns the user ID if the token resolves successfully, otherwise null.
    /// </summary>
    private static async Task<string?> TryRestoreSessionAsync(
        IServiceProvider services, DesktopPrincipalAccessor principal)
    {
        var store = services.GetRequiredService<ICredentialStore>();
        var plaintext = await store.RetrieveAsync(StoredTokenKey).ConfigureAwait(true);
        if (string.IsNullOrEmpty(plaintext)) return null;

        var tokens = services.GetRequiredService<ITokenService>();
        var resolved = await tokens.ResolveAsync(plaintext).ConfigureAwait(true);
        if (resolved is null) return null;

        principal.UserId = resolved.Token.UserId;
        principal.Role = resolved.Role;
        return resolved.Token.UserId;
    }

    /// <summary>
    /// Shows the login window and waits for the user to authenticate.
    /// Returns the authenticated user ID.
    /// </summary>
    private static async Task<string> RunLoginWindowAsync(
        IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider services,
        DesktopPrincipalAccessor principal)
    {
        var loginVm = services.GetRequiredService<LoginViewModel>();
        await loginVm.InitializeAsync().ConfigureAwait(true);

        var tcs = new TaskCompletionSource<(string userId, string role)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        loginVm.LoginSucceeded += (userId, role) => tcs.TrySetResult((userId, role));

        var loginWindow = new LoginWindow { DataContext = loginVm };

        // Prevent closing without completing login.
        loginWindow.Closing += (_, e) =>
        {
            if (!tcs.Task.IsCompleted)
                desktop.Shutdown();
        };

        loginWindow.Show();

        var (userId, role) = await tcs.Task.ConfigureAwait(true);
        loginWindow.Close();

        principal.UserId = userId;
        principal.Role = role;
        return userId;
    }

    /// <summary>
    /// Shows the setup wizard as a modal dialog before the main app loads.
    /// The wizard writes credentials/preferences/profile rows to the DB +
    /// credential store via DI services and hot-swaps the running
    /// <see cref="SovrantConfig"/>; no on-disk JSON file is touched.
    /// </summary>
    private static async Task RunSetupWizardAsync(
        IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider services)
    {
        var setupVm = ActivatorUtilities.CreateInstance<SetupWizardViewModel>(services);

        var wizardWindow = new Window
        {
            Title = "Sovrant — Setup",
            Width = 540,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            CanResize = false,
            Content = new SetupWizardOverlay { DataContext = setupVm },
        };

        // Close the window when the wizard finishes.
        setupVm.SetupCompleted += () =>
            Dispatcher.UIThread.Post(() => wizardWindow.Close());

        // Prevent user from closing without completing setup.
        wizardWindow.Closing += (_, e) =>
        {
            if (setupVm.IsVisible)
            {
                // Wizard not completed — exit the app instead.
                desktop.Shutdown();
            }
        };

        wizardWindow.Show();

        // Wait for the window to close (either wizard completed or user closed it).
        var tcs = new TaskCompletionSource();
        wizardWindow.Closed += (_, _) => tcs.TrySetResult();
        await tcs.Task;
    }
}
