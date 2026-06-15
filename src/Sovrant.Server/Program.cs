using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Sovrant.Api.Auth;
using Sovrant.Api.Config;
using Sovrant.Agents;
using Sovrant.Runtime;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Storage;
using Sovrant.Server.Auth;
using Sovrant.Server.Permissions;
using Sovrant.Server.Routes;
using Sovrant.Server.ServerConfig;
using Sovrant.Server.Webhooks;
using Sovrant.Runtime.Logging;
using Sovrant.Mcp;
using Sovrant.Tools;
using Sovrant.Tools.Extended;

// ── Configuration ─────────────────────────────────────────────────────────────
var bootstrapConfig = BootstrapConfigLoader.Load(args);
var sovrantConfig = ConfigLoader.Load();
var apiConfig = ConfigLoader.BuildConfiguration();
var credentials = CredentialConfig.Resolve(apiConfig);

var serverPort = int.TryParse(
    Environment.GetEnvironmentVariable("SOVRANT_PORT"), out var p) ? p : 5200;

const int DefaultServerHttpsPort = 5443;
var serverHttpsPort = bootstrapConfig.GetHttpsPort(DefaultServerHttpsPort);

// ── Builder ───────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(serverPort);
    if (bootstrapConfig.HasTls)
    {
        o.ListenAnyIP(serverHttpsPort, listenOpts =>
        {
            if (!string.IsNullOrEmpty(bootstrapConfig.TlsKeyPath))
                listenOpts.UseHttps(X509Certificate2.CreateFromPemFile(bootstrapConfig.TlsCertPath!, bootstrapConfig.TlsKeyPath));
            else
                listenOpts.UseHttps(bootstrapConfig.TlsCertPath!, bootstrapConfig.TlsCertPassword);
        });
    }
    o.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    o.Limits.MaxRequestLineSize = 16 * 1024;         // 16 KB
});

builder.Services.AddLogging(b => b.AddSovrantLogging(logFileOverride: bootstrapConfig.LogFile));

if (bootstrapConfig.HasTls)
    builder.Services.AddHttpsRedirection(o => o.HttpsPort = serverHttpsPort);

// Mutable runtime config — single source of truth for live config changes.
// LlmApiKey lives in the encrypted credential store, not here.
var mutableConfig = new MutableServerConfig(
    model: sovrantConfig.Model,
    llmBaseUrl: credentials.LlmBaseUrl,
    permissionMode: PermissionMode.DontAsk);   // server default: never prompt

builder.Services.AddSingleton(mutableConfig);

// Core runtime (providers + router + session store + conversation runtime).
builder.Services.AddSovrantRuntime(sovrantConfig, bootstrapConfig);

// Override IAuthProvider, IPermissionPolicy, and IPermissionModeAccessor with mutable variants.
// In Microsoft DI the last registration wins for GetRequiredService<T>().
builder.Services.AddSingleton<IAuthProvider, MutableApiKeyAuthProvider>();
builder.Services.AddSingleton<IPermissionModeAccessor, SessionAwarePermissionModeAdapter>();
builder.Services.AddSingleton<IPermissionPolicy, MutablePermissionPolicy>();

// ILogger (non-generic) needed by typed HTTP client providers.
builder.Services.AddSingleton<ILogger>(sp =>
    sp.GetRequiredService<ILoggerFactory>().CreateLogger("Sovrant.Server"));

// Tools.
builder.Services.AddSovrantTools();

// Orchestration system.
builder.Services.AddOrchestrationSystem();

// MCP server over HTTP/SSE transport (opt-in via SOVRANT_MCP_HTTP=true).
var mcpHttpEnabled = string.Equals(
    Environment.GetEnvironmentVariable("SOVRANT_MCP_HTTP"), "true", StringComparison.OrdinalIgnoreCase);
if (mcpHttpEnabled)
{
    builder.Services.AddMcpServer(McpServerSetup.ConfigureMcpServerOptions)
        .WithHttpTransport()
        .AddSovrantMcpHandlers();
}

// AskUserQuestion cannot pause an HTTP stream — return a fixed message instead.
builder.Services.AddSingleton<IUserInputProvider, HttpUserInputProvider>();

