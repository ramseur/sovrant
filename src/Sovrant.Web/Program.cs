using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Sovrant.Agents;
using Sovrant.Api.Auth;
using Sovrant.Commands;
using Sovrant.Runtime;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Logging;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Storage;
using Sovrant.Tools;
using Sovrant.Tools.Extended;
using Sovrant.Web.Adapters;
using Sovrant.Web.Auth;
using Sovrant.Web.Services;
using Sovrant.Client.Remote;
using Sovrant.Storage.Postgres;

namespace Sovrant.Web;

public static class Program
{
    private const string StoredWebTokenKey = "sovrant.web.auth_token";

    /// <summary>Signals when runtime initialization (DB, model metadata) is complete.</summary>
    public static TaskCompletionSource RuntimeReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);


    /// <summary>True when running in remote mode (connecting to an external Sovrant.Server).</summary>
    public static bool IsRemoteMode { get; private set; }

    public static async Task Main(string[] args)
    {
        // Phase 144: .env first, so every variable below (and in the runtime) honours it.
        BootstrapConfigLoader.EnsureDotEnvLoaded();
        var runtimeMode = Environment.GetEnvironmentVariable("SOVRANT_RUNTIME_MODE") ?? "embedded";
        var isRemote = string.Equals(runtimeMode, "remote", StringComparison.OrdinalIgnoreCase);

        var bootstrapConfig = BootstrapConfigLoader.Load(args);

        // ── Phase 1: read bootstrap credentials (always from SQLite) ─────────
        string? dbBackend   = null;
        string? supabaseUrl = null;
        string? supabaseKey = null;
        if (!isRemote)
        {
            var minSvc = new ServiceCollection();
            minSvc.AddSovrantStorage(bootstrapConfig);
#pragma warning disable ASP0000 // standalone mini-container, not builder.Services
            await using var minSp = minSvc.BuildServiceProvider();
#pragma warning restore ASP0000
            var minStorage = minSp.GetRequiredService<IStorageProvider>();
            await minStorage.InitializeAsync().ConfigureAwait(false);
            var minStore = minSp.GetRequiredService<ICredentialStore>();
            dbBackend   = await minStore.RetrieveAsync(CredentialKeys.DatabaseBackend).ConfigureAwait(false);
            supabaseUrl = await minStore.RetrieveAsync(CredentialKeys.SupabaseProjectUrl).ConfigureAwait(false);
            supabaseKey = await minStore.RetrieveAsync(CredentialKeys.SupabaseServiceRoleKey).ConfigureAwait(false);
        }

        // Phase 144 (GitHub #33): SOVRANT_WEB_PORT overrides the HTTP port (default 5100).
        var webHttpPort = int.TryParse(Environment.GetEnvironmentVariable("SOVRANT_WEB_PORT"), out var webPort) && webPort is > 0 and < 65536
            ? webPort : 5100;
        const int WebHttpsPortDefault = 5101;
        var webHttpsPort = bootstrapConfig.GetHttpsPort(WebHttpsPortDefault);

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenAnyIP(webHttpPort);
            if (bootstrapConfig.HasTls)
            {
                o.ListenAnyIP(webHttpsPort, listenOpts =>
                {
                    if (!string.IsNullOrEmpty(bootstrapConfig.TlsKeyPath))
                        listenOpts.UseHttps(X509Certificate2.CreateFromPemFile(bootstrapConfig.TlsCertPath!, bootstrapConfig.TlsKeyPath));
                    else
                        listenOpts.UseHttps(bootstrapConfig.TlsCertPath!, bootstrapConfig.TlsCertPassword);
                });
            }
        });
        builder.WebHost.UseStaticWebAssets();

        builder.Services.AddLogging(b => b.AddSovrantLogging(
            consoleMinOverride: LogLevel.Warning,
            logFileOverride: bootstrapConfig.LogFile));

        if (bootstrapConfig.HasTls)
            builder.Services.AddHttpsRedirection(o => o.HttpsPort = webHttpsPort);

        // Phase 144 (GitHub #33): behind a reverse proxy, trust X-Forwarded-* from known proxies.
        Sovrant.Hosting.ForwardedHeadersSetup.Configure(builder.Services,
            Environment.GetEnvironmentVariable(Sovrant.Hosting.ForwardedHeadersSetup.TrustedProxiesVariable));

        // Who is signed in (Phase 145): embedded mode reads each browser's sign-in cookie, one
        // WebSessionService per circuit/request. Remote mode keeps one process-wide user until
        // Phase 145 Part C signs each Web user in to Server separately.
        if (isRemote)
        {
            var webSession = new WebSessionService();
            builder.Services.AddSingleton(webSession);
            builder.Services.AddSingleton<IPrincipalAccessor>(webSession);
        }
        else
        {
            builder.Services.AddSovrantWebAuth();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped(sp => new WebSessionService(
                sp.GetService<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(),
                sp.GetService<IHttpContextAccessor>()));
            builder.Services.AddScoped<IPrincipalAccessor>(sp => sp.GetRequiredService<WebSessionService>());
        }

        if (isRemote)
        {
            // ── Remote mode: connect to an existing Sovrant.Server ──────────
            IsRemoteMode = true;
            var remoteOptions = new SovrantRemoteOptions
            {
                Url = Environment.GetEnvironmentVariable("SOVRANT_SERVER_URL") ?? "http://localhost:5200",
                ApiToken = Environment.GetEnvironmentVariable("SOVRANT_API_TOKEN") ?? string.Empty,
            };

            // Register local credential store so Login.razor can persist the token
            // and we can restore the session on restart.
            builder.Services.AddSovrantStorage(bootstrapConfig);
            builder.Services.AddSovrantClient(remoteOptions);
            builder.Services.AddSingleton<ActiveContextService>();
            builder.Services.AddScoped<ActiveSessionsService>();
            builder.Services.AddScoped<ChatSeedService>();
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

            // Core runtime — same as Desktop's App.axaml.cs BuildApp()
            builder.Services.AddSovrantRuntime(config, bootstrapConfig);
            builder.Services.AddSovrantTools();
            builder.Services.AddOrchestrationSystem();
            builder.Services.AddSovrantCommands();
            builder.Services.AddHttpClient("ProviderProbe", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            // Switch to Postgres if configured (Phase 40C).
            if (string.Equals(dbBackend, "supabase", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
            {
                var connStr = Sovrant.Storage.Postgres.ServiceCollectionExtensions.BuildSupabaseConnectionString(supabaseUrl, supabaseKey);
                builder.Services.AddSovrantPostgresStorage(connStr, bootstrapConfig.LegacyKeystorePath);
            }

            // Phase 145 Part B: each conversation can use its own shared provider profile (the member's
            // pick); everything else goes to the install's default router.
            Sovrant.Runtime.Conversation.SessionProviderRoutingExtensions.AddSessionProviderRouting(builder.Services);

            // Web-specific overrides
            var mutableAuth = new MutableAuthProvider(config.ApiKey ?? string.Empty, config.BaseUrl);
            // Phase 145: each conversation is judged by its owner's mode (set per chat turn); config's mode
            // is only the default for work outside a conversation.
            var permissionPolicy = new SessionAwarePermissionPolicy(config.PermissionMode);
            builder.Services.AddSingleton<IPermissionPolicy>(permissionPolicy);
            // Phase 145 stopgap: Web is shared, so members can't use file/shell tools unless an
            // admin allows it (Governance). Desktop and the CLI don't register this.
            // The caller is the ambient principal: set per request (below) and per chat turn.
            // Phase 145 Part D: file/shell tools are off on a hosted server unless an admin allows them,
            // and blocked tools are hidden from the model.
            // Slash commands that change things for everyone or use the server's files are admin-only.
            builder.Services.AddSingleton<Sovrant.Commands.ISlashCommandPolicy>(
                new Sovrant.Commands.SharedServerCommandPolicy(AmbientPrincipal.Accessor));
            Sovrant.Runtime.Tools.HostToolPolicyExtensions.AddHostToolPolicy(builder.Services, sp => new Sovrant.Runtime.Tools.MemberHostToolPolicy(
                AmbientPrincipal.Accessor, sp.GetService<Sovrant.Runtime.Workspaces.IWorkspaceSettingsStore>()));
            builder.Services.AddSingleton<IPermissionModeAccessor>(permissionPolicy);
            builder.Services.AddSingleton(config);
            var confirmationHandler = new BlazorConfirmationHandler();
            builder.Services.AddSingleton<IToolConfirmationHandler>(confirmationHandler);
            builder.Services.AddSingleton(confirmationHandler);
            builder.Services.AddSingleton<IUserInputProvider, BlazorUserInputProvider>();
            builder.Services.AddSingleton<IAuthProvider>(mutableAuth);
            builder.Services.AddSingleton(mutableAuth);
            // Phase 145: workspace, project, model, MCP servers and current chat are per browser tab.
            builder.Services.AddScoped<ActiveContextService>();
            builder.Services.AddScoped<ActiveSessionsService>();
            builder.Services.AddScoped<ChatSeedService>();
        }

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        if (isRemote)
        {
            // Initialize local credential store (for token persistence) before serving requests.
            await app.Services.GetRequiredService<Sovrant.Runtime.Storage.IStorageProvider>()
                .InitializeAsync().ConfigureAwait(false);

            // No sign-in survives a restart: everyone signs in again (see ForgetStoredWebSignInAsync).
            await ForgetStoredWebSignInAsync(app.Services).ConfigureAwait(false);
        }
        else
        {
            // Run DB migrations synchronously before app.RunAsync() so any page
            // that synchronously touches the DB on render (e.g. TrustBoundaryPage
            // resolving IWorkspaceSettingsStore in OnInitialized) can't race the
            // background InitializeRuntimeAsync. Idempotent — the deferred
            // Task.Run below re-calls it for model metadata, MCP bootstrap, etc.
            await app.Services.GetRequiredService<Sovrant.Runtime.Storage.IStorageProvider>()
                .InitializeAsync().ConfigureAwait(false);

            // No sign-in survives a restart: everyone signs in again (see ForgetStoredWebSignInAsync).
            await ForgetStoredWebSignInAsync(app.Services).ConfigureAwait(false);
        }

        // Forwarded headers first, so HTTPS redirection and links see the client's scheme/host.
        app.UseForwardedHeaders();

        if (bootstrapConfig.HasTls)
            app.UseHttpsRedirection();

        // Phase 144 (GitHub #34): unauthenticated probes for Docker HEALTHCHECK / Kubernetes.
        // /health = liveness (+ DB status, same shape as Sovrant.Server); /ready = 503 until the
        // runtime has finished starting (migrations applied, MCP servers attempted).
        app.MapGet("/health", (IServiceProvider sp) =>
        {
            if (isRemote)
                return Results.Ok(new { status = "ok", mode = "remote" });
            var health = sp.GetRequiredService<Sovrant.Runtime.Storage.IStorageProvider>().CheckHealth();
            return Results.Ok(new
            {
                status = health.Ok ? "ok" : "degraded",
                db = new { status = health.Ok ? "ok" : "error", schema_version = health.SchemaVersion, error = health.Error },
            });
        });
        app.MapGet("/ready", () => RuntimeReady.Task.IsCompleted
            ? Results.Ok(new { status = "ready" })
            : Results.Json(new { status = "starting" }, statusCode: StatusCodes.Status503ServiceUnavailable));

        if (!isRemote)
        {
            // Phase 145: each browser's sign-in cookie → its own user. The request's user also becomes
            // the ambient principal, so tool policies and background work started here know who it is.
            app.UseAuthentication();
            app.Use(async (ctx, next) =>
            {
                var uid = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (uid is null) { await next(ctx).ConfigureAwait(false); return; }
                using (AmbientPrincipal.Push(uid, ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value))
                    await next(ctx).ConfigureAwait(false);
            });
            app.UseAuthorization();
        }

        app.MapStaticAssets();
        app.UseAntiforgery();
        if (!isRemote)
            app.MapSovrantWebAuth();

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

                    // No one is signed in at startup (Phase 145: sign-in is per browser); each user's
                    // row, workspace and preferences are set up when they sign in (/auth/login).
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


    /// <summary>
    /// Earlier versions saved the last Web sign-in token and restored it at startup for the whole
    /// process, so after a restart any visitor was signed in as that user without a password.
    /// Web no longer saves the token; this deletes one left behind by an older version.
    /// Phase 145 replaces process-wide sign-in with a per-browser cookie.
    /// </summary>
    private static async Task ForgetStoredWebSignInAsync(IServiceProvider services)
    {
        try
        {
            await services.GetRequiredService<ICredentialStore>().DeleteAsync(StoredWebTokenKey).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await Console.Error.WriteLineAsync($"[web] Could not clear a stored sign-in token: {ex.Message}").ConfigureAwait(false);
        }
    }

    internal static async Task SeedUserAndWorkspaceAsync(IServiceProvider services, string userId)
    {
        var userService = services.GetRequiredService<Sovrant.Runtime.Users.IUserService>();
        var user = await userService.GetAsync(userId).ConfigureAwait(false);
        if (user is null)
            await userService.CreateAsync(userId: userId).ConfigureAwait(false);

        var workspaceService = services.GetRequiredService<Sovrant.Runtime.Workspaces.IWorkspaceService>();
        var personal = await workspaceService.GetPersonalAsync(userId).ConfigureAwait(false);
        if (personal is null)
            await workspaceService.CreatePersonalWorkspaceAsync(userId).ConfigureAwait(false);
    }

    // Serves artifact files by workspace/project/run from the LocalArtifactStore
    // root. Needed so the chat DocumentArtifactCard iframe can embed PDFs
    // (browsers block file:/// in iframes on non-file origins).
    private static void MapArtifactsEndpoint(WebApplication app)
    {
        // Single-file endpoint.
        app.MapGet("/artifacts/{workspaceId}/{projectId}/{runId}/{*relPath}",
            async (string workspaceId, string projectId, string runId, string relPath,
             HttpContext ctx,
             Sovrant.Runtime.Artifacts.IArtifactStore store,
             Sovrant.Runtime.Workspaces.IWorkspaceService wsSvc,
             WebSessionService session) =>
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

            // Personal workspaces are single-member by construction; team workspace
            // membership (any role) grants visibility into that workspace's artifacts.
            // Matches WorkspaceAuthGuards.RequireWorkspaceAccessAsync for /v1/artifacts.
            if (!session.IsAdmin &&
                !await wsSvc.IsMemberAsync(Uri.UnescapeDataString(workspaceId), session.UserId ?? string.Empty))
            {
                return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var ws = Uri.UnescapeDataString(workspaceId);
            var proj = Uri.UnescapeDataString(projectId);
            var run = Uri.UnescapeDataString(runId);
            var rel = string.Join(Path.DirectorySeparatorChar,
                relPath.Split('/').Select(Uri.UnescapeDataString));

            var rootFull = Path.GetFullPath(local.Root);
            var runDir = FindRunDir(rootFull, ws, proj, run);
            if (runDir is null) return Results.NotFound();
            var fullPath = Path.GetFullPath(Path.Combine(runDir, rel));
            if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest();
            if (!File.Exists(fullPath))
                return Results.NotFound();

            var ext = Path.GetExtension(fullPath);
            var contentType = Sovrant.Web.Services.ArtifactMime.For(ext);

            // Security: non-rendereable LLM-generated content must not execute in-browser.
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Cache-Control"] = "private, no-store";

            // Force download for types the browser would otherwise render or execute.
            if (IsUnsafeInlineType(ext))
                return Results.File(fullPath, contentType, fileDownloadName: Path.GetFileName(fullPath));

            return Results.File(fullPath, contentType, enableRangeProcessing: true);
        });

        // Streams a zip of every file under {ws}/{proj}/{run}. Lets the user grab
        // an entire run as one download from the Artifacts page.
        app.MapGet("/artifacts/{workspaceId}/{projectId}/{runId}.zip",
            async (string workspaceId, string projectId, string runId,
             HttpContext ctx,
             Sovrant.Runtime.Artifacts.IArtifactStore store,
             Sovrant.Runtime.Workspaces.IWorkspaceService wsSvc,
             WebSessionService session) =>
        {
            if (store is not Sovrant.Runtime.Artifacts.LocalArtifactStore local)
                return Results.NotFound();

            if (workspaceId.Contains("..", StringComparison.Ordinal) ||
                projectId.Contains("..", StringComparison.Ordinal) ||
                runId.Contains("..", StringComparison.Ordinal))
            {
                return Results.BadRequest();
            }

            if (!session.IsAdmin &&
                !await wsSvc.IsMemberAsync(Uri.UnescapeDataString(workspaceId), session.UserId ?? string.Empty))
            {
                return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var ws = Uri.UnescapeDataString(workspaceId);
            var proj = Uri.UnescapeDataString(projectId);
            var run = Uri.UnescapeDataString(runId);

            var rootFull = Path.GetFullPath(local.Root);
            var runDir = FindRunDir(rootFull, ws, proj, run);
            if (runDir is null) return Results.NotFound();

            var safeRun = SanitizeForFilename(run);
            var ms = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(
                ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
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
            }
            ms.Position = 0;
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Cache-Control"] = "private, no-store";
            return Results.File(ms, contentType: "application/zip", fileDownloadName: $"{safeRun}.zip");
        });
    }

    /// <summary>
    /// Resolves the run directory, handling the optional <c>{id}__{name}</c> suffix
    /// that <see cref="LocalArtifactStore"/> appends when a workspace or project has
    /// a display name. Matches the projects-only layout
    /// <c>{root}/{ws}/projects/{proj}/artifacts/{run}</c> — every artifact belongs to
    /// a workspace AND a project, there is no workspace-level shortcut.
    /// Returns <see langword="null"/> if the directory cannot be found.
    /// </summary>
    private static string? FindRunDir(string root, string ws, string proj, string run)
    {
        var wsDir = FindSegmentDir(root, ws);
        if (wsDir is null) return null;
        var projectsDir = Path.Combine(wsDir, "projects");
        var projDir = FindSegmentDir(projectsDir, proj);
        if (projDir is null) return null;
        var runDir = Path.Combine(projDir, "artifacts", run);
        return Directory.Exists(runDir) ? runDir : null;
    }

    private static string? FindSegmentDir(string parent, string id)
    {
        if (!Directory.Exists(parent)) return null;
        var exact = Path.Combine(parent, id);
        if (Directory.Exists(exact)) return exact;
        return Directory.EnumerateDirectories(parent, $"{id}__*").FirstOrDefault();
    }

    /// <summary>
    /// Returns true for file types the browser would render or execute if served inline.
    /// These must be forced to <c>Content-Disposition: attachment</c> to prevent XSS
    /// from LLM-generated content served within the user's origin.
    /// </summary>
    private static bool IsUnsafeInlineType(string ext) =>
        ext.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".htm", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".mjs", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".cjs", StringComparison.OrdinalIgnoreCase);

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
