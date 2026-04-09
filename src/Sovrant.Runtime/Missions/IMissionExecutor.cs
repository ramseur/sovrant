namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 51 — runs one mission forward by one engine cycle:
/// plan → execute → acceptance gate → journal → terminal state.
/// Idempotent at the mission-state boundary: calling <see cref="RunAsync"/>
/// twice on an already-completed mission is a no-op.
/// </summary>
public interface IMissionExecutor
{
    Task<Mission> RunAsync(string missionId, CancellationToken ct = default);
}
