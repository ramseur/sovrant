namespace Sovrant.Runtime.CommandCenter;

/// <summary>
/// Phase 90 (foundational) — surface for autonomous external systems ("claws")
/// that Sovrant manages but doesn't compose teams from. Claws are MCP-connected
/// (Phase 50/60) and currently parallel to the Agents → Teams → Swarms stack.
///
/// The Command Center reads from this monitor so Claws shows up as a first-class
/// bucket alongside missions, team runs, agent runs, and sessions. The full claw
/// runtime / ledger lands later; today this interface lets us wire the cockpit
/// once and swap in a real monitor without touching the UI.
/// </summary>
public interface IClawConnectionMonitor
{
    /// <summary>Returns the current set of monitored claws (active + recent).</summary>
    Task<IReadOnlyList<ClawConnectionInfo>> ListAsync(CancellationToken ct = default);
}

/// <summary>One claw — an MCP-connected autonomous external system.</summary>
public sealed record ClawConnectionInfo(
    string Id,
    string Name,
    string Status,
    DateTimeOffset LastActivity,
    string? Preview,
    string? DetailRoute);

/// <summary>
/// Default no-op monitor — used until the claw runtime lands. Lets the
/// aggregator depend on the interface without forcing a real backend.
/// </summary>
public sealed class NullClawConnectionMonitor : IClawConnectionMonitor
{
    public static readonly NullClawConnectionMonitor Instance = new();
    public Task<IReadOnlyList<ClawConnectionInfo>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ClawConnectionInfo>>([]);
}
