using Sovrant.Api.Auth;
using Sovrant.Runtime;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Permissions;
using Sovrant.Server.Auth;
using Sovrant.Server.Permissions;
using Sovrant.Server.Routes;
using Sovrant.Server.ServerConfig;
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

builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

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
builder.Services.AddSingleton<IPermissionPolicy, MutablePermissionPolicy>();
builder.Services.AddSingleton<IPermissionModeAccessor, MutableServerPermissionModeAdapter>();

// ILogger (non-generic) needed by typed HTTP client providers.
builder.Services.AddSingleton<ILogger>(sp =>
    sp.GetRequiredService<ILoggerFactory>().CreateLogger("Sovrant.Server"));

// Tools.
builder.Services.AddSovrantTools();

// AskUserQuestion cannot pause an HTTP stream — return a fixed message instead.
builder.Services.AddSingleton<IUserInputProvider, HttpUserInputProvider>();

// Auth middleware.
builder.Services.AddSingleton<BearerTokenMiddleware>();

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
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()));

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseCors();
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

app.Logger.LogInformation(
    "Sovrant Server ready — http://127.0.0.1:{Port}", serverPort);

await app.RunAsync().ConfigureAwait(false);

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
