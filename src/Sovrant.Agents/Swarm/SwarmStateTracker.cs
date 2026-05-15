using System.Collections.Concurrent;

namespace Sovrant.Agents.Swarm;

/// <summary>Tracks live and completed swarm results for status queries.</summary>
public sealed class SwarmStateTracker : ISwarmStateTracker
{
    private readonly ConcurrentDictionary<string, SwarmResult> _swarms = new(StringComparer.Ordinal);

    /// <summary>Updates or inserts the swarm result.</summary>
    public void Update(string swarmId, SwarmResult result) => _swarms[swarmId] = result;

    /// <summary>Returns the result for a given swarm, or <c>null</c> if not found.</summary>
    public SwarmResult? Get(string swarmId) =>
        _swarms.TryGetValue(swarmId, out var result) ? result : null;

    /// <summary>Returns the most recently updated swarm result, or <c>null</c> if none.</summary>
    public SwarmResult? GetLatest() =>
        _swarms.Values.OrderByDescending(r => r.Duration).FirstOrDefault();

    /// <summary>Returns all tracked swarm IDs.</summary>
    public IReadOnlyList<string> ListIds() => [.. _swarms.Keys];
}
