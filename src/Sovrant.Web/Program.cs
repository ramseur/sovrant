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
using Sovrant.Web.Services;
using Sovrant.Client.Remote;
using Sovrant.Storage.Postgres;

namespace Sovrant.Web;

public static class Program
{
    private const string StoredWebTokenKey = "sovrant.web.auth_token";

    /// <summary>Signals when runtime initialization (DB, model metadata) is complete.</summary>
    public static TaskCompletionSource RuntimeReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The authenticated user's ID. Updated after login; falls back to OS identity until then.</summary>
    public static string SovrantUserId { get; private set; } =
        Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName;

    /// <summary>True when running in remote mode (connecting to an external Sovrant.Server).</summary>
    public static bool IsRemoteMode { get; private set; }

    public static async Task Main(string[] args)
    {
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

        const int WebHttpPort = 5100;
        const int WebHttpsPortDefault = 5101;
        var webHttpsPort = bootstrapConfig.GetHttpsPort(WebHttpsPortDefault);

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.ListenAnyIP(WebHttpPort);
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

        // WebSessionService is a singleton used by all Blazor circuits.
        // For embedded mode it is populated after session restore or login.
        // For remote mode it is pre-populated with the env/OS identity.
        var webSession = new WebSessionService();
        builder.Services.AddSingleton(webSession);
        builder.Services.AddSingleton<IPrincipalAccessor>(webSession);

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

            // Restore a previously stored session token from the local credential store.
            // Validates via GET /v1/auth/me on the remote server; redirects to /login if missing/invalid.
            await TryRestoreWebRemoteSessionAsync(app.Services, webSession).ConfigureAwait(false);
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

            // Attempt to restore a stored session token before serving any request.
            // If valid, MainLayout will render normally; otherwise it redirects to /login.
            await TryRestoreWebSessionAsync(app.Services, webSession).ConfigureAwait(false);
        }

        if (bootstrapConfig.HasTls)
            app.UseHttpsRedirection();

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

                    // Only seed user/workspace if a session was restored at startup.
                    // If not, the user hasn't logged in yet; Login.razor will seed after auth.
                    if (webSession.IsAuthenticated)
                    {
                        await SeedUserAndWorkspaceAsync(app.Services, webSession.UserId!).ConfigureAwait(false);
                        // Re-apply preferences for the authenticated user. InitializeRuntimeAsync
                        // used the OS identity; this pass corrects it to the token's actual userId.
                        await app.Services.ApplyUserPreferencesForUserAsync(webSession.UserId!).ConfigureAwait(false);
                        // Sync mutableAuth with the DB-loaded config so the provider routes correctly.
                        var auth = app.Services.GetService<Sovrant.Web.Adapters.MutableAuthProvider>();
                        var cfg = app.Services.GetService<Sovrant.Runtime.Config.SovrantConfig>();
                        if (auth is not null && cfg is not null && !string.IsNullOrWhiteSpace(cfg.ApiKey))
                        {
                            auth.ApiKey = cfg.ApiKey!;
                            auth.BaseUrl = cfg.BaseUrl;
                        }
                    }
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

    internal static void SetUserId(string userId) => SovrantUserId = userId;

    private static async Task TryRestoreWebRemoteSessionAsync(IServiceProvider services, WebSessionService session)
    {
        try
        {
            var store = services.GetRequiredService<ICredentialStore>();
            var token = await store.RetrieveAsync(StoredWebTokenKey).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token)) return;

            var remoteOptions = services.GetRequiredService<SovrantRemoteOptions>();
            remoteOptions.ApiToken = token;

            // Validate against the server's /v1/auth/me endpoint.
            var httpFactory = services.GetRequiredService<IHttpClientFactory>();
            using var http = httpFactory.CreateClient("SovrantApi");
            var resp = await http.GetAsync(new Uri("/v1/auth/me", UriKind.Relative)).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var userId = doc.RootElement.TryGetProperty("user_id", out var u) ? u.GetString() : null;
            var role = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() : "user";
            if (string.IsNullOrEmpty(userId)) return;

            session.SignIn(userId, role ?? "user");
            SovrantUserId = userId;
        }
        catch
        {
            // Ignore restore errors — user will be redirected to login.
        }
    }

    private static async Task TryRestoreWebSessionAsync(IServiceProvider services, WebSessionService session)
    {
        try
        {
            var store = services.GetRequiredService<ICredentialStore>();
            var plaintext = await store.RetrieveAsync(StoredWebTokenKey).ConfigureAwait(false);
            if (string.IsNullOrEmpty(plaintext)) return;

            var tokens = services.GetRequiredService<ITokenService>();
            var resolved = await tokens.ResolveAsync(plaintext).ConfigureAwait(false);
            if (resolved is null) return;

            // Hydrate email so the sidebar shows the friendly name, not the raw usr_... ID.
            var userService = services.GetRequiredService<Sovrant.Runtime.Users.IUserService>();
            var user = await userService.GetAsync(resolved.Token.UserId).ConfigureAwait(false);
            session.SignIn(resolved.Token.UserId, resolved.Role, user?.Email);
            SovrantUserId = resolved.Token.UserId;
        }
        catch
        {
            // Ignore restore errors — user will be redirected to login.
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
