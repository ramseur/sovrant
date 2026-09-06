using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Canonical serialization for <see cref="Workflow.PlanJson"/>. Both
/// executors used to hand-roll this shape as an anonymous object on write,
/// and the Web/Desktop UIs each hand-rolled a matching read-only parser --
/// three copies of the same contract. This is the one place that shape is
/// defined, so a plan generated for review, persisted by an executor, and
/// rendered/edited by the UI all agree on the same JSON.
/// </summary>
public static class WorkflowPlanJson
{
    private static readonly JsonSerializerOptions s_options = new() { PropertyNameCaseInsensitive = true };

    public static string Serialize(RuntimePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var dto = new PlanDto
        {
            PlanId = plan.Id,
            PlanVersion = plan.PlanVersion,
            Goal = plan.Goal,
            Steps = plan.Steps.Select(s => new StepDto
            {
                Index = s.Index,
                Intent = s.Intent,
                Expected = s.ExpectedOutcome,
                Tier = s.ModelTier.ToString(),
            }).ToList(),
        };
        return JsonSerializer.Serialize(dto, s_options);
    }

    /// <summary>
    /// Parses a persisted <c>plan_json</c> value into a <see cref="RuntimePlan"/>,
    /// or <c>null</c> if it is empty, the placeholder <c>"{}"</c>, unparseable,
    /// or has no steps. Callers fall back to calling the planner when this
    /// returns <c>null</c>.
    /// </summary>
    public static RuntimePlan? TryDeserialize(string? planJson, string fallbackGoal)
    {
        if (string.IsNullOrWhiteSpace(planJson) || planJson == "{}") return null;

        PlanDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PlanDto>(planJson, s_options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (dto is null || dto.Steps.Count == 0) return null;

        return new RuntimePlan(
            Id: string.IsNullOrEmpty(dto.PlanId) ? $"plan-{Guid.NewGuid():N}" : dto.PlanId,
            PlanVersion: dto.PlanVersion,
            Goal: string.IsNullOrEmpty(dto.Goal) ? fallbackGoal : dto.Goal,
            Steps: dto.Steps
                .Where(s => !string.IsNullOrWhiteSpace(s.Intent))
                .Select(s => new RuntimeStep(
                    Index: s.Index,
                    Intent: s.Intent,
                    ExpectedOutcome: string.IsNullOrEmpty(s.Expected) ? "step completed successfully" : s.Expected,
                    ModelTier: ParseTier(s.Tier)))
                .ToList(),
            CreatedAt: DateTimeOffset.UtcNow);
    }

    private static RuntimeModelTier ParseTier(string? tier) => tier?.ToUpperInvariant() switch
    {
        "HIGH" => RuntimeModelTier.High,
        "FAST" => RuntimeModelTier.Fast,
        _ => RuntimeModelTier.Standard,
    };

    private sealed class PlanDto
    {
        [JsonPropertyName("plan_id")]
        public string? PlanId { get; set; }

        [JsonPropertyName("plan_version")]
        public int PlanVersion { get; set; } = 1;

        [JsonPropertyName("goal")]
        public string? Goal { get; set; }

        [JsonPropertyName("steps")]
        public List<StepDto> Steps { get; set; } = [];
    }

    private sealed class StepDto
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = string.Empty;

        [JsonPropertyName("expected")]
        public string Expected { get; set; } = string.Empty;

        [JsonPropertyName("tier")]
        public string Tier { get; set; } = "Standard";
    }
}
