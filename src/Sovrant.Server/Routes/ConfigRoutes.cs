using System.Text.Json.Serialization;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Permissions;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/config</c> and <c>PUT /v1/config</c>.</summary>
internal static class ConfigRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/config", GetConfig);
        app.MapPut("/v1/config", PutConfig);
    }

    private static IResult GetConfig(MutableServerConfig config) =>
        Results.Ok(new ConfigResponse
        {
            Model = config.Model,
            BaseUrl = config.LlmBaseUrl,
            PermissionMode = config.PermissionMode.ToString().ToLowerInvariant(),
            Provider = config.PinnedProvider,
        });

    private static async Task<IResult> PutConfig(
        ConfigUpdateRequest req,
        MutableServerConfig config,
        ISmartRouter router,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (req.Model is not null)
            config.Model = req.Model;

        if (req.ApiKey is not null)
            config.LlmApiKey = req.ApiKey;

        if (req.BaseUrl is not null)
            config.LlmBaseUrl = req.BaseUrl;

        if (req.PermissionMode is not null &&
            Enum.TryParse<PermissionMode>(req.PermissionMode, ignoreCase: true, out var pm))
            config.PermissionMode = pm;

        if (req.Provider is not null)
        {
            config.PinnedProvider = req.Provider;
            try
            {
                await router.PinProviderAsync(req.Provider, ct).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }

        return Results.Ok(new ConfigResponse
        {
            Model = config.Model,
            BaseUrl = config.LlmBaseUrl,
            PermissionMode = config.PermissionMode.ToString().ToLowerInvariant(),
            Provider = config.PinnedProvider,
        });
    }
}

/// <summary>Response body for <c>GET /v1/config</c> and <c>PUT /v1/config</c>.</summary>
internal sealed class ConfigResponse
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; init; } = string.Empty;

    [JsonPropertyName("permission_mode")]
    public string PermissionMode { get; init; } = string.Empty;

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Provider { get; init; }
}

/// <summary>Request body for <c>PUT /v1/config</c>. All fields are optional.</summary>
internal sealed class ConfigUpdateRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>Write-only: sets the LLM API key. Never returned in GET responses.</summary>
    [JsonPropertyName("api_key")]
    public string? ApiKey { get; init; }

    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; init; }

    [JsonPropertyName("permission_mode")]
    public string? PermissionMode { get; init; }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }
}
