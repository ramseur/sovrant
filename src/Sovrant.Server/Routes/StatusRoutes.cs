using System.Text.Json.Serialization;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Conversation;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/status</c>.</summary>
internal static class StatusRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/status", async (
            ISmartRouter router,
            MutableServerConfig config,
            IRuntimeSessionPool sessionPool,
            CancellationToken ct) =>
        {
            await router.InitializeAsync(ct).ConfigureAwait(false);
            var providers = router.GetStatus()
                .Select(s => new ProviderStatusDto
                {
                    Name = s.Name,
                    Healthy = s.Healthy,
                    LatencyMs = s.LatencyMs,
                    RequestCount = s.RequestCount,
                    ErrorCount = s.ErrorCount,
                    ErrorRate = s.ErrorRate,
                    Score = s.Score,
                })
                .ToList();

            // Read eviction service config from env vars (same defaults as SessionEvictionService).
            var ttlSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("SOVRANT_SESSION_TTL_SECONDS"), out var t) ? t : 3600;
            var maxSessions = int.TryParse(
                Environment.GetEnvironmentVariable("SOVRANT_MAX_SESSIONS"), out var m) ? m : 500;

            return Results.Ok(new StatusResponse
            {
                Providers = providers,
                ActiveModel = config.Model,
                PermissionMode = config.PermissionMode.ToString().ToLowerInvariant(),
                PinnedProvider = config.PinnedProvider,
                ActiveSessions = sessionPool.ActiveCount,
                MaxSessions = maxSessions,
                SessionTtlSeconds = ttlSeconds,
            });
        });
    }
}

internal sealed class StatusResponse
{
    [JsonPropertyName("providers")]
    public IReadOnlyList<ProviderStatusDto> Providers { get; init; } = [];

    [JsonPropertyName("active_model")]
    public string ActiveModel { get; init; } = string.Empty;

    [JsonPropertyName("permission_mode")]
    public string PermissionMode { get; init; } = string.Empty;

    [JsonPropertyName("pinned_provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PinnedProvider { get; init; }

    [JsonPropertyName("active_sessions")]
    public int ActiveSessions { get; init; }

    [JsonPropertyName("max_sessions")]
    public int MaxSessions { get; init; }

    [JsonPropertyName("session_ttl_seconds")]
    public int SessionTtlSeconds { get; init; }
}

internal sealed class ProviderStatusDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("healthy")]
    public bool Healthy { get; init; }

    [JsonPropertyName("latency_ms")]
    public double LatencyMs { get; init; }

    [JsonPropertyName("request_count")]
    public int RequestCount { get; init; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; init; }

    [JsonPropertyName("error_rate")]
    public string ErrorRate { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public string Score { get; init; } = string.Empty;
}
