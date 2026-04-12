namespace Sovrant.Agents.Swarm;

/// <summary>Tracks live and completed swarm results for status queries.</summary>
public interface ISwarmStateTracker
{
    /// <summary>Updates or inserts the swarm result.</summary>
    void Update(string swarmId, SwarmResult result);

    /// <summary>Returns the result for a given swarm, or <c>null</c> if not found.</summary>
    SwarmResult? Get(string swarmId);

    /// <summary>Returns the most recently updated swarm result, or <c>null</c> if none.</summary>
    SwarmResult? GetLatest();

    /// <summary>Returns all tracked swarm IDs.</summary>
    IReadOnlyList<string> ListIds();
}
