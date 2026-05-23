namespace Sovrant.Agents.Swarm.Bus;

/// <summary>Controls how a swarm interacts with the OpenClaw federation bus.</summary>
public enum SwarmFederationMode
{
    /// <summary>No federation — swarm runs locally, no bus traffic.</summary>
    Silo,

    /// <summary>
    /// Peer federation — the swarm publishes events to the OpenClaw bus so that
    /// other connected Sovrant instances can observe progress and results.
    /// </summary>
    Federated,

    /// <summary>
    /// Manager-led federation — this swarm acts as a child; a remote swarm-manager
    /// agent dispatches tasks and collects results via OpenClaw.
    /// </summary>
    ManagerLed,
}
