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

        // Check if first-run setup is needed BEFORE building the DI container.
        // Desktop uses Fixed routing — the configured provider is used directly.
        Environment.SetEnvironmentVariable("ROUTER_MODE", "Fixed");
        var config = ConfigLoader.Load();
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            await RunSetupWizardAsync(desktop);
            // Reload config now that the wizard has saved settings.json.
            config = ConfigLoader.Load();
        }

        // Bridge config into env vars so the API layer picks them up.
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            Environment.SetEnvironmentVariable("LLM_API_KEY", config.ApiKey);
            if (config.BaseUrl?.ToString().Contains("openrouter", StringComparison.OrdinalIgnoreCase) == true)
                Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", config.ApiKey);
        }
        if (config.BaseUrl is not null)
            Environment.SetEnvironmentVariable("LLM_BASE_URL", config.BaseUrl.ToString());

        // Now build the full app with the correct config.
        try
        {
            BuildApp(config, desktop);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"BuildApp FATAL: {ex}");
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildApp(SovrantConfig config, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddSovrantLogging(consoleMinOverride: LogLevel.Warning));

        services.AddSovrantRuntime(config);
        services.AddSovrantTools();
        services.AddMultiAgentSystem();
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
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<WorkspacesViewModel>();
        services.AddTransient<AgentsViewModel>();
        services.AddTransient<AutomationsViewModel>();
        services.AddTransient<MultiAgentViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        _serviceProvider.GetRequiredService<ToolRegistrar>().RegisterAll();

        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = mainVm };
        desktop.MainWindow = window;
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.ShutdownRequested += (_, _) => Environment.Exit(0);
        MainWindow = window;

        // Initialize runtime in background — DB migrations, model metadata, MCP servers.
        _ = Task.Run(async () =>
        {
            try
            {
                await _serviceProvider.InitializeRuntimeAsync().ConfigureAwait(false);

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
                logger.LogError(ex, "Runtime initialization failed");
            }
            finally
            {
                RuntimeReady.TrySetResult();
            }
        });
    }

    /// <summary>
    /// Shows the setup wizard as a modal dialog before the main app loads.
    /// Returns after the user completes setup (settings.json is saved).
    /// </summary>
    private static async Task RunSetupWizardAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var setupVm = new SetupWizardViewModel(new SovrantConfig());

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