// Named HttpClient for per-request scoped providers (Phase 9).
builder.Services.AddHttpClient("ScopedProvider");

// Named HttpClient for webhook callback delivery (Phase 12).
builder.Services.AddHttpClient("WebhookCallback", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<WebhookCallbackService>();

// Session eviction background service — TTL sweep + LRU cap (Phase 9.1).
builder.Services.AddHostedService<Sovrant.Server.SessionEvictionService>();

// SignalR for real-time web frontend streaming (Phase 61).
builder.Services.AddSignalR()
    .AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Middleware.
builder.Services.AddSingleton<BearerTokenMiddleware>();
builder.Services.AddSingleton<RequestLoggingMiddleware>();

// IPrincipalAccessor — reads the authenticated caller from HttpContext.Items.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Sovrant.Runtime.Auth.IPrincipalAccessor, Sovrant.Server.Auth.HttpContextPrincipalAccessor>();

// CORS — configurable via SOVRANT_CORS_ORIGINS (comma-separated); falls back to localhost defaults.
var defaultCorsOrigins = new[]
{
    "http://localhost",
    "http://localhost:3000",
    "http://localhost:5100",
    "http://localhost:5173",
    "http://localhost:8080",
    "http://127.0.0.1",
    "http://127.0.0.1:3000",
    "http://127.0.0.1:5100",
    $"https://localhost:{serverHttpsPort}",
    $"https://127.0.0.1:{serverHttpsPort}",
    "http://127.0.0.1:5173",
    "http://127.0.0.1:8080",
};
var corsOriginsEnv = Environment.GetEnvironmentVariable("SOVRANT_CORS_ORIGINS");
var corsOrigins = !string.IsNullOrWhiteSpace(corsOriginsEnv)
    ? corsOriginsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : defaultCorsOrigins;

builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
    policy
        .WithOrigins(corsOrigins)
        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
        .WithHeaders("Content-Type", "Accept", "Authorization", "X-Session-Id", "X-Workspace-Id", "X-Project-Id")
        .AllowCredentials()));

