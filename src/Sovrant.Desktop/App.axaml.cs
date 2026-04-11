using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Agents;
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

    public static IServiceProvider Services { get; private set; } = null!;
    public static Window? MainWindow { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var config = ConfigLoader.Load();
        var needsSetup = string.IsNullOrWhiteSpace(config.ApiKey);

        var services = new ServiceCollection();

        // Logging — suppress console noise, file logging only.
        services.AddLogging(b => b.AddSovrantLogging(consoleMinOverride: LogLevel.Warning));

        // Core runtime + tools + agents + commands.
        services.AddSovrantRuntime(config);
        services.AddSovrantTools();
        services.AddMultiAgentSystem();
        services.AddSovrantCommands();

        // Desktop-specific overrides.
        var permissionPolicy = new MutableCliPermissionPolicy(config.PermissionMode);
        services.AddSingleton<IPermissionPolicy>(permissionPolicy);
        services.AddSingleton<IPermissionModeAccessor>(permissionPolicy);
        services.AddSingleton(config);
        services.AddSingleton<IToolConfirmationHandler, DesktopConfirmationHandler>();
        services.AddSingleton<IUserInputProvider, DesktopUserInputProvider>();

        // ViewModels.
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SidebarViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<IntegrationsViewModel>();
        services.AddTransient<ArtifactsViewModel>();
        services.AddTransient<ToolsViewModel>();
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

        // Seed tool registry.
        _serviceProvider.GetRequiredService<ToolRegistrar>().RegisterAll();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();

            // Show setup wizard overlay if no API key is configured.
            if (needsSetup)
            {
                var setupVm = new SetupWizardViewModel(config);
                mainVm.SetupWizard = setupVm;

                setupVm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(SetupWizardViewModel.IsVisible) && !setupVm.IsVisible)
                    {
                        // Restart the app so the runtime picks up the new config.
                        RestartApp();
                    }
                };
            }

            var window = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = window;
            MainWindow = window;
        }

        // Initialize runtime in background (provider pings, MCP connect, live model fetch).
        _ = Task.Run(async () =>
        {
            try
            {
                await _serviceProvider.InitializeRuntimeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
                logger.LogError(ex, "Runtime initialization failed");
            }
        });

        base.OnFrameworkInitializationCompleted();
    }

    private static void RestartApp()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
            });
        }

        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
