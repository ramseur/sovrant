using System.Text.Json;
using Sovrant.Runtime.Storage;

namespace Sovrant.Cli;

/// <summary>
/// Parses a single line of a legacy <c>~/.sovrant/swarm/sessions/{swarmId}.jsonl</c>
/// file into a <see cref="SwarmEventRecord"/> ready to be inserted via
/// <see cref="ISwarmEventStore"/>. Phase 37.5 migration helper.
/// </summary>
/// <remarks>
/// Pre-Phase 37.5, <c>SwarmSession</c> wrote each event with the default
/// PascalCase property names of the concrete record. The discriminator was
/// reconstructed by sniffing for unique property names — we mirror that
/// approach here so the importer round-trips files written by any pre-37.5
/// build. Workspace/project columns are left null because legacy files were
/// never scope-stamped.
/// </remarks>
internal static class JsonlSwarmImport
{
    /// <summary>
    /// Parses one JSONL line into a <see cref="SwarmEventRecord"/>, or returns
    /// <c>null</c> when the line cannot be classified as a known swarm event.
    /// </summary>
    public static SwarmEventRecord? TryBuildRecord(string swarmId, string line)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var eventType = ClassifyEventType(root);
            if (eventType is null)
                return null;

            var timestamp = root.TryGetProperty("Timestamp", out var tsEl) && tsEl.TryGetDateTimeOffset(out var ts)
                ? ts
                : DateTimeOffset.UtcNow;

            var agentId = root.TryGetProperty("AgentName", out var agentEl) && agentEl.ValueKind == JsonValueKind.String
                ? agentEl.GetString()
                : null;

            return new SwarmEventRecord(
                SwarmId: swarmId,
                EventType: eventType,
                Payload: line,
                Timestamp: timestamp,
                AgentId: agentId,
                WorkspaceId: null,
                ProjectId: null);
        }
    }

    /// <summary>
    /// Recovers the SwarmEvent subtype name from a legacy payload by sniffing
    /// for properties that are unique to each event variant. The order matters
    /// — most-specific checks come first.
    /// </summary>
    private static string? ClassifyEventType(JsonElement root)
    {
        if (root.TryGetProperty("TaskCount", out _)) return "PlanCreated";
        if (root.TryGetProperty("FilePath", out _)) return "FileConflict";
        if (root.TryGetProperty("Output", out _)) return "TaskCompleted";
        if (root.TryGetProperty("Error", out _)) return "TaskFailed";
        if (root.TryGetProperty("Wave", out _)) return "TaskStarted";
        if (root.TryGetProperty("Limit", out _)) return "BudgetExceeded";
        if (root.TryGetProperty("FinalStatus", out _)) return "SwarmCompleted";
        if (root.TryGetProperty("Verdict", out _)) return "QualityGateCompleted";

        // QualityGateStarted has nothing beyond SwarmId + Timestamp.
        var propCount = 0;
        foreach (var _ in root.EnumerateObject())
            propCount++;
        if (propCount == 2 && root.TryGetProperty("SwarmId", out _) && root.TryGetProperty("Timestamp", out _))
            return "QualityGateStarted";

        return null;
    }
}