// Per-session rate limiting — keyed on session_id from the request body or "anonymous".
var rateLimitRpm = int.TryParse(
    Environment.GetEnvironmentVariable("SOVRANT_RATE_LIMIT_RPM"), out var rpm) ? rpm : 60;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-session", httpContext =>
    {
        // Key on X-Session-Id header (set by proxies) or the connection IP.
        // Never fall back to a shared bucket — each client gets its own limit.
        var key = httpContext.Request.Headers["X-Session-Id"].FirstOrDefault()
                  ?? httpContext.Connection.RemoteIpAddress?.ToString()
                  ?? httpContext.Connection.Id;

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitRpm,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

if (bootstrapConfig.HasTls)
    app.UseHttpsRedirection();

app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<BearerTokenMiddleware>();
app.UseMiddleware<Sovrant.Server.Middleware.ETagMiddleware>();
app.UseMiddleware<Sovrant.Server.Middleware.WorkspaceContextMiddleware>();
app.UseMiddleware<Sovrant.Server.Middleware.ProjectContextMiddleware>();

// Health check — unauthenticated so load balancers / monitors can ping it.
// Phase 42.5 — also reports DB path/schema/liveness so operators can detect
// a "running but DB broken" state. The DB probe is a cheap COUNT against
// schema_version; any failure flips db.status to "error".
app.MapGet("/health", (IStorageProvider storage) =>
{
    var health = storage.CheckHealth();
    var overall = health.Ok ? "ok" : "degraded";
    return Results.Ok(new
    {
        status = overall,
        db = new
        {
            status = health.Ok ? "ok" : "error",
            schema_version = health.SchemaVersion,
            path = health.Path,
            error = health.Error,
        },
    });
});

// Run DB migrations BEFORE the first GetRequiredService() that touches a
// table. ToolRegistrar's DI graph transitively resolves SwarmConfig, whose
// factory synchronously queries workspace_settings; on a fresh install that
// table does not yet exist and the resolution throws. InitializeRuntimeAsync
// (below) calls storage.InitializeAsync() too — that call is a no-op once
// migrations have already run, so the duplicate is safe.
await app.Services.GetRequiredService<IStorageProvider>().InitializeAsync().ConfigureAwait(false);

// Seed tool registry.
app.Services.GetRequiredService<ToolRegistrar>().RegisterAll();

// Connect MCP servers if configured.
await app.Services.InitializeRuntimeAsync().ConfigureAwait(false);

// Seed the encrypted credential store with the LLM API key from env if no value
// is persisted yet. This preserves the env-var bootstrap path while keeping the
// secret out of MutableServerConfig and the HTTP surface.
if (!string.IsNullOrEmpty(credentials.LlmApiKey))
{
    var credentialStore = app.Services.GetRequiredService<Sovrant.Runtime.Mcp.ICredentialStore>();
    var existing = await credentialStore.RetrieveAsync(MutableApiKeyAuthProvider.LlmApiKeyCredentialKey).ConfigureAwait(false);
    if (string.IsNullOrEmpty(existing))
        await credentialStore.StoreAsync(MutableApiKeyAuthProvider.LlmApiKeyCredentialKey, credentials.LlmApiKey).ConfigureAwait(false);
}

// Phase 88-C — re-sync MutableServerConfig from SovrantConfig now that
// InitializeRuntimeAsync's ApplyUserPreferencesAsync has populated it from
// the DB. The mutableConfig built earlier (line ~42) was seeded from the
// JSON file; this overwrite makes sure the running server uses the user's
// saved DB values on boots after the migrator renamed settings.json.
mutableConfig.Model = sovrantConfig.Model;
if (sovrantConfig.BaseUrl is not null)
    mutableConfig.LlmBaseUrl = sovrantConfig.BaseUrl.ToString();

// First-run hint — log a startup message when no users exist yet.
var identityService = app.Services.GetRequiredService<Sovrant.Runtime.Auth.IIdentityService>();
if (await identityService.IsFirstRunAsync().ConfigureAwait(false))
    app.Logger.LogInformation("No users configured — visit POST /v1/auth/register to create the first admin account.");

// Routes.
AuthRoutes.Map(app);
ChatRoutes.Map(app);
ConfigRoutes.Map(app);
StatusRoutes.Map(app);
ModelsRoutes.Map(app);
SessionRoutes.Map(app);
UsageRoutes.Map(app);
WebhookRoutes.Map(app);
McpAuthRoutes.Map(app);
McpServerRoutes.Map(app);
McpTrustRoutes.Map(app);
EvalRoutes.Map(app);
SwarmRoutes.Map(app);
EngineRoutes.Map(app);
MissionRoutes.Map(app);
TeamRoutes.Map(app);
ArtifactRoutes.Map(app);
ToolRegistryRoutes.Map(app);
SkillRegistryRoutes.Map(app);
KnowledgeAuthoringRoutes.Map(app);
AgentTemplateRoutes.Map(app);
WorkspaceRoutes.Map(app);
ProjectRoutes.Map(app);
UserRoutes.Map(app);
MeRoutes.Map(app);
CostRoutes.Map(app);
CommandCenterRoutes.Map(app);
UserDashboardRoutes.Map(app);
PrivacyRoutes.Map(app);

// SignalR hub for real-time web frontend streaming (Phase 61).
app.MapHub<Sovrant.Server.Hubs.ChatHub>("/hubs/chat");

// MCP HTTP/SSE endpoint (only when SOVRANT_MCP_HTTP=true).
if (mcpHttpEnabled)
{
    app.MapMcp("/mcp");
    app.Logger.LogInformation("MCP HTTP/SSE endpoint enabled at /mcp");
}

Sovrant.Server.ServerLog.LogServerReady(app.Logger, serverPort);

await app.RunAsync().ConfigureAwait(false);

// ── Expose the implicit Program class for WebApplicationFactory<Program> ─────
/// <summary>Enables integration tests via <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program { }

// ── Inline types (top-level-statement scope) ──────────────────────────────────

/// <summary>
/// Returns a fixed message when the engine asks for user input mid-turn over HTTP.
/// The AskUserQuestion tool cannot pause an SSE stream waiting for a response.
/// </summary>
internal sealed class HttpUserInputProvider : IUserInputProvider
{
    public Task<string> AskAsync(string question, CancellationToken ct = default) =>
        Task.FromResult(
            "[User input is not available in server mode. Please proceed without it.]");
}
