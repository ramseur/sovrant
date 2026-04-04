namespace Sovrant.Agents.Models;

/// <summary>Represents a unit of work routed through the multi-agent system.</summary>
/// <param name="Id">Unique identifier for this task (caller-assigned or auto-generated).</param>
/// <param name="Prompt">The natural-language instruction or goal for the agent.</param>
/// <param name="AssignedAgentName">
/// Optional name of the specific agent to handle this task.
/// When <see langword="null"/>, the coordinator selects an agent automatically.
/// </param>
/// <param name="Metadata">Optional key-value context forwarded to the agent unchanged.</param>
public sealed record AgentTask(
    string Id,
    string Prompt,
    string? AssignedAgentName = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    /// <summary>The UTC time at which the task was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Creates a new task with an auto-generated identifier.</summary>
    public static AgentTask Create(string prompt, string? assignedAgentName = null) =>
        new(Guid.NewGuid().ToString("N"), prompt, assignedAgentName);
}
