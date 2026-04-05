using System.Text.Json.Serialization;
using Sovrant.Agents.Templates;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/agents/templates</c> and <c>GET /v1/agents/templates/{name}</c>.</summary>
internal static class AgentTemplateRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/agents/templates", (AgentTemplateRegistry registry) =>
        {
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

            return Results.Ok(new AgentTemplateListResponse { Templates = templates, Count = templates.Count });
        });

        app.MapGet("/v1/agents/templates/{name}", (string name, AgentTemplateRegistry registry) =>
        {
            var template = registry.TryGet(name);
            if (template is null)
                return Results.NotFound(new { error = $"Agent template '{name}' not found." });

            return Results.Ok(new AgentTemplateDetailDto
            {
                Name = template.Name,
                Role = template.Role.ToString(),
                RecommendedLevel = template.RecommendedLevel.ToString(),
                AllowedTools = template.AllowedTools.Count > 0 ? template.AllowedTools : null,
                SystemPrompt = template.SystemPrompt,
            });
        });
    }
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
