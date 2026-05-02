using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Agents;
using Sovrant.Api.Auth;
using Sovrant.Commands;
using Sovrant.Desktop.Adapters;
using Sovrant.Desktop.ViewModels;
using Sovrant.Desktop.Views;
using Sovrant.Runtime;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Logging;
using Sovrant.Runtime.Permissions;
using Sovrant.Tools;
using Sovrant.Tools.Extended;

namespace Sovrant.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>Unified user identity — matches the runtime's default (SOVRANT_USER_ID or OS username).</summary>
    internal static readonly string SovrantUserId =
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
        var services = new ServiceCollection();
        var bootstrap = BootstrapConfigLoader.Load();

        services.AddLogging(b => b.AddSovrantLogging(
            consoleMinOverride: LogLevel.Warning,
            logFileOverride: bootstrap.LogFile));

        services.AddSovrantRuntime(config, bootstrap);
        services.AddSovrantTools();
        services.AddOrchestrationSystem();
        services.AddSovrantCommands();
        services.AddHttpClient("ProviderProbe", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

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
        services.AddTransient<ActivityViewModel>();
        services.AddTransient<CommandCenterViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        _serviceProvider.GetRequiredService<ToolRegistrar>().RegisterAll();

        // Run DB migrations + the legacy migrator + ApplyUserPreferencesAsync
        // synchronously so SovrantConfig.ApiKey is hydrated from the DB before
        // we decide whether to show the setup wizard. Subsequent boot work
        // (model metadata, MCP servers, user/workspace seeding) runs on a
        // background task below.
        await _serviceProvider.InitializeRuntimeAsync().ConfigureAwait(true);

        // First-run setup — only after ApplyUserPreferencesAsync has had a
        // chance to populate config.ApiKey from the credential store.
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            await RunSetupWizardAsync(desktop, _serviceProvider).ConfigureAwait(true);
            // The wizard hot-swaps SovrantConfig in place; no reload needed.
        }

        // Refresh the auth provider's API key now that the wizard (or the
        // boot path) has populated config.ApiKey.
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            mutableAuth.ApiKey = config.ApiKey!;

        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = mainVm };
        desktop.MainWindow = window;
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.ShutdownRequested += (_, _) => Environment.Exit(0);
        MainWindow = window;
        // Avalonia's classic desktop lifetime calls Start() once the initial
        // sync part of OnFrameworkInitializationCompleted returns, and would
        // normally Show() MainWindow at that point. Because BuildAppAsync
        // awaits, the lifetime has already Started before we set MainWindow,
        // so we Show() explicitly. On first-run paths the setup wizard's own
        // Show() kept the pump alive and masked this; with an existing
        // API key no wizard runs and the bug surfaces.
        window.Show();

        // Background user/workspace seeding — InitializeRuntimeAsync above
        // already covered storage migrations, the legacy migrator, the
        // preference apply step, and model-metadata fetching. The remaining
        // work just needs to happen before the user starts a chat session.
        _ = Task.Run(async () =>
        {
            try
            {
                // Ensure the desktop user exists (required by workspace FK constraints).
                var userService = _serviceProvider.GetRequiredService<Sovrant.Runtime.Users.IUserService>();
                var user = await userService.GetAsync(SovrantUserId).ConfigureAwait(false);
                if (user is null)
                    await userService.CreateAsync(SovrantUserId, userId: SovrantUserId).ConfigureAwait(false);

                // Ensure a personal workspace exists for the desktop user.
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
