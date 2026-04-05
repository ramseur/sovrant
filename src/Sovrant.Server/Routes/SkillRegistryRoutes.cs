using System.Text.Json.Serialization;
using Sovrant.Tools.Skills;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/skills</c> and <c>GET /v1/skills/{name}</c>.</summary>
internal static class SkillRegistryRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/skills", (SkillRegistry registry) =>
        {
            var skills = registry.All
                .Select(s => new SkillSummaryDto
                {
                    Name = s.Name,
                    Description = s.Description,
                    Trigger = string.IsNullOrWhiteSpace(s.Trigger) ? null : s.Trigger,
                    Agents = s.Agents.Count > 0 ? s.Agents : null,
                    Tools = s.Tools.Count > 0 ? s.Tools : null,
                })
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Results.Ok(new SkillListResponse { Skills = skills, Count = skills.Count });
        });

        app.MapGet("/v1/skills/{name}", (string name, SkillRegistry registry) =>
        {
            var skill = registry.TryGetByName(name);
            if (skill is null)
                return Results.NotFound(new { error = $"Skill '{name}' not found." });

            return Results.Ok(new SkillDetailDto
            {
                Name = skill.Name,
                Description = skill.Description,
                Trigger = string.IsNullOrWhiteSpace(skill.Trigger) ? null : skill.Trigger,
                Agents = skill.Agents.Count > 0 ? skill.Agents : null,
                Tools = skill.Tools.Count > 0 ? skill.Tools : null,
                Body = skill.Body,
            });
        });
    }
}

internal sealed class SkillListResponse
{
    [JsonPropertyName("skills")]
    public IReadOnlyList<SkillSummaryDto> Skills { get; init; } = [];

    [JsonPropertyName("count")]
    public int Count { get; init; }
}

internal sealed class SkillSummaryDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("trigger")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Trigger { get; init; }

    [JsonPropertyName("agents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Agents { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Tools { get; init; }
}

internal sealed class SkillDetailDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("trigger")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Trigger { get; init; }

    [JsonPropertyName("agents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Agents { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Tools { get; init; }

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;
}
