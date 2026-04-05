using System.Threading.RateLimiting;
using Sovrant.Api.Auth;
using Sovrant.Agents;
using Sovrant.Runtime;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;
using Sovrant.Server.Auth;
using Sovrant.Server.Permissions;
using Sovrant.Server.Routes;
using Sovrant.Server.ServerConfig;
using Sovrant.Server.Webhooks;
using Sovrant.Runtime.Logging;
using Sovrant.Tools;
using Sovrant.Tools.Extended;

// ── Configuration ─────────────────────────────────────────────────────────────
var sovrantConfig = ConfigLoader.Load();

var llmApiKey =
    Environment.GetEnvironmentVariable("LLM_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? sovrantConfig.ApiKey
    ?? string.Empty;

var llmBaseUrl =
    Environment.GetEnvironmentVariable("LLM_BASE_URL")
    ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
    ?? sovrantConfig.BaseUrl?.ToString()
    ?? "https://api.openai.com/v1";

var serverPort = int.TryParse(
    Environment.GetEnvironmentVariable("SOVRANT_PORT"), out var p) ? p : 5200;

// ── Builder ───────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.ListenLocalhost(serverPort));

builder.Services.AddLogging(b => b.AddSovrantLogging());

// Mutable runtime config — single source of truth for live config changes.
var mutableConfig = new MutableServerConfig(
    model: sovrantConfig.Model,
    llmApiKey: llmApiKey,
    llmBaseUrl: llmBaseUrl,
    permissionMode: PermissionMode.DontAsk);   // server default: never prompt

builder.Services.AddSingleton(mutableConfig);

// Core runtime (providers + router + session store + conversation runtime).
builder.Services.AddSovrantRuntime(sovrantConfig);

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

// Multi-agent system.
builder.Services.AddMultiAgentSystem();

// AskUserQuestion cannot pause an HTTP stream — return a fixed message instead.
builder.Services.AddSingleton<IUserInputProvider, HttpUserInputProvider>();

// Named HttpClient for per-request scoped providers (Phase 9).
builder.Services.AddHttpClient("ScopedProvider");

// Named HttpClient for webhook callback delivery (Phase 12).
builder.Services.AddHttpClient("WebhookCallback");
builder.Services.AddSingleton<WebhookCallbackService>();

// Session eviction background service — TTL sweep + LRU cap (Phase 9.1).
builder.Services.AddHostedService<Sovrant.Server.SessionEvictionService>();

// Middleware.
builder.Services.AddSingleton<BearerTokenMiddleware>();
builder.Services.AddSingleton<RequestLoggingMiddleware>();

// CORS — localhost only.
builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
    policy
        .WithOrigins(
            "http://localhost",
            "http://localhost:3000",
            "http://localhost:5173",
            "http://localhost:8080",
            "http://127.0.0.1",
            "http://127.0.0.1:3000",
            "http://127.0.0.1:5173",
            "http://127.0.0.1:8080")
        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
        .WithHeaders("Content-Type", "Accept", "Authorization", "X-Session-Id", "X-LLM-Api-Key", "X-LLM-Base-Url")
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

// ── Startup validation ────────────────────────────────────────────────────────
var sovrantToken = Environment.GetEnvironmentVariable("SOVRANT_TOKEN")
    ?? builder.Configuration["Server:Token"];
if (string.IsNullOrEmpty(sovrantToken))
{
    throw new InvalidOperationException(
        "SOVRANT_TOKEN environment variable is required. " +
        "Set it to a non-empty bearer token that clients must supply to authenticate requests.");
}

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<BearerTokenMiddleware>();

// Health check — unauthenticated so load balancers / monitors can ping it.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Seed tool registry.
app.Services.GetRequiredService<ToolRegistrar>().RegisterAll();

// Connect MCP servers if configured.
await app.Services.InitializeRuntimeAsync().ConfigureAwait(false);

// Routes.
ChatRoutes.Map(app);
ConfigRoutes.Map(app);
StatusRoutes.Map(app);
ModelsRoutes.Map(app);
SessionRoutes.Map(app);
UsageRoutes.Map(app);
WebhookRoutes.Map(app);
McpAuthRoutes.Map(app);
EvalRoutes.Map(app);

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
