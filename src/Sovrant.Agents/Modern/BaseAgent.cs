using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Modern;

/// <summary>
/// Base class for in-process agents. Subclasses override <see cref="HandleAsync"/> to
/// provide role-specific behaviour. Each instance owns an unbounded async inbox channel;
/// <see cref="RunLoopAsync"/> drains it until the channel is completed or the token fires.
/// </summary>
public abstract class BaseAgent : IAgent
{
    private readonly Channel<AgentTask> _inbox =
        Channel.CreateUnbounded<AgentTask>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

    /// <summary>Logger available to all subclass implementations.</summary>
    protected ILogger Logger { get; }

    /// <param name="name">Unique agent name used for routing.</param>
    /// <param name="role">The functional role of this agent within a team.</param>
    /// <param name="logger">Logger for this agent instance.</param>
    protected BaseAgent(string name, AgentRole role, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(logger);
        Name = name;
        Role = role;
        Logger = logger;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>The functional role of this agent within a multi-agent team.</summary>
    public AgentRole Role { get; }

    /// <inheritdoc/>
    public abstract Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default);

    /// <summary>
    /// Enqueues a task onto this agent's inbox for processing by <see cref="RunLoopAsync"/>.
    /// </summary>
    public ValueTask EnqueueAsync(AgentTask task, CancellationToken ct = default) =>
        _inbox.Writer.WriteAsync(task, ct);

    /// <summary>
    /// Runs the agent's internal message loop. Processes tasks from the inbox sequentially
    /// until the channel is completed (via <see cref="Complete"/>) or <paramref name="ct"/> fires.
    /// </summary>
    /// <remarks>
    /// TODO (Phase 19): Feed each result back to the <see cref="MultiAgentCoordinator"/> via a
    /// result channel or callback so the caller's <c>RunTaskAsync</c> awaitable can complete.
    /// </remarks>
    public async Task RunLoopAsync(CancellationToken ct = default)
    {
        await foreach (var task in _inbox.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            _ = await HandleAsync(task, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Signals that no more tasks will be enqueued. The message loop will drain remaining
    /// items and then exit cleanly.
    /// </summary>
    public void Complete() => _inbox.Writer.Complete();
}
