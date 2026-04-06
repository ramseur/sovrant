using Sovrant.Agents.Swarm;

namespace Sovrant.Tools.Swarm;

/// <summary>
/// Reports swarm progress events to the user. The CLI implements this to render
/// live progress; the server can implement it to emit SSE events.
/// </summary>
public interface ISwarmProgressReporter
{
    /// <summary>Reports a swarm event to the user.</summary>
    void Report(SwarmEvent evt);
}

/// <summary>Default no-op reporter for non-interactive contexts.</summary>
public sealed class NullSwarmProgressReporter : ISwarmProgressReporter
{
    public void Report(SwarmEvent evt) { }
}
