using Microsoft.Extensions.Logging;
using Sovrant.Agents;
using Sovrant.Api.Auth;
using Sovrant.Commands;
using Sovrant.Runtime;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Logging;
using Sovrant.Runtime.Permissions;
using Sovrant.Tools;
using Sovrant.Tools.Extended;
using Sovrant.Web.Adapters;
using Sovrant.Web.Services;

namespace Sovrant.Web;

public static class Program
{
    /// <summary>Signals when runtime initialization (DB, model metadata) is complete.</summary>
    public static TaskCompletionSource RuntimeReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static async Task Main(string[] args)
    {
        // Desktop uses Fixed routing — the configured provider is used directly.
        Environment.SetEnvironmentVariable("ROUTER_MODE", "Fixed");
        var config = ConfigLoader.Load();

        // Bridge config into env vars so the API layer picks them up.
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            Environment.SetEnvironmentVariable("LLM_API_KEY", config.ApiKey);
            if (config.BaseUrl?.ToString().Contains("openrouter", StringComparison.OrdinalIgnoreCase) == true)
                Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", config.ApiKey);
        }
        if (config.BaseUrl is not null)
            Environment.SetEnvironmentVariable("LLM_BASE_URL", config.BaseUrl.ToString());

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5100");
        builder.WebHost.UseStaticWebAssets();

        builder.Services.AddLogging(b => b.AddSovrantLogging(consoleMinOverride: LogLevel.Warning));

        // Core runtime — same as Desktop's App.axaml.cs BuildApp()
        builder.Services.AddSovrantRuntime(config);
        builder.Services.AddSovrantTools();
        builder.Services.AddMultiAgentSystem();
        builder.Services.AddSovrantCommands();

        // Web-specific overrides
        var mutableAuth = new MutableAuthProvider(config.ApiKey ?? string.Empty, config.BaseUrl);
        var permissionPolicy = new MutableCliPermissionPolicy(config.PermissionMode);
        builder.Services.AddSingleton<IPermissionPolicy>(permissionPolicy);
        builder.Services.AddSingleton<IPermissionModeAccessor>(permissionPolicy);
        builder.Services.AddSingleton(config);
        var confirmationHandler = new BlazorConfirmationHandler();
        builder.Services.AddSingleton<IToolConfirmationHandler>(confirmationHandler);
        builder.Services.AddSingleton(confirmationHandler);
        builder.Services.AddSingleton<IUserInputProvider, BlazorUserInputProvider>();
        builder.Services.AddSingleton<IAuthProvider>(mutableAuth);
        builder.Services.AddSingleton(mutableAuth);
        builder.Services.AddSingleton<ActiveContextService>();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        app.MapStaticAssets();
        app.UseAntiforgery();

        app.MapRazorComponents<Sovrant.Web.Components.App>()
            .AddInteractiveServerRenderMode();

        // Initialize runtime in background (DB migrations, model metadata, MCP servers).
        _ = Task.Run(async () =>
        {
            try
            {
                await app.Services.InitializeRuntimeAsync().ConfigureAwait(false);
                app.Services.GetRequiredService<ToolRegistrar>().RegisterAll();

                var userService = app.Services.GetRequiredService<Sovrant.Runtime.Users.IUserService>();
                var user = await userService.GetAsync("web-user").ConfigureAwait(false);
                if (user is null)
                    await userService.CreateAsync("web-user", userId: "web-user").ConfigureAwait(false);

                // Ensure personal workspace exists (same as desktop's WorkspacesViewModel)
                var workspaceService = app.Services.GetRequiredService<Sovrant.Runtime.Workspaces.IWorkspaceService>();
                var personal = await workspaceService.GetPersonalAsync("web-user").ConfigureAwait(false);
                if (personal is null)
                    await workspaceService.CreatePersonalWorkspaceAsync("web-user").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<BlazorConfirmationHandler>>();
                logger.LogError(ex, "Runtime initialization failed");
            }
            finally
            {
                RuntimeReady.TrySetResult();
            }
        });

        await app.RunAsync();
    }
}
