using System.Text;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Agents.Shared;

/// <summary>
/// An in-process agent backed by a <see cref="IConversationRuntime"/>. Runs a full
/// agentic turn (including tool use) against the LLM and collects the text output.
/// </summary>
public sealed partial class SovrantAgent : BaseAgent
{
    private readonly IConversationRuntime _runtime;

    [LoggerMessage(Level = LogLevel.Error, Message = "Agent '{AgentName}' failed on task '{TaskId}'")]
    private static partial void LogAgentFailed(ILogger logger, Exception ex, string agentName, string taskId);

    public SovrantAgent(
        string name,
        AgentRole role,
        IConversationRuntime runtime,
        ILogger logger)
        : base(name, role, logger)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    /// <summary>
    /// Binds this agent's runtime to a specific session id so its turns are
    /// persisted under that id. Used by <see cref="AdHocAgentRunner"/> to align
    /// the runtime's session id with the agent_runs ledger RunId, which lets
    /// the Activity drill-down find the run by its RunId.
    /// </summary>
    public Task InitializeSessionAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
        => _runtime.InitializeSessionAsync(sessionId, ownerUserId, ct);

    /// <inheritdoc/>
    public override async Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        var sb = new StringBuilder();
        try
        {
            await foreach (var ev in _runtime.RunTurnAsync(task.Prompt, ct).ConfigureAwait(false))
            {
                switch (ev)
                {
                    case RuntimeEvent.TextChunk tc:
                        sb.Append(tc.Text);
                        break;
                    case RuntimeEvent.RuntimeError err:
                        return AgentResult.Fail(task.Id, err.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return AgentResult.Fail(task.Id, "Task was cancelled.");
        }
        catch (InvalidOperationException ex)
        {
            LogAgentFailed(Logger, ex, Name, task.Id);
            return AgentResult.Fail(task.Id, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            LogAgentFailed(Logger, ex, Name, task.Id);
            return AgentResult.Fail(task.Id, ex.Message);
        }

        var output = sb.Length > 0 ? sb.ToString() : "(agent returned no output)";
        return AgentResult.Ok(task.Id, output);
    }
}
