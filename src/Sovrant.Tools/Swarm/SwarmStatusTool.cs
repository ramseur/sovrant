using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Agents.Swarm;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Swarm;

/// <summary>Returns live swarm progress from <see cref="SwarmStateTracker"/>.</summary>
public sealed class SwarmStatusTool : ITool
{
    private static readonly ToolDefinition s_definition = new("SwarmStatus", CreateSchema())
    {
        Description =
            "Returns the current status of a running or completed swarm. " +
            "Provide a swarm ID to check a specific swarm, or omit to see the most recent.",
    };

    private readonly ISwarmStateTracker _tracker;

    public SwarmStatusTool(ISwarmStateTracker tracker) => _tracker = tracker;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var swarmId = input.GetStringProp("swarm_id");

        SwarmResult? result;
        if (!string.IsNullOrWhiteSpace(swarmId))
        {
            result = _tracker.Get(swarmId);
            if (result is null)
                return Task.FromResult(string.Create(CultureInfo.InvariantCulture, $"No swarm found with ID '{swarmId}'."));
        }
        else
        {
            result = _tracker.GetLatest();
            if (result is null)
                return Task.FromResult("No swarms have been executed in this session.");
        }

        return Task.FromResult(FormatStatus(result));
    }

    private static string FormatStatus(SwarmResult result)
    {
        var sb = new StringBuilder();
        sb.Append("## Swarm ").Append(result.SwarmId).Append(" — ").AppendLine(result.Status.ToString());

        if (result.Duration > TimeSpan.Zero)
            sb.Append("Duration: ").Append(result.Duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)).AppendLine("s");

        sb.Append("Tokens: ").AppendLine(result.TotalTokensUsed.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        var completed = 0;
        var failed = 0;
        var running = 0;
        var pending = 0;

        foreach (var task in result.Tasks)
        {
            switch (task.Status)
            {
                case SwarmTaskStatus.Completed: completed++; break;
                case SwarmTaskStatus.Failed: failed++; break;
                case SwarmTaskStatus.Running: running++; break;
                default: pending++; break;
            }
        }

        sb.Append("Tasks: ").Append(completed.ToString(CultureInfo.InvariantCulture)).Append(" completed, ")
          .Append(running.ToString(CultureInfo.InvariantCulture)).Append(" running, ")
          .Append(failed.ToString(CultureInfo.InvariantCulture)).Append(" failed, ")
          .Append(pending.ToString(CultureInfo.InvariantCulture)).AppendLine(" pending");

        if (result.QualityGate is { } qg)
        {
            sb.AppendLine();
            sb.Append("Quality Gate: ").Append(qg.Verdict).Append(" (score ").Append(qg.Score.ToString(CultureInfo.InvariantCulture)).AppendLine(")");
        }

        return sb.ToString();
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "swarm_id": {"type": "string", "description": "Optional swarm ID. Omit to see the most recent swarm."}
            }
        }
        """).RootElement;
}
