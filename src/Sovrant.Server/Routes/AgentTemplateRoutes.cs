using System.Text.Json.Serialization;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Caching;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/agents/templates</c> and <c>GET /v1/agents/templates/{name}</c>.</summary>
internal static class AgentTemplateRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/agents/templates", async (AgentTemplateRegistry registry, ICacheProvider cache, HttpContext ctx) =>
        {
            var cached = await cache.GetAsync<AgentTemplateListResponse>("templates:list").ConfigureAwait(false);
            if (cached is not null)
            {
                SetCacheHeaders(ctx, CacheTtl.Templates);
                return Results.Ok(cached);
            }

            var templates = registry.All
                .Select(t => new AgentTemplateSummaryDto
                {
                    Name = t.Name,
                    Description = t.SystemPrompt.Length > 200
                        ? string.Concat(t.SystemPrompt.AsSpan(0, 200), "...")
                        : t.SystemPrompt,
                    RecommendedLevel = t.RecommendedLevel.ToString(),
                    AllowedTools = t.AllowedTools.Count > 0 ? t.AllowedTools : null,
                })
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var response = new AgentTemplateListResponse { Templates = templates, Count = templates.Count };
            await cache.SetAsync("templates:list", response, CacheTtl.Templates).ConfigureAwait(false);
            SetCacheHeaders(ctx, CacheTtl.Templates);
            return Results.Ok(response);
        });

        app.MapGet("/v1/agents/templates/{name}", async (string name, AgentTemplateRegistry registry, ICacheProvider cache, HttpContext ctx) =>
        {
            var cacheKey = $"templates:{name}";
            var cached = await cache.GetAsync<AgentTemplateDetailDto>(cacheKey).ConfigureAwait(false);
            if (cached is not null)
            {
                SetCacheHeaders(ctx, CacheTtl.Templates);
                return Results.Ok(cached);
            }

            var template = registry.TryGet(name);
            if (template is null)
                return Results.NotFound(new { error = $"Agent template '{name}' not found." });

            var dto = new AgentTemplateDetailDto
            {
                Name = template.Name,
                Role = template.Role.ToString(),
                RecommendedLevel = template.RecommendedLevel.ToString(),
                AllowedTools = template.AllowedTools.Count > 0 ? template.AllowedTools : null,
                SystemPrompt = template.SystemPrompt,
            };
            await cache.SetAsync(cacheKey, dto, CacheTtl.Templates).ConfigureAwait(false);
            SetCacheHeaders(ctx, CacheTtl.Templates);
            return Results.Ok(dto);
        });
    }

    private static void SetCacheHeaders(HttpContext ctx, TimeSpan ttl) =>
        ctx.Response.Headers.CacheControl = $"private, max-age={(int)ttl.TotalSeconds}";
}

internal sealed class AgentTemplateListResponse
{
    [JsonPropertyName("templates")]
    public IReadOnlyList<AgentTemplateSummaryDto> Templates { get; init; } = [];

    [JsonPropertyName("count")]
    public int Count { get; init; }
}

internal sealed class AgentTemplateSummaryDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("recommended_level")]
    public string RecommendedLevel { get; init; } = string.Empty;

    [JsonPropertyName("allowed_tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AllowedTools { get; init; }
}

internal sealed class AgentTemplateDetailDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("recommended_level")]
    public string RecommendedLevel { get; init; } = string.Empty;

    [JsonPropertyName("allowed_tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AllowedTools { get; init; }

    [JsonPropertyName("system_prompt")]
    public string SystemPrompt { get; init; } = string.Empty;
}
