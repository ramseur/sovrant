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

        var bootstrapConfig = BootstrapConfigLoader.Load(args);

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5100");
        builder.WebHost.UseStaticWebAssets();

        builder.Services.AddLogging(b => b.AddSovrantLogging(
            consoleMinOverride: LogLevel.Warning,
            logFileOverride: bootstrapConfig.LogFile));

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
            builder.Services.AddSovrantRuntime(config, bootstrapConfig);
            builder.Services.AddSovrantTools();
            builder.Services.AddOrchestrationSystem();
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

        if (!isRemote)
        {
            // Run DB migrations synchronously before app.RunAsync() so any page
            // that synchronously touches the DB on render (e.g. TrustBoundaryPage
            // resolving IWorkspaceSettingsStore in OnInitialized) can't race the
            // background InitializeRuntimeAsync. Idempotent — the deferred
            // Task.Run below re-calls it for model metadata, MCP bootstrap, etc.
            await app.Services.GetRequiredService<Sovrant.Runtime.Storage.IStorageProvider>()
                .InitializeAsync().ConfigureAwait(false);
        }

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
        // Single-file endpoint.
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

            var contentType = Sovrant.Web.Services.ArtifactMime.For(Path.GetExtension(fullPath));
            return Results.File(fullPath, contentType, enableRangeProcessing: true);
        });

        // Streams a zip of every file under {ws}/{proj}/{run}. Lets the user grab
        // an entire run as one download from the Artifacts page.
        app.MapGet("/artifacts/{workspaceId}/{projectId}/{runId}.zip",
            (string workspaceId, string projectId, string runId,
             Sovrant.Runtime.Artifacts.IArtifactStore store) =>
        {
            if (store is not Sovrant.Runtime.Artifacts.LocalArtifactStore local)
                return Results.NotFound();

            if (workspaceId.Contains("..", StringComparison.Ordinal) ||
                projectId.Contains("..", StringComparison.Ordinal) ||
                runId.Contains("..", StringComparison.Ordinal))
            {
                return Results.BadRequest();
            }

            var ws = Uri.UnescapeDataString(workspaceId);
            var proj = Uri.UnescapeDataString(projectId);
            var run = Uri.UnescapeDataString(runId);

            var rootFull = Path.GetFullPath(local.Root);
            var runDir = Path.GetFullPath(Path.Combine(rootFull, ws, proj, run));
            if (!runDir.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest();
            if (!Directory.Exists(runDir))
                return Results.NotFound();

            var safeRun = SanitizeForFilename(run);
            return Results.Stream(async (output) =>
            {
                using var archive = new System.IO.Compression.ZipArchive(
                    output, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: false);
                foreach (var file in Directory.EnumerateFiles(runDir, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file).Equals("_manifest.json", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var rel = Path.GetRelativePath(runDir, file).Replace('\\', '/');
                    var entry = archive.CreateEntry(rel, System.IO.Compression.CompressionLevel.Fastest);
                    await using var entryStream = await entry.OpenAsync().ConfigureAwait(false);
                    await using var fileStream = File.OpenRead(file);
                    await fileStream.CopyToAsync(entryStream).ConfigureAwait(false);
                }
            }, contentType: "application/zip", fileDownloadName: $"{safeRun}.zip");
        });
    }

    private static string SanitizeForFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var span = name.AsSpan();
        Span<char> buffer = stackalloc char[Math.Min(span.Length, 64)];
        var len = 0;
        for (var i = 0; i < span.Length && len < buffer.Length; i++)
        {
            var c = span[i];
            buffer[len++] = Array.IndexOf(invalid, c) >= 0 ? '_' : c;
        }
        return len == 0 ? "artifacts" : new string(buffer[..len]);
    }
}
