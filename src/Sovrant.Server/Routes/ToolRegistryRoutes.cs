using System.Text.Json.Serialization;
using Sovrant.Runtime.Caching;
using Sovrant.Runtime.Tools;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/tools</c> and <c>GET /v1/tools/{name}</c>.</summary>
internal static class ToolRegistryRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/tools", async (IToolRegistry registry, ICacheProvider cache, HttpContext ctx) =>
        {
            var cached = await cache.GetAsync<ToolListResponse>("tools:list").ConfigureAwait(false);
            if (cached is not null)
            {
                SetCacheHeaders(ctx, CacheTtl.Tools);
                return Results.Ok(cached);
            }

            var tools = registry.GetDefinitions()
                .Select(d => new ToolDto
                {
                    Name = d.Name,
                    Description = d.Description,
                    Parameters = d.InputSchema,
                })
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var response = new ToolListResponse { Tools = tools, Count = tools.Count };
            await cache.SetAsync("tools:list", response, CacheTtl.Tools).ConfigureAwait(false);
            SetCacheHeaders(ctx, CacheTtl.Tools);
            return Results.Ok(response);
        });

        app.MapGet("/v1/tools/{name}", async (string name, IToolRegistry registry, ICacheProvider cache, HttpContext ctx) =>
        {
            var cacheKey = $"tools:{name}";
            var cached = await cache.GetAsync<ToolDto>(cacheKey).ConfigureAwait(false);
            if (cached is not null)
            {
                SetCacheHeaders(ctx, CacheTtl.Tools);
                return Results.Ok(cached);
            }

            var def = registry.GetDefinitions()
                .FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

            if (def is null)
                return Results.NotFound(new { error = $"Tool '{name}' not found." });

            var dto = new ToolDto
            {
                Name = def.Name,
                Description = def.Description,
                Parameters = def.InputSchema,
            };
            await cache.SetAsync(cacheKey, dto, CacheTtl.Tools).ConfigureAwait(false);
            SetCacheHeaders(ctx, CacheTtl.Tools);
            return Results.Ok(dto);
        });
    }

    private static void SetCacheHeaders(HttpContext ctx, TimeSpan ttl) =>
        ctx.Response.Headers.CacheControl = $"private, max-age={(int)ttl.TotalSeconds}";
}

internal sealed class ToolListResponse
{
    [JsonPropertyName("tools")]
    public IReadOnlyList<ToolDto> Tools { get; init; } = [];

    [JsonPropertyName("count")]
    public int Count { get; init; }
}

internal sealed class ToolDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; init; }
}
