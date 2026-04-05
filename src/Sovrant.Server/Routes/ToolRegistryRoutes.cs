using System.Text.Json.Serialization;
using Sovrant.Runtime.Tools;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/tools</c> and <c>GET /v1/tools/{name}</c>.</summary>
internal static class ToolRegistryRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/tools", (IToolRegistry registry) =>
        {
            var tools = registry.GetDefinitions()
                .Select(d => new ToolDto
                {
                    Name = d.Name,
                    Description = d.Description,
                    Parameters = d.InputSchema,
                })
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Results.Ok(new ToolListResponse { Tools = tools, Count = tools.Count });
        });

        app.MapGet("/v1/tools/{name}", (string name, IToolRegistry registry) =>
        {
            var def = registry.GetDefinitions()
                .FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

            if (def is null)
                return Results.NotFound(new { error = $"Tool '{name}' not found." });

            return Results.Ok(new ToolDto
            {
                Name = def.Name,
                Description = def.Description,
                Parameters = def.InputSchema,
            });
        });
    }
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
