namespace Sovrant.Runtime.Conversation;

public abstract partial record RuntimeEvent
{
    /// <summary>A team agent was spawned.</summary>
    public sealed record AgentSpawned(string AgentId, string AgentName, string Role) : RuntimeEvent;

    /// <summary>A team agent completed its task.</summary>
    public sealed record AgentCompleted(string AgentId, string AgentName, string Output) : RuntimeEvent;

    /// <summary>A team agent failed its task.</summary>
    public sealed record AgentFailed(string AgentId, string AgentName, string Error) : RuntimeEvent;
}
