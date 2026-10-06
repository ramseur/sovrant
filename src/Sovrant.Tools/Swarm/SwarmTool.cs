using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Swarm.Bus;
using Sovrant.Api.Types;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

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
    private readonly ISwarmOrchestrator _orchestrator;
    private readonly SwarmQualityGate _qualityGate;
    private readonly ISwarmStateTracker _stateTracker;
    private readonly ISwarmProgressReporter _progress;
    private readonly IAgentRunStore? _runStore;

    public SwarmTool(
        SwarmConfig config,
        ISwarmDecomposer decomposer,
        ISwarmOrchestrator orchestrator,
        SwarmQualityGate qualityGate,
        ISwarmStateTracker stateTracker,
        ISwarmProgressReporter progress,
        IAgentRunStore? runStore = null)
    {
        _config = config;
        _decomposer = decomposer;
        _orchestrator = orchestrator;
        _qualityGate = qualityGate;
        _stateTracker = stateTracker;
        _progress = progress;
        _runStore = runStore;
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
        var federationRaw = input.GetStringProp("federation");
        var parentSwarmId = input.GetStringProp("parent_swarm_id");

        // Merge per-call federation override into a config clone
        var config = MergeFederation(_config, federationRaw, parentSwarmId);

        // Phase 1: Decompose
        SwarmPlan plan;
        try
        {
            plan = await _decomposer.DecomposeAsync(prompt, config, ct).ConfigureAwait(false);
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
        var runId = await RecordRunStartAsync(prompt, ct).ConfigureAwait(false);
        SwarmResult result;
        try
        {
            result = await _orchestrator.ExecuteAsync(plan, config, onEvent: _progress.Report, ct: ct).ConfigureAwait(false);
        }
        catch
        {
            await RecordRunEndAsync(runId, succeeded: false).ConfigureAwait(false);
            throw;
        }
        await RecordRunEndAsync(runId, succeeded: result.Status == SwarmStatus.Completed).ConfigureAwait(false);

        // Phase 3: Quality gate (optional)
        if (config.QualityGateEnabled && result.Status == SwarmStatus.Completed)
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

    /// <summary>
    /// Phase 133 — when the swarm is launched from a chat, records a <c>swarm</c>
    /// run linked to that conversation so its sidebar row can show "Swarm · n runs".
    /// Best-effort: a ledger failure never blocks or fails the swarm itself.
    /// </summary>
    private async Task<string?> RecordRunStartAsync(string prompt, CancellationToken ct)
    {
        if (_runStore is null || TurnContext.Current is not { } turn)
            return null;
        try
        {
            var userId = turn.OwnerUserId is { Length: > 0 } u ? u : WorkspaceIdentity.CurrentUserId;
            var runId = $"run-{Guid.NewGuid():N}";
            await _runStore.CreateAsync(new AgentRunRecord(
                RunId: runId,
                ParentRunId: null,
                TeamId: null,
                MemberId: null,
                WorkspaceId: Environment.GetEnvironmentVariable("SOVRANT_WORKSPACE_ID") ?? WorkspaceIdentity.DefaultPersonalFor(userId),
                ProjectId: null,
                UserId: userId,
                Kind: "swarm",
                Status: "running",
                StartedAt: DateTimeOffset.UtcNow,
                Prompt: prompt.Length > 120 ? prompt[..117] + "…" : prompt,
                SessionId: turn.SessionId), ct).ConfigureAwait(false);
            return runId;
        }
#pragma warning disable CA1031 // best-effort ledger write: never fail the swarm over it
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }

    private async Task RecordRunEndAsync(string? runId, bool succeeded)
    {
        if (_runStore is null || runId is null)
            return;
        try
        {
            await _runStore.UpdateStatusAsync(runId, succeeded ? "succeeded" : "failed", ct: CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // best-effort ledger write: never fail the swarm over it
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }

    private static SwarmConfig MergeFederation(SwarmConfig base_, string? federationRaw, string? parentSwarmId)
    {
        if (string.IsNullOrWhiteSpace(federationRaw) && string.IsNullOrWhiteSpace(parentSwarmId))
            return base_;

        var mode = federationRaw?.ToUpperInvariant() switch
        {
            "FEDERATED"   => SwarmFederationMode.Federated,
            "MANAGER_LED" => SwarmFederationMode.ManagerLed,
            _             => SwarmFederationMode.Silo,
        };

        var fedConfig = new SwarmFederationConfig
        {
            Mode = mode,
            ParentSwarmId = parentSwarmId,
            OpenClaw = base_.Federation?.OpenClaw,
        };

        return new SwarmConfig
        {
            Enabled = base_.Enabled,
            MaxConcurrent = base_.MaxConcurrent,
            MaxTokenBudget = base_.MaxTokenBudget,
            MaxRetries = base_.MaxRetries,
            QualityGateEnabled = base_.QualityGateEnabled,
            QualityGateThreshold = base_.QualityGateThreshold,
            FileLocksEnabled = base_.FileLocksEnabled,
            DecomposerLevel = base_.DecomposerLevel,
            WorkerLevel = base_.WorkerLevel,
            TaskTimeoutSeconds = base_.TaskTimeoutSeconds,
            Permissions = base_.Permissions,
            TemplateOverrides = base_.TemplateOverrides,
            Federation = fedConfig,
        };
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
                "prompt":          {"type": "string",  "description": "The complex task to decompose and execute via the swarm."},
                "team":            {"type": "string",  "description": "Optional team name/ID. When set, swarm workers are resolved from this orchestrated team before falling back to templates."},
                "dry_run":         {"type": "boolean", "description": "If true, only decompose and show the plan without executing."},
                "federation":      {"type": "string",  "enum": ["silo", "federated", "manager_led"], "description": "Federation mode override. 'silo' (default) runs locally; 'federated' publishes events to OpenClaw; 'manager_led' treats this swarm as a child of a remote manager."},
                "parent_swarm_id": {"type": "string",  "description": "ID of the parent swarm when federation is 'manager_led'. Ignored otherwise."}
            },
            "required": ["prompt"]
        }
        """).RootElement;
}
