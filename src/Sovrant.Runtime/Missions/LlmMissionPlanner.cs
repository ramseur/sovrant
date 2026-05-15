using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 51 — LLM-backed <see cref="IMissionPlanner"/>. Asks a model
/// to decompose a mission goal into a multi-step <see cref="RuntimePlan"/>
/// instead of the single-step stub the <see cref="SimpleMissionPlanner"/>
/// produces. Falls back to the simple planner if the LLM call fails or
/// returns unparseable output so the mission layer never stalls on a
/// planning error.
///
/// The prompt asks the model to return a JSON array of step objects,
/// each with <c>intent</c>, <c>expected_outcome</c>, and optionally
/// <c>model_tier</c> ("high" / "standard" / "fast"). The planner
/// parses the array and builds a <see cref="RuntimePlan"/> with
/// contiguous indexes.
/// </summary>
public sealed partial class LlmMissionPlanner : IMissionPlanner
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "LlmMissionPlanner: LLM decomposition failed, falling back to simple planner: {Error}")]
    private static partial void LogFallback(ILogger logger, string error);

    private readonly IRuntimeSessionPool _sessionPool;
    private readonly SimpleMissionPlanner _fallback = new();
    private readonly ILogger<LlmMissionPlanner> _logger;

    private const string PlannerSessionId = "__sovrant_mission_planner__";

    public LlmMissionPlanner(
        IRuntimeSessionPool sessionPool,
        ILogger<LlmMissionPlanner>? logger = null)
    {
        _sessionPool = sessionPool ?? throw new ArgumentNullException(nameof(sessionPool));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LlmMissionPlanner>.Instance;
    }

    public async Task<RuntimePlan> PlanAsync(Mission mission, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var prompt = BuildPrompt(mission);

        try
        {
            var rawJson = await AskLlmAsync(prompt, ct).ConfigureAwait(false);
            var steps = ParseSteps(rawJson);
            if (steps.Count == 0)
            {
                LogFallback(_logger, "LLM returned empty step list");
                return await _fallback.PlanAsync(mission, ct).ConfigureAwait(false);
            }

            return new RuntimePlan(
                Id: $"plan-{Guid.NewGuid():N}",
                PlanVersion: 1,
                Goal: mission.Goal,
                Steps: steps,
                CreatedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFallback(_logger, ex.Message);
            return await _fallback.PlanAsync(mission, ct).ConfigureAwait(false);
        }
    }

    private async Task<string> AskLlmAsync(string prompt, CancellationToken ct)
    {
        var pooled = await _sessionPool
            .GetOrCreateAsync(PlannerSessionId, scopedRouterOverride: null, ownerUserId: null, ct)
            .ConfigureAwait(false);

        var text = new StringBuilder();
        await pooled.Lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await foreach (var evt in pooled.Runtime.RunTurnAsync(prompt, ct).ConfigureAwait(false))
            {
                if (evt is RuntimeEvent.TextChunk tc)
                    text.Append(tc.Text);
            }
        }
        finally
        {
            pooled.Lock.Release();
        }

        return text.ToString().Trim();
    }

    private static string BuildPrompt(Mission mission) =>
        $$"""
        You are a mission planner. Decompose the following goal into 2-8 concrete steps
        that an autonomous coding agent can execute sequentially. Each step should be
        small enough to complete in one tool-calling turn.

        Return ONLY a JSON array (no markdown fences, no commentary) where each element is:
        {"intent": "what to do", "expected_outcome": "what success looks like", "model_tier": "standard"}

        model_tier must be one of: "high" (reasoning-heavy), "standard" (normal), "fast" (mechanical).

        Mission goal: {{mission.Goal}}
        """;

    private static List<RuntimeStep> ParseSteps(string rawJson)
    {
        // Strip markdown fences if the model wrapped the JSON.
        var json = rawJson;
        var fenceStart = json.IndexOf('[', StringComparison.Ordinal);
        var fenceEnd = json.LastIndexOf(']');
        if (fenceStart >= 0 && fenceEnd > fenceStart)
            json = json[fenceStart..(fenceEnd + 1)];

        using var doc = JsonDocument.Parse(json);
        var steps = new List<RuntimeStep>();
        var index = 0;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var intent = element.TryGetProperty("intent", out var ip) ? ip.GetString() : null;
            var expected = element.TryGetProperty("expected_outcome", out var ep) ? ep.GetString() : null;
            if (string.IsNullOrWhiteSpace(intent)) continue;

            var tierText = element.TryGetProperty("model_tier", out var tp) ? tp.GetString() : "standard";
            var tier = tierText switch
            {
                "high" => RuntimeModelTier.High,
                "fast" => RuntimeModelTier.Fast,
                _ => RuntimeModelTier.Standard,
            };

            steps.Add(new RuntimeStep(
                Index: index++,
                Intent: intent!,
                ExpectedOutcome: expected ?? "step completed successfully",
                ModelTier: tier));
        }

        return steps;
    }
}
