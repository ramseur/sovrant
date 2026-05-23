namespace Sovrant.Agents.Swarm.Bus;

/// <summary>
/// Maps a swarm ID, execution context, and federation mode to a deterministic
/// OpenClaw route name used for publishing and subscribing to swarm events.
/// </summary>
public static class RouteResolver
{
    /// <summary>
    /// Returns the fully-qualified route name for the given swarm.
    ///
    /// Format:
    ///   Silo        → null  (no route; caller should skip publish)
    ///   Federated   → {prefix}.{workspaceId}.{swarmId}
    ///   ManagerLed  → {prefix}.manager.{parentSwarmId}.{swarmId}
    /// </summary>
    public static string? Resolve(
        string swarmId,
        SwarmExecutionContext context,
        SwarmFederationMode mode,
        string routePrefix,
        string? parentSwarmId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(swarmId);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);

        return mode switch
        {
            SwarmFederationMode.Silo => null,
            SwarmFederationMode.Federated =>
                $"{routePrefix}.{Sanitize(context.WorkspaceId)}.{Sanitize(swarmId)}",
            SwarmFederationMode.ManagerLed =>
                $"{routePrefix}.manager.{Sanitize(parentSwarmId ?? "unknown")}.{Sanitize(swarmId)}",
            _ => null,
        };
    }

    private static string Sanitize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "default" : value.Replace('/', '-').Replace('\\', '-');
}
