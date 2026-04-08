using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Agents.Swarm;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Swarm;

/// <summary>
/// Agent-callable tool that runs the full swarm pipeline: decompose → execute → quality gate.
/// Returns a summary of the swarm result.
/// </summary>
public sealed class SwarmTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Swarm", CreateSchema())
    {
        Description =
            "Decomposes a complex task into parallel sub-tasks, executes them via a swarm of agents, " +
            "and optionally runs a quality gate review. Requires swarm to be enabled in config.",
    };

    private readonly SwarmConfig _config;
    private readonly ISwarmDecomposer _decomposer;
    private readonly SwarmOrchestrator _orchestrator;
    private readonly SwarmQualityGate _qualityGate;
    private readonly SwarmStateTracker _stateTracker;
    private readonly ISwarmProgressReporter _progress;

    public SwarmTool(
        SwarmConfig config,
        ISwarmDecomposer decomposer,
        SwarmOrchestrator orchestrator,
        SwarmQualityGate qualityGate,
        SwarmStateTracker stateTracker,
        ISwarmProgressReporter progress)
    {
        _config = config;
        _decomposer = decomposer;
        _orchestrator = orchestrator;
        _qualityGate = qualityGate;
        _stateTracker = stateTracker;
        _progress = progress;
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        if (!_config.Enabled)
            return "Error: Swarm orchestration is disabled. Use /swarm enable to turn it on.";

        var prompt = input.GetStringProp("prompt");
        if (string.IsNullOrWhiteSpace(prompt))
            return "Error: prompt is required.";

        var dryRun = input.GetBoolProp("dry_run");
        var teamId = input.GetStringProp("team");

        // Phase 1: Decompose
        SwarmPlan plan;
        try
        {
            plan = await _decomposer.DecomposeAsync(prompt, _config, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(teamId))
                plan.TeamId = teamId;
        }
        catch (InvalidOperationException ex)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Error decomposing task: {ex.Message}");
        }

        if (dryRun)
            return FormatDryRun(plan);

        // Phase 2: Execute
        var result = await _orchestrator.ExecuteAsync(plan, _config, onEvent: _progress.Report, ct: ct).ConfigureAwait(false);

        // Phase 3: Quality gate (optional)
        if (_config.QualityGateEnabled && result.Status == SwarmStatus.Completed)
        {
            result.Status = SwarmStatus.QualityReview;
            _stateTracker.Update(result.SwarmId, result);

            var verdict = await _qualityGate.ReviewAsync(
                result.SwarmId, prompt, result.CombinedOutput, ct).ConfigureAwait(false);
            result.QualityGate = verdict;
            result.Status = SwarmStatus.Completed;
            _stateTracker.Update(result.SwarmId, result);
        }

        return FormatResult(result);
    }

    private static string FormatDryRun(SwarmPlan plan)
    {
        var sb = new StringBuilder();
        sb.Append("## Swarm Plan (dry run) — ").Append(plan.Tasks.Count.ToString(CultureInfo.InvariantCulture))
          .Append(" tasks, ").Append(plan.WaveCount.ToString(CultureInfo.InvariantCulture)).AppendLine(" waves");
        sb.AppendLine();

        for (var wave = 0; wave < plan.WaveCount; wave++)
        {
            sb.Append("### Wave ").AppendLine(wave.ToString(CultureInfo.InvariantCulture));
            foreach (var task in plan.GetWave(wave))
            {
                sb.Append("- **").Append(task.Id).Append("**: ").AppendLine(task.Description);
                if (task.Dependencies.Count > 0)
                    sb.Append("  depends on: ").AppendLine(string.Join(", ", task.Dependencies));
                if (task.FilesToModify.Count > 0)
                    sb.Append("  files: ").AppendLine(string.Join(", ", task.FilesToModify));
            }
        }

        return sb.ToString();
    }

    private static string FormatResult(SwarmResult result)
    {
        var sb = new StringBuilder();
        sb.Append("## Swarm ").Append(result.SwarmId).Append(" — ").AppendLine(result.Status.ToString());
        sb.Append("Duration: ").Append(result.Duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)).AppendLine("s");
        sb.Append("Tokens: ").AppendLine(result.TotalTokensUsed.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        foreach (var task in result.Tasks)
        {
            sb.Append("- **").Append(task.Id).Append("** [").Append(task.Status.ToString()).Append("]: ").AppendLine(task.Description);
        }

        if (result.QualityGate is { } qg)
        {
            sb.AppendLine();
            sb.Append("### Quality Gate: ").Append(qg.Verdict).Append(" (score ").Append(qg.Score.ToString(CultureInfo.InvariantCulture)).AppendLine(")");
            sb.AppendLine(qg.Feedback);
        }

        return sb.ToString();
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "prompt":  {"type": "string",  "description": "The complex task to decompose and execute via the swarm."},
                "team":    {"type": "string",  "description": "Optional team name/ID. When set, swarm workers are resolved from this multi-agent team before falling back to templates."},
                "dry_run": {"type": "boolean", "description": "If true, only decompose and show the plan without executing."}
            },
            "required": ["prompt"]
        }
        """).RootElement;
}
