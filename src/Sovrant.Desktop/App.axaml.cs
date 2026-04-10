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
        var services = new ServiceCollection();

        // Logging — suppress console noise, file logging only.
        services.AddLogging(b => b.AddSovrantLogging(consoleMinOverride: LogLevel.Warning));

        // Core runtime + tools + agents + commands.
        var config = ConfigLoader.Load();
        services.AddSovrantRuntime(config);
        services.AddSovrantTools();
        services.AddMultiAgentSystem();
        services.AddSovrantCommands();

        // Desktop-specific overrides.
        services.AddSingleton<IPermissionPolicy>(new MutableCliPermissionPolicy(config.PermissionMode));
        services.AddSingleton<IToolConfirmationHandler, DesktopConfirmationHandler>();
        services.AddSingleton<IUserInputProvider, DesktopUserInputProvider>();

        // ViewModels.
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SidebarViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Seed tool registry.
        _serviceProvider.GetRequiredService<ToolRegistrar>().RegisterAll();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
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
}
