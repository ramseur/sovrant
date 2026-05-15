namespace Sovrant.Agents.Models;

/// <summary>The result returned by an agent after handling an <see cref="AgentTask"/>.</summary>
/// <param name="TaskId">Identifies the task this result corresponds to.</param>
/// <param name="Success">Whether the agent completed the task without error.</param>
/// <param name="Output">The agent's output text (empty on failure).</param>
/// <param name="Error">Error message when <paramref name="Success"/> is <see langword="false"/>.</param>
public sealed record AgentResult(
    string TaskId,
    bool Success,
    string Output,
    string? Error = null)
{
    /// <summary>Creates a successful result with the given output.</summary>
    public static AgentResult Ok(string taskId, string output) =>
        new(taskId, true, output);

    /// <summary>Creates a failed result with the given error message.</summary>
    public static AgentResult Fail(string taskId, string error) =>
        new(taskId, false, string.Empty, error);
}
