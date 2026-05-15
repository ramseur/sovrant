using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Storage;

namespace Sovrant.Tools.Coordination;

/// <summary>
/// Phase 57 — returns the current coordination status for a group, including
/// pending event count and recent coordination events.
/// </summary>
public sealed class CoordinationStatusTool : ITool
{
    private static readonly ToolDefinition s_definition = new("CoordinationStatus", CreateSchema())
    {
        Description =
            "Returns the coordination status for an agent group. " +
            "Provide a group_id to check pending coordination messages and recent events.",
    };

    private readonly ICoordinationEventStore _store;

    public CoordinationStatusTool(ICoordinationEventStore store) => _store = store;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var groupId = input.GetStringProp("group_id");

        if (string.IsNullOrWhiteSpace(groupId))
            return "No group_id provided. Specify a group_id to check coordination status.";

        var pendingCount = await _store.GetPendingCountAsync(groupId, ct).ConfigureAwait(false);
        var recent = await _store.ListByGroupAsync(groupId, limit: 10, ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.Append("## Coordination Status for ").AppendLine(groupId);
        sb.Append("Pending messages: ").AppendLine(pendingCount.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        if (recent.Count > 0)
        {
            sb.AppendLine("### Recent Events");
            foreach (var evt in recent)
            {
                sb.Append("- [").Append(evt.EventType).Append("] ");
                sb.Append(evt.SourceGroupId).Append(" → ").Append(evt.TargetGroupId);
                sb.Append(" (").Append(evt.Status).Append(')');
                if (!string.IsNullOrWhiteSpace(evt.Payload) && evt.Payload != "{}")
                {
                    var preview = evt.Payload.Length > 80
                        ? string.Concat(evt.Payload.AsSpan(0, 80), "...")
                        : evt.Payload;
                    sb.Append(": ").Append(preview);
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No recent coordination events.");
        }

        return sb.ToString();
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "group_id": {"type": "string", "description": "The agent group ID to check coordination status for."}
            }
        }
        """).RootElement;
}
