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
using Sovrant.Web.Services.Remote;

namespace Sovrant.Web;

public static class Program
{
    /// <summary>Signals when runtime initialization (DB, model metadata) is complete.</summary>
    public static TaskCompletionSource RuntimeReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Unified user identity — matches the runtime's default (SOVRANT_USER_ID or OS username).</summary>
    internal static readonly string SovrantUserId =
        Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName;

    public static async Task Main(string[] args)
    {
        var runtimeMode = Environment.GetEnvironmentVariable("SOVRANT_RUNTIME_MODE") ?? "embedded";
        var isRemote = string.Equals(runtimeMode, "remote", StringComparison.OrdinalIgnoreCase);

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5100");
        builder.WebHost.UseStaticWebAssets();

        builder.Services.AddLogging(b => b.AddSovrantLogging(consoleMinOverride: LogLevel.Warning));

        if (isRemote)
        {
            // ── Remote mode: connect to an existing Sovrant.Server ──────────
            var remoteOptions = new SovrantRemoteOptions
            {
                Url = Environment.GetEnvironmentVariable("SOVRANT_SERVER_URL") ?? "http://localhost:5200",
                ApiToken = Environment.GetEnvironmentVariable("SOVRANT_API_TOKEN") ?? string.Empty,
            };

            builder.Services.AddSovrantClient(remoteOptions);
            builder.Services.AddSingleton<ActiveContextService>();
        }
        else
        {
            // ── Embedded mode: full in-process runtime (existing path) ──────
            Environment.SetEnvironmentVariable("ROUTER_MODE", "Fixed");
            // Route artifact access URLs through our own HTTP endpoint so
            // the browser-side <iframe> preview can load PDFs (file:/// URIs
            // are blocked inside iframes on non-file origins).
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_URL_PREFIX")))
                Environment.SetEnvironmentVariable("SOVRANT_ARTIFACTS_URL_PREFIX", "/artifacts");
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

            // Core runtime — same as Desktop's App.axaml.cs BuildApp()
            builder.Services.AddSovrantRuntime(config);
            builder.Services.AddSovrantTools();
            builder.Services.AddMultiAgentSystem();
            builder.Services.AddSovrantCommands();
            builder.Services.AddHttpClient("ProviderProbe", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });

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
        }

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        app.MapStaticAssets();
        app.UseAntiforgery();

        app.MapRazorComponents<Sovrant.Web.Components.App>()
            .AddInteractiveServerRenderMode();

        if (!isRemote)
            MapArtifactsEndpoint(app);

        if (isRemote)
        {
            // Remote mode — connect SignalR and signal readiness.
            _ = Task.Run(async () =>
            {
                try
                {
                    var signalR = app.Services.GetRequiredService<SignalRStreamingClient>();
                    await signalR.EnsureConnectedAsync();

                    // Refresh tool registry from server.
                    if (app.Services.GetService<Sovrant.Runtime.Tools.IToolRegistry>() is RemoteToolRegistry remoteTools)
                        await remoteTools.RefreshAsync();
                }
                catch (Exception ex)
                {
                    var logger = app.Services.GetRequiredService<ILogger<RemoteConnectionState>>();
                    logger.LogError(ex, "Failed to connect to remote Sovrant server");
                }
                finally
                {
                    RuntimeReady.TrySetResult();
                }
            });
        }
        else
        {
            // Embedded mode — initialize runtime in background (DB migrations, model metadata, MCP servers).
            _ = Task.Run(async () =>
            {
                try
                {
                    await app.Services.InitializeRuntimeAsync().ConfigureAwait(false);
                    app.Services.GetRequiredService<ToolRegistrar>().RegisterAll();

                    var userService = app.Services.GetRequiredService<Sovrant.Runtime.Users.IUserService>();
                    var user = await userService.GetAsync(SovrantUserId).ConfigureAwait(false);
                    if (user is null)
                        await userService.CreateAsync(SovrantUserId, userId: SovrantUserId).ConfigureAwait(false);

                    // Ensure personal workspace exists (same as desktop's WorkspacesViewModel)
                    var workspaceService = app.Services.GetRequiredService<Sovrant.Runtime.Workspaces.IWorkspaceService>();
                    var personal = await workspaceService.GetPersonalAsync(SovrantUserId).ConfigureAwait(false);
                    if (personal is null)
                        await workspaceService.CreatePersonalWorkspaceAsync(SovrantUserId).ConfigureAwait(false);
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
        }

        await app.RunAsync();
    }

    // Serves artifact files by workspace/project/run from the LocalArtifactStore
    // root. Needed so the chat DocumentArtifactCard iframe can embed PDFs
    // (browsers block file:/// in iframes on non-file origins).
    private static void MapArtifactsEndpoint(WebApplication app)
    {
        app.MapGet("/artifacts/{workspaceId}/{projectId}/{runId}/{*relPath}",
            (string workspaceId, string projectId, string runId, string relPath,
             Sovrant.Runtime.Artifacts.IArtifactStore store) =>
        {
            if (store is not Sovrant.Runtime.Artifacts.LocalArtifactStore local)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(relPath) ||
                relPath.Contains("..", StringComparison.Ordinal) ||
                workspaceId.Contains("..", StringComparison.Ordinal) ||
                projectId.Contains("..", StringComparison.Ordinal) ||
                runId.Contains("..", StringComparison.Ordinal))
            {
                return Results.BadRequest();
            }

            var ws = Uri.UnescapeDataString(workspaceId);
            var proj = Uri.UnescapeDataString(projectId);
            var run = Uri.UnescapeDataString(runId);
            var rel = string.Join(Path.DirectorySeparatorChar,
                relPath.Split('/').Select(Uri.UnescapeDataString));

            var rootFull = Path.GetFullPath(local.Root);
            var fullPath = Path.GetFullPath(Path.Combine(rootFull, ws, proj, run, rel));
            if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest();
            if (!File.Exists(fullPath))
                return Results.NotFound();

            var contentType = GuessContentType(fullPath);
            return Results.File(fullPath, contentType, enableRangeProcessing: true);
        });
    }

    private static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return "application/pdf";
        if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase)) return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        if (ext.Equals(".pptx", StringComparison.OrdinalIgnoreCase)) return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        if (ext.Equals(".md", StringComparison.OrdinalIgnoreCase) || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)) return "text/plain; charset=utf-8";
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase)) return "application/json";
        if (ext.Equals(".html", StringComparison.OrdinalIgnoreCase) || ext.Equals(".htm", StringComparison.OrdinalIgnoreCase)) return "text/html; charset=utf-8";
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)) return "image/png";
        if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) return "image/jpeg";
        return "application/octet-stream";
    }
}
